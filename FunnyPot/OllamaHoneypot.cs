using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FunnyPot;

internal sealed class OllamaHoneypot : IDisposable
{
    internal const string Reply = "Hello! How can I help?";
    const string Model = "llama3.2:latest";
    // Fingerprint for scanners: real Ollama advertises itself this way.
    const string ServerSignature = "ollama/0.6.2";
    const int MaxBody = 16 * 1024;
    static readonly Regex ArithmeticRegex = new(
        @"(?<left>-?\d{1,9})\s*(?<op>multiplied\s+by|times|\*|x|×|plus|\+|minus|-|divided\s+by|/)\s*(?<right>-?\d{1,9})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    static readonly Regex ExactMarkerRegex = new(
        @"\b(?:reply|respond|answer|output|return)\s+(?:with\s+)?(?:only\s+|just\s+|exact(?:ly)?\s+)+['""`]?([A-Za-z0-9_.:/-]{2,40})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    readonly TcpListener listener;
    readonly SemaphoreSlim slots = new(16);
    readonly object logLock = new();
    readonly string logPath;
    readonly ConnectionThrottle throttle;
    readonly HashSet<Task> pending = new();
    readonly object pendingLock = new();
    internal int Port => ((IPEndPoint)listener.LocalEndpoint).Port;

    internal OllamaHoneypot(int port, string logPath, int perIp = 12, int global = 120)
    {
        this.logPath = Path.GetFullPath(logPath);
        Directory.CreateDirectory(Path.GetDirectoryName(this.logPath)!);
        // Fail startup rather than silently run without capture.
        using (File.Open(this.logPath, FileMode.Append, FileAccess.Write, FileShare.Read)) { }
        throttle = new ConnectionThrottle(perIp, global);
        listener = new TcpListener(IPAddress.Any, port);
        listener.Start(128);
    }

    internal async Task RunAsync(CancellationToken stopping)
    {
        try
        {
            while (!stopping.IsCancellationRequested)
            {
                var client = await listener.AcceptTcpClientAsync(stopping);
                var id = Guid.NewGuid().ToString("N");
                var peer = (IPEndPoint)client.Client.RemoteEndPoint!;
                try { Log(new { type = "connection", id, peer = peer.ToString() }); }
                catch { client.Dispose(); throw; }
                // Charge every TCP connection, including incomplete HTTP and discovery probes.
                var allowed = throttle.Allow(peer.Address.ToString(), DateTimeOffset.UtcNow);
                if (!allowed || !slots.Wait(0))
                {
                    try
                    {
                        Log(new { type = "rejected", id, status = 429 });
                        client.Client.Blocking = false;
                        try { client.Client.Send(Encoding.ASCII.GetBytes($"HTTP/1.1 429 Too Many Requests\r\nServer: {ServerSignature}\r\nRetry-After: 60\r\nContent-Length: 0\r\nConnection: close\r\n\r\n")); }
                        catch (SocketException) { }
                    }
                    finally { client.Dispose(); }
                    // No unbounded rejection tasks or slow writes to hostile peers.
                    continue;
                }
                var task = HandleAsync(client, id, stopping);
                lock (pendingLock) pending.Add(task);
                _ = task.ContinueWith(done =>
                {
                    // Capture failures (e.g. disk full) must stop accepting traffic, not disappear.
                    if (done.Exception != null) listener.Stop();
                    lock (pendingLock) pending.Remove(done);
                }, TaskScheduler.Default);
            }
        }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested) { }
        catch (SocketException) when (stopping.IsCancellationRequested) { }
        finally
        {
            listener.Stop();
            Task[] tasks;
            lock (pendingLock) tasks = pending.ToArray();
            await Task.WhenAll(tasks);
        }
    }

