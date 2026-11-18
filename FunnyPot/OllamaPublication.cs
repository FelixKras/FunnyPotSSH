using System.Net;
using System.Text.Json;

namespace FunnyPot;

internal static class OllamaPublication
{
    internal const int Limit = 1000;
    internal sealed class Connection
    {
        public string Id { get; set; } = "";
        public DateTimeOffset Timestamp { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public string? Peer { get; set; }
        public string? Method { get; set; }
        public string? Path { get; set; }
        public string? UserAgent { get; set; }
        public string? Body { get; set; }
        public bool BodyTruncated { get; set; }
        public int? Status { get; set; }
        public string? Error { get; set; }
        public string? Response { get; set; }
    }

    internal sealed class Document
    {
        public int SchemaVersion { get; set; } = 1;
        public DateTimeOffset? LastUpdated { get; set; }
        public int RetentionLimit { get; set; } = Limit;
        public int UpstreamTokens { get; set; } = 0;
        public List<Connection> Connections { get; set; } = new();
    }

    internal static Document Read(string path)
    {
        if (!File.Exists(path)) return new();
        try { return JsonSerializer.Deserialize<Document>(File.ReadAllText(path)) ?? new(); }
        catch (JsonException) { return new(); }
    }

    static bool IsPublishablePeer(string? peer)
    {
        if (peer == null || !IPEndPoint.TryParse(peer, out var endpoint)) return false;
        var ip = endpoint.Address;
        if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
        if (IPAddress.IsLoopback(ip)) return false;
        var bytes = ip.GetAddressBytes();
        if (bytes.Length == 16)
            return !ip.Equals(IPAddress.IPv6Any) && !ip.IsIPv6LinkLocal && !ip.IsIPv6SiteLocal &&
                !ip.IsIPv6Multicast && (bytes[0] & 0xfe) != 0xfc;
        return !(bytes[0] is 0 or 10 or 127 or >= 224 ||
            (bytes[0] == 169 && bytes[1] == 254) ||
            (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) ||
            (bytes[0] == 192 && bytes[1] == 168));
    }

    static List<Connection> Retain(IEnumerable<Connection> connections) => connections
        .Where(c => !string.IsNullOrEmpty(c.Id) && IsPublishablePeer(c.Peer)).GroupBy(c => c.Id)
        .Select(group => group.OrderByDescending(c => c.UpdatedAt).ThenByDescending(c => c.Status.HasValue).First())
        .OrderByDescending(c => c.Timestamp).ThenBy(c => c.Id, StringComparer.Ordinal).Take(Limit).ToList();

    static void Save(string path, IEnumerable<Connection> rows)
    {
        var retained = Retain(rows);
        var document = new Document { Connections = retained, LastUpdated = retained.Count == 0 ? null : retained.Max(c => c.UpdatedAt) };
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(document));
        File.Move(temp, path, true);
    }

    internal static void MergeFiles(string target, string snapshot) =>
        Save(target, Read(target).Connections.Concat(Read(snapshot).Connections));

    internal static void Export(string logPath, string target)
    {
        var rows = Read(target).Connections.ToDictionary(c => c.Id, StringComparer.Ordinal);
        // Raw files remain private. Replay one bounded rotated file at a time, never
        // retain the full log stream or copy arbitrary headers into public telemetry.
        for (var archive = 10; archive >= 0; archive--)
        {
            var source = archive == 0 ? logPath : logPath + "." + archive;
            if (!File.Exists(source)) continue;
            try
            {
                using var stream = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream);
                while (reader.ReadLine() is { } line)
                {
                    try
                    {
                        using var parsed = JsonDocument.Parse(line);
                        var root = parsed.RootElement;
                        var data = root.GetProperty("data");
                        var id = data.GetProperty("id").GetString()!;
                        var timestamp = root.GetProperty("timestamp").GetDateTimeOffset();
                        var type = data.GetProperty("type").GetString();
                        if (!rows.TryGetValue(id, out var row))
                            rows[id] = row = new Connection { Id = id, Timestamp = timestamp };
                        if (type == "connection")
                        {
                            row.Timestamp = timestamp;
                            row.Peer = data.GetProperty("peer").GetString();
                        }
                        if (timestamp >= row.UpdatedAt)
                        {
                            row.UpdatedAt = timestamp;
                            if (type == "rejected") row.Status = 429;
                            if (type == "request")
                            {
                                row.Method = data.GetProperty("method").GetString();
                                row.Path = data.GetProperty("path").GetString();
                                row.UserAgent = data.TryGetProperty("userAgent", out var ua) ? ua.GetString() : null;
                                var body = data.GetProperty("body").GetString();
                                row.BodyTruncated = body?.Length > 4096;
                                row.Body = body is { Length: > 4096 } ? body[..4096] : body;
                                row.Status = data.GetProperty("status").GetInt32();
                                row.Error = data.GetProperty("error").GetString();
                                var loggedResponse = data.TryGetProperty("response", out var respProp) && respProp.ValueKind == JsonValueKind.String ? respProp.GetString() : null;
                                row.Response = !string.IsNullOrEmpty(loggedResponse)
                                    ? (loggedResponse.Length > 4096 ? loggedResponse[..4096] : loggedResponse)
                                    : row.Status == 200 && row.Path is "/api/chat" or "/api/generate" or "/v1/chat/completions" or "/v1/completions"
                                        ? OllamaHoneypot.Reply : null;
                            }
                        }
                        if (rows.Count > Limit * 2) rows = Retain(rows.Values).ToDictionary(c => c.Id, StringComparer.Ordinal);
                    }
                    catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException) { /* Partial/malformed capture line. */ }
                }
            }
            catch (FileNotFoundException) { /* Concurrent rotation; the next publish retries. */ }
        }
        Save(target, rows.Values);
    }
}
