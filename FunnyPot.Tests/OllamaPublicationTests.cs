using System.Text.Json;
using FunnyPot;
using Xunit;

public class OllamaPublicationTests : IDisposable
{
    readonly string dir = Path.Combine(Path.GetTempPath(), "ollama-publication-" + Guid.NewGuid().ToString("N"));
    public OllamaPublicationTests() => Directory.CreateDirectory(dir);
    public void Dispose() => Directory.Delete(dir, true);

    [Fact]
    public void ExportCorrelatesRotatedCapturesAndIsIdempotent()
    {
        var raw = Path.Combine(dir, "connections.jsonl");
        var target = Path.Combine(dir, "ollama.json");
        var now = DateTimeOffset.UtcNow;
        File.WriteAllText(raw + ".1", Line(now, new { type = "connection", id = "a", peer = "203.0.113.1:1234" }));
        File.WriteAllText(raw, Line(now.AddSeconds(1), new { type = "request", id = "a", method = "POST", path = "/api/chat", body = new string('x', 5000), status = 200, error = "", userAgent = "test-agent" })
            + Line(now.AddSeconds(2), new { type = "connection", id = "b", peer = "203.0.113.2:1234" })
            + Line(now.AddSeconds(3), new { type = "rejected", id = "b", status = 429 }) + "{partial");
        OllamaPublication.Export(raw, target);
        var first = File.ReadAllText(target);
        OllamaPublication.Export(raw, target);
        Assert.Equal(first, File.ReadAllText(target));
        var document = OllamaPublication.Read(target);
        Assert.Equal(2, document.Connections.Count);
        var request = document.Connections.Single(c => c.Id == "a");
        Assert.Equal("203.0.113.1:1234", request.Peer);
        Assert.Equal(4096, request.Body!.Length);
        Assert.True(request.BodyTruncated);
        Assert.Equal(OllamaHoneypot.Reply, request.Response);
        Assert.Equal(429, document.Connections.Single(c => c.Id == "b").Status);
        Assert.Equal(now.AddSeconds(3), document.LastUpdated);
        Assert.Equal(0, document.UpstreamTokens);
    }

    [Fact]
    public void MergePreservesRemoteAndSnapshotRowsAndPrefersLatestUpdate()
    {
        var target = Path.Combine(dir, "target.json");
        var snapshot = Path.Combine(dir, "snapshot.json");
        var now = DateTimeOffset.UtcNow;
        File.WriteAllText(target, JsonSerializer.Serialize(new OllamaPublication.Document { Connections = new()
        {
            new() { Id = "remote", Peer = "203.0.113.1:1234", Timestamp = now, UpdatedAt = now },
            new() { Id = "shared", Peer = "203.0.113.1:1234", Timestamp = now, UpdatedAt = now.AddSeconds(2), Status = 200 }
        }}));
        File.WriteAllText(snapshot, JsonSerializer.Serialize(new OllamaPublication.Document { Connections = new()
        {
            new() { Id = "local", Peer = "203.0.113.1:1234", Timestamp = now, UpdatedAt = now },
            new() { Id = "shared", Peer = "203.0.113.1:1234", Timestamp = now, UpdatedAt = now, Status = null }
        }}));
        OllamaPublication.MergeFiles(target, snapshot);
        var rows = OllamaPublication.Read(target).Connections;
        Assert.Equal(3, rows.Count);
        Assert.Equal(200, rows.Single(c => c.Id == "shared").Status);
        OllamaPublication.Export(Path.Combine(dir, "absent-raw-log"), target);
        Assert.Equal(3, OllamaPublication.Read(target).Connections.Count);
    }

    [Fact]
    public void PublicationIsBoundedAndNewestConnectionsWin()
    {
        var raw = Path.Combine(dir, "connections.jsonl");
        var target = Path.Combine(dir, "ollama.json");
        var now = DateTimeOffset.UtcNow;
        File.WriteAllLines(raw, Enumerable.Range(0, 3100).Select(i => Line(now.AddSeconds(i),
            new { type = "connection", id = i.ToString(), peer = "203.0.113.1:1234" }).TrimEnd()));
        OllamaPublication.Export(raw, target);
        var rows = OllamaPublication.Read(target).Connections;
        Assert.Equal(OllamaPublication.Limit, rows.Count);
        Assert.Equal("3099", rows[0].Id);
        Assert.DoesNotContain(rows, c => c.Id == "0");
    }

    [Fact]
    public void ExportFiltersLocalAndPrivatePeers()
    {
        var raw = Path.Combine(dir, "connections.jsonl");
        var target = Path.Combine(dir, "ollama.json");
        var now = DateTimeOffset.UtcNow;
        File.WriteAllText(raw,
            Line(now, new { type = "request", id = "orphan", method = "POST", path = "/api/chat", body = "private", status = 200, error = "" }) +
            Line(now, new { type = "connection", id = "invalid", peer = "unknown" }) +
            Line(now, new { type = "connection", id = "loopback", peer = "127.0.0.1:1234" }) +
            Line(now.AddSeconds(1), new { type = "request", id = "loopback", method = "GET", path = "/api/version", body = "", status = 200, error = "" }) +
            Line(now.AddSeconds(2), new { type = "connection", id = "docker", peer = "172.18.0.1:5678" }) +
            Line(now.AddSeconds(3), new { type = "request", id = "docker", method = "GET", path = "/api/tags", body = "", status = 200, error = "" }) +
            Line(now.AddSeconds(4), new { type = "connection", id = "external", peer = "45.112.149.170:39682" }) +
            Line(now.AddSeconds(5), new { type = "request", id = "external", method = "POST", path = "/v1/chat/completions", body = "{\"prompt\":\"hi\"}", response = "ROUTER_OK 323", status = 200, error = "" }));
        OllamaPublication.Export(raw, target);
        var rows = OllamaPublication.Read(target).Connections;
        var single = Assert.Single(rows);
        Assert.Equal("external", single.Id);
        Assert.Equal("ROUTER_OK 323", single.Response);
    }

    static string Line(DateTimeOffset timestamp, object data) => JsonSerializer.Serialize(new { timestamp, data }) + "\n";
}