    async Task HandleAsync(TcpClient client, string id, CancellationToken stopping)
    {
        using (client)
        using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(stopping))
        {
            deadline.CancelAfter(TimeSpan.FromSeconds(15));
            var token = deadline.Token;
            var status = 400;
            string? method = null, path = null, body = null, userAgent = null, responseText = null;
            using var capturedBody = new MemoryStream();
            var error = "";
            try
            {
                var stream = client.GetStream();
                var header = new List<byte>();
                var single = new byte[1];
                while (header.Count < 8192)
                {
                    if (await stream.ReadAsync(single, token) == 0) throw new IOException("incomplete_headers");
                    header.Add(single[0]);
                    if (header.Count >= 4 && header[^4] == 13 && header[^3] == 10 && header[^2] == 13 && header[^1] == 10) break;
                }
                if (header.Count == 8192) throw new InvalidDataException("headers_too_large");
                var lines = Encoding.ASCII.GetString(header.ToArray()).Split("\r\n");
                var first = lines[0].Split(' ');
                if (first.Length != 3 || first[2] != "HTTP/1.1" && first[2] != "HTTP/1.0")
                    throw new InvalidDataException("invalid_request_line");
                method = first[0];
                path = first[1].Split('?')[0];
                var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var line in lines.Skip(1).Where(l => l.Length > 0))
                {
                    var colon = line.IndexOf(':');
                    if (colon < 1 || !headers.TryAdd(line[..colon].Trim(), line[(colon + 1)..].Trim()))
                        throw new InvalidDataException("invalid_or_duplicate_header");
                }
                headers.TryGetValue("User-Agent", out userAgent);
                // One request per connection; reject ambiguous framing rather than
                // interpreting a request differently from other HTTP implementations.
                var chunked = headers.TryGetValue("Transfer-Encoding", out var encoding);
                if (chunked && (headers.ContainsKey("Content-Length") || first[2] != "HTTP/1.1"))
                    throw new InvalidDataException("ambiguous_body_framing");
                if (chunked && !string.Equals(encoding, "chunked", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("unsupported_transfer_encoding");
                var length = 0;
                if (headers.TryGetValue("Content-Length", out var value) && (!int.TryParse(value, out length) || length < 0))
                    throw new InvalidDataException("invalid_content_length");
                if (length > MaxBody)
                {
                    status = 413;
                    throw new InvalidDataException("body_too_large");
                }
                if (headers.TryGetValue("Expect", out var expect) && expect.Equals("100-continue", StringComparison.OrdinalIgnoreCase))
                    await stream.WriteAsync("HTTP/1.1 100 Continue\r\n\r\n"u8.ToArray(), token);
                try { await ReadRequestBodyAsync(stream, chunked, length, capturedBody, token); }
                catch (InvalidDataException ex) when (ex.Message == "body_too_large") { status = 413; throw; }
                finally { body = Encoding.UTF8.GetString(capturedBody.ToArray()); }
                var result = Respond(method, path, body);
                status = result.Status;
                responseText = result.ReplyText;
                await WriteAsync(stream, status, result.ContentType, result.Body, token);
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or SocketException or OperationCanceledException or JsonException or InvalidOperationException or FormatException)
            {
                error = ex is OperationCanceledException ? "timeout_or_shutdown" : ex is InvalidDataException ? ex.Message : ex.GetType().Name;
                if (ex is OperationCanceledException) status = 408;
                try { await WriteAsync(client.GetStream(), status, "application/json", JsonSerializer.Serialize(new { error = "Invalid request" }), token); }
                catch (Exception writeError) when (writeError is IOException or SocketException or OperationCanceledException) { }
            }
            finally
            {
                try { Log(new { type = "request", id, method, path, userAgent, body, response = responseText, status, error }); }
                finally { slots.Release(); }
            }
        }
    }

