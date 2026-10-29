using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace FunnyPot;

internal sealed class SubscriptionBridgeAdapter
{
    static readonly HttpClient Client = CreateClient();

    static HttpClient CreateClient()
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseProxy = false,
            ConnectCallback = async (_, token) =>
            {
                var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                try
                {
                    var path = Environment.GetEnvironmentVariable("SUBSCRIPTION_BRIDGE_SOCKET") ?? "/run/funnypot-subscription/socket/bridge.sock";
                    await socket.ConnectAsync(new UnixDomainSocketEndPoint(path), token).ConfigureAwait(false);
                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch { socket.Dispose(); throw; }
            }
        };
        return new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
    }

    internal static async Task<(string response, int promptTokens, int completionTokens, int totalTokens, string model)> GetResponseAsync(
        List<ChatRequestData.ChatMessage> history, int timeoutSeconds, CancellationToken cancellationToken)
    {
        const string model = "openai/gpt-6.1-terra";
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(timeoutSeconds, 1, 600)));
        try
        {
            using var content = CreateRequestContent(history);
            using var response = await Client.PostAsync("http://subscription-bridge/v1/terminal", content, timeout.Token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            using var stream = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
            using var json = await JsonDocument.ParseAsync(stream, cancellationToken: timeout.Token).ConfigureAwait(false);
            return ParseResponse(json.RootElement);
        }
        catch (OperationCanceledException) { return ("[api error] Subscription bridge timed out or cancelled", 0, 0, 0, model); }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidOperationException or KeyNotFoundException or FormatException or OverflowException)
        { return ("[api error] Subscription bridge unavailable", 0, 0, 0, model); }
    }

    // Known Content-Length avoids chunked encoding, which the narrow bridge rejects.
    internal static HttpContent CreateRequestContent(List<ChatRequestData.ChatMessage> history) =>
        new StringContent(JsonSerializer.Serialize(new { messages = history }), Encoding.UTF8, "application/json");

    internal static (string response, int promptTokens, int completionTokens, int totalTokens, string model) ParseResponse(JsonElement root)
    {
        var usage = root.GetProperty("usage");
        return (root.GetProperty("text").GetString() ?? throw new JsonException("Missing text"),
            usage.GetProperty("input_tokens").GetInt32(), usage.GetProperty("output_tokens").GetInt32(),
            usage.GetProperty("total_tokens").GetInt32(), root.GetProperty("model").GetString() ?? throw new JsonException("Missing model"));
    }
}
