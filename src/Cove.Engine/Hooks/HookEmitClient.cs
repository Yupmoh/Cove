using System.Text;

namespace Cove.Engine.Hooks;

public sealed record HookEmitResult(bool Ok, int StatusCode, string? Body);

public sealed class HookEmitClient
{
    private readonly int _port;
    private static readonly HttpClient HttpClient = new() { Timeout = System.TimeSpan.FromSeconds(5) };

    public HookEmitClient(int port)
    {
        _port = port;
    }

    public async Task<HookEmitResult> EmitAsync(string adapter, string eventName, string payload, string? nookId = null, CancellationToken ct = default)
    {
        try
        {
            using var msg = new HttpRequestMessage(HttpMethod.Post, $"http://127.0.0.1:{_port}/api/adapter/{adapter}/{eventName}");
            msg.Content = new StringContent(payload, Encoding.UTF8, "application/json");
            if (nookId is not null)
                msg.Headers.Add("X-Cove-Nook-Id", nookId);

            var resp = await HttpClient.SendAsync(msg, ct).ConfigureAwait(false);
            var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return new HookEmitResult(resp.IsSuccessStatusCode, (int)resp.StatusCode, body);
        }
        catch (HttpRequestException)
        {
            return new HookEmitResult(false, 0, null);
        }
        catch (TaskCanceledException)
        {
            return new HookEmitResult(false, 0, null);
        }
    }
}
