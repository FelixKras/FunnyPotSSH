using System.Text.Json;
using Xunit;

namespace FunnyPot.Tests;

public class SubscriptionBridgeAdapterTests
{
    [Fact]
    public async Task RequestHasKnownLengthAndCorrectMessageNames()
    {
        using var content = SubscriptionBridgeAdapter.CreateRequestContent(new()
        {
            new() { Role = "user", Content = "uname -s" }
        });
        Assert.True(content.Headers.ContentLength > 0);
        using var json = JsonDocument.Parse(await content.ReadAsStringAsync());
        Assert.Equal("user", json.RootElement.GetProperty("messages")[0].GetProperty("role").GetString());
        Assert.Equal("uname -s", json.RootElement.GetProperty("messages")[0].GetProperty("content").GetString());
    }

    [Fact]
    public void ParsesTextAndUsage()
    {
        using var json = JsonDocument.Parse("""
            {"text":"Linux\n","model":"openai/gpt-6.1-terra",
            "usage":{"input_tokens":12,"output_tokens":3,"total_tokens":15}}
            """);
        var result = SubscriptionBridgeAdapter.ParseResponse(json.RootElement);
        Assert.Equal("Linux\n", result.response);
        Assert.Equal(12, result.promptTokens);
        Assert.Equal(3, result.completionTokens);
        Assert.Equal(15, result.totalTokens);
        Assert.Equal("openai/gpt-6.1-terra", result.model);
    }

    [Fact]
    public void PreservesEmptyTerminalOutput()
    {
        using var json = JsonDocument.Parse("""
            {"text":"","model":"openai/gpt-6.1-terra",
            "usage":{"input_tokens":0,"output_tokens":0,"total_tokens":0}}
            """);
        Assert.Equal("", SubscriptionBridgeAdapter.ParseResponse(json.RootElement).response);
    }

    [Fact]
    public async Task CancelledRequestReturnsNonLearnableError()
    {
        using var cancel = new CancellationTokenSource();
        cancel.Cancel();
        var result = await SubscriptionBridgeAdapter.GetResponseAsync(new(), 30, cancel.Token);
        Assert.StartsWith("[api error]", result.response);
    }
}
