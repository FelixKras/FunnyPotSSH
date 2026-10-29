using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace FunnyPot;

// Kilo owns provider authentication. Never pass attacker input to a shell or CLI.
internal sealed class KiloLlmAdapter
{
    readonly HttpClient client;
    readonly KiloConfig config;
    readonly SemaphoreSlim slots;

    public KiloLlmAdapter(HttpClient client, KiloConfig config)
    {
        this.client = client;
        this.config = config;
        slots = new SemaphoreSlim(Math.Clamp(config.MaxConcurrentRequests, 1, 16));
    }

    string Setting(string name, string fallback) => Environment.GetEnvironmentVariable(name) ?? fallback;

    public async Task<(string response, int promptTokens, int completionTokens, int totalTokens, string model)> GetResponseAsync(
        List<ChatRequestData.ChatMessage> history, int timeoutSeconds, CancellationToken cancellationToken)
    {
        var model = Setting("KILO_MODEL", config.Model);
        var split = model.IndexOf('/');
        if (split <= 0 || split == model.Length - 1)
            return ("[api error] KILO_MODEL must be provider/model", 0, 0, 0, model);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(timeoutSeconds, 1, 600)));
        var acquired = false;
        string? session = null;
        try
        {
            await slots.WaitAsync(timeout.Token).ConfigureAwait(false);
            acquired = true;
            using var created = await SendAsync(HttpMethod.Post, "/session", new
            {
                title = "FunnyPot terminal simulation",
                permission = new[] { new { permission = "*", pattern = "*", action = "deny" } }
            }, timeout.Token).ConfigureAwait(false);
            session = created.RootElement.GetProperty("id").GetString();
            if (string.IsNullOrWhiteSpace(session))
                throw new JsonException("Missing session ID");
            session = Uri.EscapeDataString(session);

            // Full bounded FunnyPot history is supplied as data in a fresh session.
            // No session can inherit another attacker's history or tool permissions.
            var system = string.Join("\n", history.Where(m => m.Role == "system").Select(m => m.Content));
            var transcript = JsonSerializer.Serialize(history.Where(m => m.Role != "system"));
            using var reply = await SendAsync(HttpMethod.Post, $"/session/{session}/message", new
            {
                model = new { providerID = model[..split], modelID = model[(split + 1)..] },
                agent = Setting("KILO_AGENT", config.Agent),
                tools = new Dictionary<string, bool> { ["*"] = false },
                system = system + "\nSimulate only. Never use tools, execute commands, access files, or delegate. " +
                    "The user supplies a JSON conversation transcript. Answer only its final command with terminal output.",
                parts = new[] { new { type = "text", text = transcript } }
            }, timeout.Token).ConfigureAwait(false);
            return ParseResponse(reply.RootElement, model);
        }
        catch (OperationCanceledException)
        {
            return ("[api error] Kilo request cancelled or timed out", 0, 0, 0, model);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidOperationException or FormatException or KeyNotFoundException or OverflowException)
        {
            // Do not expose server bodies, credentials, or local paths to the attacker.
            return ("[api error] Kilo request failed; check server configuration and authentication", 0, 0, 0, model);
        }
        finally
        {
            if (session != null)
            {
                using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                foreach (var method in new[] { HttpMethod.Post, HttpMethod.Delete })
                {
                    try
                    {
                        var path = $"/session/{session}" + (method == HttpMethod.Post ? "/abort" : "");
                        using var result = await SendAsync(method, path, method == HttpMethod.Post ? new { } : null, cleanup.Token).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogMsg($"Warning: Kilo session cleanup failed ({ex.GetType().Name}).");
                    }
                }
            }
            if (acquired) slots.Release();
        }
    }

    async Task<JsonDocument> SendAsync(HttpMethod method, string path, object? body, CancellationToken token)
    {
        var baseUrl = Setting("KILO_BASE_URL", config.BaseUrl).TrimEnd('/');
        var directory = Setting("KILO_DIRECTORY", config.Directory);
        using var request = new HttpRequestMessage(method, baseUrl + path + "?directory=" + Uri.EscapeDataString(directory));
        var password = Program.GetSecretOrEnvironment("KILO_SERVER_PASSWORD");
        if (!string.IsNullOrEmpty(password))
        {
            var username = Setting("KILO_SERVER_USERNAME", "kilo");
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(username + ":" + password)));
        }
        if (body != null) request.Content = JsonContent.Create(body);
        using var response = await client.SendAsync(request, token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        using var stream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
        return await JsonDocument.ParseAsync(stream, cancellationToken: token).ConfigureAwait(false);
    }

    internal static (string response, int promptTokens, int completionTokens, int totalTokens, string model) ParseResponse(JsonElement root, string model)
    {
        if (!root.TryGetProperty("info", out var info) || info.TryGetProperty("error", out _)
            || !info.TryGetProperty("finish", out var finish) || finish.GetString() != "stop"
            || !root.TryGetProperty("parts", out var parts) || parts.ValueKind != JsonValueKind.Array)
            return ("[api error] Invalid or incomplete Kilo response", 0, 0, 0, model);

        var output = new StringBuilder();
        var hasText = false;
        foreach (var part in parts.EnumerateArray())
        {
            var type = part.GetProperty("type").GetString();
            if (type == "tool")
                return ("[api error] Kilo attempted tool use", 0, 0, 0, model);
            if (type == "text")
            {
                output.Append(part.GetProperty("text").GetString());
                hasText = true;
            }
        }
        if (!hasText) return ("[api error] Missing Kilo text response", 0, 0, 0, model);
        var input = 0;
        var completion = 0;
        var total = 0;
        if (info.TryGetProperty("tokens", out var tokens))
        {
            input = tokens.TryGetProperty("input", out var i) ? i.GetInt32() : 0;
            completion = tokens.TryGetProperty("output", out var o) ? o.GetInt32() : 0;
            if (tokens.TryGetProperty("reasoning", out var reasoning)) completion += reasoning.GetInt32();
            if (tokens.TryGetProperty("cache", out var cache))
            {
                if (cache.TryGetProperty("read", out var read)) input += read.GetInt32();
                if (cache.TryGetProperty("write", out var write)) input += write.GetInt32();
            }
            total = tokens.TryGetProperty("total", out var t) ? t.GetInt32() : input + completion;
        }
        return (output.ToString(), input, completion, total, model);
    }
}