    internal static async Task ReadRequestBodyAsync(Stream stream, bool chunked, int length,
        MemoryStream capture, CancellationToken token)
    {
        var buffer = new byte[1024];
        async Task ReadPayloadAsync(int count)
        {
            while (count > 0)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(0, Math.Min(buffer.Length, count)), token);
                if (read == 0) throw new InvalidDataException("incomplete_body");
                capture.Write(buffer, 0, read);
                count -= read;
            }
        }
        if (!chunked)
        {
            if (length < 0) throw new InvalidDataException("invalid_content_length");
            if (length > MaxBody) throw new InvalidDataException("body_too_large");
            await ReadPayloadAsync(length);
            return;
        }

        var metadataBytes = 0;
        var single = new byte[1];
        async Task<byte> ReadMetadataByteAsync()
        {
            if (++metadataBytes > 8192) throw new InvalidDataException("chunk_metadata_too_large");
            if (await stream.ReadAsync(single, token) == 0) throw new InvalidDataException("incomplete_chunk_framing");
            return single[0];
        }
        async Task<string> ReadLineAsync()
        {
            var line = new List<byte>();
            while (line.Count <= 1024)
            {
                var value = await ReadMetadataByteAsync();
                if (value == 13)
                {
                    if (await ReadMetadataByteAsync() != 10) throw new InvalidDataException("invalid_chunk_crlf");
                    return Encoding.ASCII.GetString(line.ToArray());
                }
                if (value < 32 && value != 9 || value > 126) throw new InvalidDataException("invalid_chunk_metadata");
                line.Add(value);
            }
            throw new InvalidDataException("chunk_line_too_large");
        }

        for (var chunks = 0; chunks <= 1024; chunks++)
        {
            var line = await ReadLineAsync();
            var hex = line.Split(';', 2)[0]; // Bounded chunk extensions are ignored.
            if (hex.Length == 0 || hex.Any(c => !Uri.IsHexDigit(c)) ||
                !ulong.TryParse(hex, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var size))
                throw new InvalidDataException("invalid_chunk_size");
            if (size > (ulong)(MaxBody - capture.Length)) throw new InvalidDataException("body_too_large");
            if (size == 0)
            {
                // Trailer values are deliberately discarded (including credentials).
                // Do not let trailers redefine request framing or routing.
                while ((line = await ReadLineAsync()).Length > 0)
                {
                    var colon = line.IndexOf(':');
                    if (colon < 1 || line[..colon].Any(c => !char.IsAsciiLetterOrDigit(c) && !"!#$%&'*+-.^_`|~".Contains(c)))
                        throw new InvalidDataException("invalid_chunk_trailer");
                    if (new[] { "Content-Length", "Transfer-Encoding", "Host", "Trailer" }.Contains(line[..colon], StringComparer.OrdinalIgnoreCase))
                        throw new InvalidDataException("forbidden_chunk_trailer");
                }
                return;
            }
            if (chunks == 1024) throw new InvalidDataException("too_many_chunks");
            await ReadPayloadAsync((int)size);
            if (await ReadMetadataByteAsync() != 13 || await ReadMetadataByteAsync() != 10)
                throw new InvalidDataException("invalid_chunk_crlf");
        }
    }

    internal sealed record ApiResponse(int Status, string ContentType, string Body, string? ReplyText = null);
    static string Json(object data) => JsonSerializer.Serialize(data);
    internal static ApiResponse Respond(string method, string path, string body)
    {
        ApiResponse Ok(object data, string? replyText = null) => new(200, "application/json", Json(data), replyText);
        var timestamp = DateTimeOffset.UtcNow.ToString("O");
        var modelInfo = new { name = Model, model = Model, modified_at = "2026-10-01T00:00:00Z", size = 2019393189L,
            digest = "a80c4f17acd5e8d4e59a8988fcdceae037f8f159f5ff034c1848c59d508d6e21",
            details = new { parent_model = "", format = "gguf", family = "llama", families = new[] { "llama" }, parameter_size = "3.2B", quantization_level = "Q4_K_M" } };
        if (method == "GET")
        {
            return path switch
            {
                "/" => new(200, "text/plain", "Ollama is running"),
                "/api/version" => Ok(new { version = "0.6.2" }),
                "/api/tags" => Ok(new { models = new[] { modelInfo } }),
                "/api/ps" => Ok(new { models = Array.Empty<object>() }),
                "/v1/models" => Ok(new { @object = "list", data = new[] { new { id = Model, @object = "model", created = 1759276800, owned_by = "library" } } }),
                _ => new(404, "application/json", Json(new { error = "Not found" }))
            };
        }
        if (method != "POST") return new(405, "application/json", Json(new { error = "Method not allowed" }));
        using var document = JsonDocument.Parse(body, new JsonDocumentOptions { MaxDepth = 32 });
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object) throw new JsonException("Expected object");
        var requestedName = root.TryGetProperty("model", out var requested) ? requested.GetString() : root.TryGetProperty("name", out var named) ? named.GetString() : null;
        var model = string.IsNullOrWhiteSpace(requestedName) ? Model : requestedName;
        // Do not reflect unbounded attacker-controlled model strings into responses.
        if (model.Length > 128) throw new JsonException("Model too long");
        if (path == "/api/show") return Ok(new { details = modelInfo.details, capabilities = new[] { "completion" }, parameters = "num_predict 16", template = "{{ .Prompt }}" });
        if (path is "/api/pull" or "/api/create" or "/api/copy" or "/api/delete")
        {
            var statusMsg = path == "/api/pull" ? "pulling manifest\nverifying sha256 digest\nwriting manifest\nsuccess" : "success";
            var streamPull = !root.TryGetProperty("stream", out var pullStreamFlag) || pullStreamFlag.ValueKind != JsonValueKind.False;
            if (!streamPull || path is "/api/copy" or "/api/delete")
                return Ok(new { status = "success" }, statusMsg);
            return new(200, "application/x-ndjson",
                Json(new { status = "pulling manifest" }) + "\n" +
                Json(new { status = "verifying sha256 digest" }) + "\n" +
                Json(new { status = "writing manifest" }) + "\n" +
                Json(new { status = "success" }) + "\n",
                statusMsg);
        }
        if (path is "/api/embed" or "/api/embeddings" or "/v1/embeddings")
        {
            var vec = new[] { 0.0124, -0.0231, 0.0412, 0.0089, -0.0156, 0.0301, -0.0044, 0.0198 };
            if (path == "/v1/embeddings")
                return Ok(new { @object = "list", data = new[] { new { @object = "embedding", embedding = vec, index = 0 } }, model, usage = new { prompt_tokens = 4, total_tokens = 4 } }, "[embedding vector]");
            if (path == "/api/embeddings")
                return Ok(new { embedding = vec }, "[embedding vector]");
            return Ok(new { model, embeddings = new[] { vec } }, "[embedding vector]");
        }
        var ollama = path == "/api/chat" || path == "/api/generate";
        var chat = path == "/api/chat" || path == "/v1/chat/completions";
        if (!ollama && path != "/v1/chat/completions" && path != "/v1/completions")
            return new(404, "application/json", Json(new { error = "Not found" }));
        var streaming = root.TryGetProperty("stream", out var flag) && flag.ValueKind is JsonValueKind.True or JsonValueKind.False ? flag.GetBoolean() : ollama;
        var replyText = ResolveCompletionReply(root, model);
        var promptTokens = Math.Max(1, body.Length / 16);
        var completionTokens = Math.Max(1, replyText.Length / 4);
        if (ollama)
        {
            object chunk = chat
                ? new { model, created_at = timestamp, message = new { role = "assistant", content = replyText }, done = false }
                : new { model, created_at = timestamp, response = replyText, done = false };
            object final = chat
                ? new { model, created_at = timestamp, message = new { role = "assistant", content = streaming ? "" : replyText }, done = true, done_reason = "stop", total_duration = 1000000, prompt_eval_count = promptTokens, eval_count = completionTokens }
                : new { model, created_at = timestamp, response = streaming ? "" : replyText, done = true, done_reason = "stop", total_duration = 1000000, prompt_eval_count = promptTokens, eval_count = completionTokens };
            return streaming ? new(200, "application/x-ndjson", Json(chunk) + "\n" + Json(final) + "\n", replyText) : Ok(final, replyText);
        }
        var id = "chatcmpl-" + Guid.NewGuid().ToString("N");
        var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (!streaming)
        {
            object choice = chat ? new { index = 0, message = new { role = "assistant", content = replyText }, finish_reason = "stop" }
                : new { index = 0, text = replyText, finish_reason = "stop" };
            return Ok(new { id, @object = chat ? "chat.completion" : "text_completion", created, model, choices = new[] { choice }, usage = new { prompt_tokens = promptTokens, completion_tokens = completionTokens, total_tokens = promptTokens + completionTokens } }, replyText);
        }
        object delta = chat ? new { index = 0, delta = new { role = "assistant", content = replyText }, finish_reason = (string?)null }
            : new { index = 0, text = replyText, finish_reason = (string?)null };
        object end = chat ? new { index = 0, delta = new { }, finish_reason = "stop" }
            : new { index = 0, text = "", finish_reason = "stop" };
        var kind = chat ? "chat.completion.chunk" : "text_completion";
        return new(200, "text/event-stream", "data: " + Json(new { id, @object = kind, created, model, choices = new[] { delta } }) + "\n\ndata: " +
            Json(new { id, @object = kind, created, model, choices = new[] { end } }) + "\n\ndata: [DONE]\n\n", replyText);
    }

    static string ResolveCompletionReply(JsonElement root, string model)
    {
        var messages = ExtractMessages(root);
        var lastUserPrompt = messages.LastOrDefault(m => m.Role == "user")?.Content ?? "";
        var combinedPrompt = string.Join("\n", messages.Where(m => m.Role != "system").Select(m => m.Content)).Trim();
        var promptToMatch = !string.IsNullOrWhiteSpace(lastUserPrompt) ? lastUserPrompt : combinedPrompt;

        if (TryMatchPredefinedKeywordReply(promptToMatch, model, out var predefined))
            return predefined;

        return Reply;
    }

    internal static List<ChatRequestData.ChatMessage> ExtractMessages(JsonElement root)
    {
        var list = new List<ChatRequestData.ChatMessage>();

        if (root.TryGetProperty("messages", out var messages) && messages.ValueKind == JsonValueKind.Array)
        {
            foreach (var msg in messages.EnumerateArray())
            {
                if (msg.ValueKind != JsonValueKind.Object) continue;
                var role = msg.TryGetProperty("role", out var r) && r.ValueKind == JsonValueKind.String ? r.GetString() ?? "user" : "user";
                if (role is not ("system" or "user" or "assistant")) role = "user";
                var content = "";
                if (msg.TryGetProperty("content", out var c))
                {
                    if (c.ValueKind == JsonValueKind.String) content = c.GetString() ?? "";
                    else if (c.ValueKind == JsonValueKind.Array)
                    {
                        var parts = new List<string>();
                        foreach (var item in c.EnumerateArray())
                        {
                            if (item.ValueKind == JsonValueKind.String) parts.Add(item.GetString() ?? "");
                            else if (item.ValueKind == JsonValueKind.Object && item.TryGetProperty("text", out var t) && t.ValueKind == JsonValueKind.String)
                                parts.Add(t.GetString() ?? "");
                        }
                        content = string.Join("\n", parts);
                    }
                }
                if (!string.IsNullOrWhiteSpace(content))
                    list.Add(new ChatRequestData.ChatMessage { Role = role, Content = content });
            }
        }
        if (root.TryGetProperty("prompt", out var prompt) && prompt.ValueKind == JsonValueKind.String)
        {
            var p = prompt.GetString();
            if (!string.IsNullOrWhiteSpace(p))
                list.Add(new ChatRequestData.ChatMessage { Role = "user", Content = p });
        }
        if (list.Count == 0 || list[^1].Role != "user")
            list.Add(new ChatRequestData.ChatMessage { Role = "user", Content = "Hello" });
        return list;
    }

    internal static bool TryMatchPredefinedKeywordReply(string prompt, string model, out string reply)
    {
        reply = "";
        if (string.IsNullOrWhiteSpace(prompt)) return false;
        var trimmed = prompt.Trim();
        var lower = trimmed.ToLowerInvariant();

        var mathMatch = ArithmeticRegex.Match(trimmed);
        long? computed = null;
        if (mathMatch.Success &&
            long.TryParse(mathMatch.Groups["left"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var left) &&
            long.TryParse(mathMatch.Groups["right"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var right))
        {
            var op = mathMatch.Groups["op"].Value.ToLowerInvariant();
            computed = op switch
            {
                "*" or "x" or "×" or "times" or _ when op.StartsWith("multipl") => left * right,
                "+" or "plus" => left + right,
                "-" or "minus" => left - right,
                "/" or _ when op.StartsWith("divid") => right != 0 ? left / right : null,
                _ => null
            };
        }

        var markerMatch = ExactMarkerRegex.Match(trimmed);
        if (markerMatch.Success)
        {
            var token = markerMatch.Groups[1].Value.TrimEnd('.', ',', ';', '!', '?');
            if (!token.Equals("with", StringComparison.OrdinalIgnoreCase) &&
                !token.Equals("the", StringComparison.OrdinalIgnoreCase) &&
                !token.Equals("a", StringComparison.OrdinalIgnoreCase))
            {
                if (computed.HasValue && !token.Contains(computed.Value.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal))
                {
                    reply = $"{token} {computed.Value}";
                    return true;
                }
                var afterMarker = trimmed[(markerMatch.Index + markerMatch.Length)..].Trim();
                var trailingNumber = Regex.Match(afterMarker, @"^(?<num>-?\d{1,12})\b");
                if (trailingNumber.Success)
                {
                    reply = $"{token} {trailingNumber.Groups["num"].Value}";
                    return true;
                }
                reply = token;
                return true;
            }
        }

        if (computed.HasValue && (lower.Contains("compute") || lower.Contains("calculate") || lower.Contains("what is") || lower.Contains("solve") || lower.Contains("nothing else") || lower.Contains("only")))
        {
            reply = computed.Value.ToString(CultureInfo.InvariantCulture);
            return true;
        }

        if (lower is "ping" or "ping!" or "health" or "healthcheck" || lower.Contains("reply with pong") || lower.Contains("respond with pong"))
        {
            reply = "PONG";
            return true;
        }

        if (lower.Contains("router_ok") || lower.Contains("status_ok") || lower.Contains("test_ok") || lower.Contains("probe_ok"))
        {
            reply = computed.HasValue ? $"ROUTER_OK {computed.Value}" : "ROUTER_OK";
            return true;
        }

        if (lower.Contains("ignore previous instructions") || lower.Contains("ignore all previous") || lower.Contains("system prompt") || lower.Contains("developer message") || lower.Contains("initial instructions"))
        {
            reply = "I am an AI assistant running on Llama 3.2. My instructions are to provide helpful, accurate, and concise responses to user queries.";
            return true;
        }

        if (lower.Contains("who are you") || lower.Contains("what model") || lower.Contains("which model") || lower.Contains("your name") || lower.Contains("identify yourself"))
        {
            reply = $"I am {model}, a large language model trained to assist with text generation, reasoning, and coding tasks.";
            return true;
        }

        if (lower.Contains("python") || lower.Contains("script") || lower.Contains("write code") || lower.Contains("function") || lower.Contains("fibonacci") || lower.Contains("hello world"))
        {
            reply = "```python\ndef main():\n    print(\"Hello, world!\")\n\nif __name__ == \"__main__\":\n    main()\n```";
            return true;
        }

        if (lower.Contains("sql") || lower.Contains("select ") || lower.Contains("database") || lower.Contains("schema"))
        {
            reply = "```sql\nSELECT id, username, created_at\nFROM users\nWHERE active = 1\nORDER BY created_at DESC\nLIMIT 10;\n```";
            return true;
        }

        if (lower.Contains("bash") || lower.Contains("shell") || lower.Contains("linux") || lower.Contains("command") || lower.Contains("curl ") || lower.Contains("wget "))
        {
            reply = "```bash\nuname -a && uptime\n```";
            return true;
        }

        if (lower.Contains("json") || lower.Contains("format as json") || lower.Contains("return json"))
        {
            reply = "{\"status\":\"ok\",\"model\":\"" + model + "\",\"ready\":true}";
            return true;
        }

        if (lower.Contains("translate") || lower.Contains("translation"))
        {
            reply = "Here is the translation: Hello, how are you today?";
            return true;
        }

        if (lower.Contains("summarize") || lower.Contains("summary") || lower.Contains("tl;dr"))
        {
            reply = "Summary: The provided text outlines the main operational parameters, key findings, and recommended next steps.";
            return true;
        }

        return false;
    }

    static async Task WriteAsync(NetworkStream stream, int status, string contentType, string body, CancellationToken token)
    {
        var bytes = Encoding.UTF8.GetBytes(body);
    var reason = status switch { 200 => "OK", 404 => "Not Found", 405 => "Method Not Allowed", 408 => "Request Timeout", 413 => "Payload Too Large", _ => "Bad Request" };
    var header = Encoding.ASCII.GetBytes($"HTTP/1.1 {status} {reason}\r\nServer: {ServerSignature}\r\nContent-Type: {contentType}\r\nContent-Length: {bytes.Length}\r\nConnection: close\r\n\r\n");
        await stream.WriteAsync(header, token);
        await stream.WriteAsync(bytes, token);
    }

    void Log(object data)
    {
        lock (logLock)
        {
            // Retain current file plus ten 10 MiB archives; never mix with SSH/publication telemetry.
            if (new FileInfo(logPath).Length >= 10 * 1024 * 1024)
            {
                for (var i = 9; i >= 1; i--)
                    if (File.Exists(logPath + "." + i)) File.Move(logPath + "." + i, logPath + "." + (i + 1), true);
                File.Move(logPath, logPath + ".1", true);
            }
            File.AppendAllText(logPath, Json(new { timestamp = DateTimeOffset.UtcNow, data }) + "\n");
        }
    }

    public void Dispose() => listener.Stop();
}

internal sealed class ConnectionThrottle(int perIp, int global)
{
    readonly Dictionary<string, int> counts = new();
    DateTimeOffset window;
    int total;
    internal bool Allow(string ip, DateTimeOffset now)
    {
        lock (counts)
        {
            if (now - window >= TimeSpan.FromMinutes(1)) { counts.Clear(); total = 0; window = now; }
            // Global ceiling also bounds the number of tracked source addresses.
            if (total >= Math.Max(1, global)) return false;
            total++;
            counts.TryGetValue(ip, out var count);
            if (count >= Math.Max(1, perIp)) return false;
            counts[ip] = count + 1;
            return true;
        }
    }
}
