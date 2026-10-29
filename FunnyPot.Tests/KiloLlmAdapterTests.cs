using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

namespace FunnyPot.Tests;

public class KiloLlmAdapterTests
{
    const string Success = """
        {"info":{"finish":"stop","tokens":{"input":12,"output":3}},"parts":[
          {"type":"reasoning","text":"private reasoning"},
          {"type":"text","text":"Linux"},{"type":"text","text":"\n"}]}
        """;

    [Fact]
    public void ParseResponse_UsesOnlyTextAndReportsTokens()
    {
        using var json = JsonDocument.Parse(Success);
        var result = KiloLlmAdapter.ParseResponse(json.RootElement, "openai/test");
        Assert.Equal("Linux\n", result.response);
        Assert.Equal(12, result.promptTokens);
        Assert.Equal(3, result.completionTokens);
        Assert.Equal(15, result.totalTokens);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"info\":{\"error\":{\"name\":\"APIError\"}},\"parts\":[]}")]
    [InlineData("{\"info\":{\"finish\":\"length\"},\"parts\":[{\"type\":\"text\",\"text\":\"partial\"}]}")]
    [InlineData("{\"info\":{\"finish\":\"stop\"},\"parts\":[{\"type\":\"tool\"}]}")]
    [InlineData("{\"info\":{\"finish\":\"stop\"},\"parts\":[]}")]
    public void ParseResponse_RejectsFailuresAndToolUse(string body)
    {
        using var json = JsonDocument.Parse(body);
        Assert.StartsWith("[api error]", KiloLlmAdapter.ParseResponse(json.RootElement, "openai/test").response);
    }

    [Fact]
    public void ParseResponse_PreservesValidEmptyText()
    {
        using var json = JsonDocument.Parse("""{"info":{"finish":"stop"},"parts":[{"type":"text","text":""}]}""");
        Assert.Equal("", KiloLlmAdapter.ParseResponse(json.RootElement, "openai/test").response);
    }

    [Fact]
    public void ParseResponse_CountsReasoningAndCachedTokens()
    {
        using var json = JsonDocument.Parse("""
            {"info":{"finish":"stop","tokens":{"input":10,"output":2,"reasoning":5,
            "total":117,"cache":{"read":100,"write":0}}},"parts":[{"type":"text","text":"ok"}]}
            """);
        var result = KiloLlmAdapter.ParseResponse(json.RootElement, "openai/test");
        Assert.Equal(110, result.promptTokens);
        Assert.Equal(7, result.completionTokens);
        Assert.Equal(117, result.totalTokens);
    }

    [Fact]
    public async Task Request_DeniesToolsSendsHistoryAndCleansUp()
    {
        var handler = new FakeKiloHandler(Success);
        using var client = new HttpClient(handler);
        var adapter = new KiloLlmAdapter(client, new KiloConfig());
        var result = await adapter.GetResponseAsync(History(), 10, default);
        Assert.Equal("Linux\n", result.response);
        Assert.Equal(new[] { "POST /session", "POST /session/ses_test/message", "POST /session/ses_test/abort", "DELETE /session/ses_test" }, handler.Calls);
        using var create = JsonDocument.Parse(handler.Bodies[0]);
        var deny = create.RootElement.GetProperty("permission")[0];
        Assert.Equal("*", deny.GetProperty("permission").GetString());
        Assert.Equal("deny", deny.GetProperty("action").GetString());
        using var prompt = JsonDocument.Parse(handler.Bodies[1]);
        var root = prompt.RootElement;
        Assert.False(root.GetProperty("tools").GetProperty("*").GetBoolean());
        Assert.Equal("funnypot-terminal", root.GetProperty("agent").GetString());
        Assert.Equal("gpt-6.1-terra", root.GetProperty("model").GetProperty("modelID").GetString());
        Assert.Contains("terminal persona", root.GetProperty("system").GetString());
        using var transcript = JsonDocument.Parse(root.GetProperty("parts")[0].GetProperty("text").GetString()!);
        Assert.Equal(3, transcript.RootElement.GetArrayLength());
        Assert.Equal("uname -s", transcript.RootElement[2].GetProperty("content").GetString());
    }

    [Theory]
    [InlineData("not json", false)]
    [InlineData("{\"info\":{\"finish\":\"stop\"},\"parts\":[{}]}", false)]
    [InlineData("sensitive upstream error", true)]
    public async Task FailedPrompt_IsSanitizedAndSessionStillDeleted(string reply, bool fail)
    {
        var handler = new FakeKiloHandler(reply) { FailPrompt = fail, FailAbort = true };
        using var client = new HttpClient(handler);
        var result = await new KiloLlmAdapter(client, new KiloConfig()).GetResponseAsync(History(), 10, default);
        Assert.StartsWith("[api error]", result.response);
        Assert.DoesNotContain("sensitive", result.response);
        Assert.Contains("DELETE /session/ses_test", handler.Calls);
    }

    [Fact]
    public async Task CancelledRequest_DoesNotCreateSession()
    {
        var handler = new FakeKiloHandler(Success);
        using var client = new HttpClient(handler);
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var result = await new KiloLlmAdapter(client, new KiloConfig()).GetResponseAsync(History(), 10, cts.Token);
        Assert.StartsWith("[api error]", result.response);
        Assert.Empty(handler.Calls);
    }

    [Fact]
    public void Configuration_HasSeparateKiloModel()
    {
        var config = AppConfiguration.Load(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../config/app-config.yaml")));
        Assert.Equal("openrouter", config.Llm.Provider);
        Assert.Equal("openai/gpt-6.1-terra", config.Llm.Kilo.Model);
        Assert.Equal(2, config.Llm.Kilo.MaxConcurrentRequests);
    }

    static List<ChatRequestData.ChatMessage> History() => new()
    {
        new() { Role = "system", Content = "terminal persona" },
        new() { Role = "user", Content = "pwd" },
        new() { Role = "assistant", Content = "/root" },
        new() { Role = "user", Content = "uname -s" }
    };

    sealed class FakeKiloHandler(string reply) : HttpMessageHandler
    {
        public bool FailPrompt { get; init; }
        public bool FailAbort { get; init; }
        public List<string> Calls { get; } = new();
        public List<string> Bodies { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            Calls.Add(request.Method + " " + path);
            Assert.Contains("directory=", request.RequestUri.Query);
            Bodies.Add(request.Content == null ? "" : await request.Content.ReadAsStringAsync(cancellationToken));
            var isPrompt = path.EndsWith("/message");
            var status = (isPrompt && FailPrompt) || (path.EndsWith("/abort") && FailAbort)
                ? HttpStatusCode.InternalServerError : HttpStatusCode.OK;
            var body = path == "/session" ? "{\"id\":\"ses_test\"}" : isPrompt ? reply : "true";
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }
}
