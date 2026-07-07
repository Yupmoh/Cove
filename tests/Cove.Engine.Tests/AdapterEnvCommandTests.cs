using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Cove.Adapters;
using Cove.Engine;
using Cove.Protocol;
using Xunit;

public class AdapterEnvCommandTests
{
    private static string NewDir() => Path.Combine(Path.GetTempPath(), "cove-envcmd-" + Guid.NewGuid().ToString("N"));

    private static AdapterEnvStore MakeStore(string dir)
    {
        Directory.CreateDirectory(dir);
        return new AdapterEnvStore(dir);
    }

    private static JsonElement MakeParams(string adapter, List<AdapterEnvVar> entries)
    {
        var entriesEl = JsonSerializer.SerializeToElement(entries, CoveJsonContext.Default.ListAdapterEnvVar);
        using var doc = JsonDocument.Parse($$"""{"adapter":"{{adapter}}","entries":{{entriesEl.GetRawText()}}}""");
        return doc.RootElement.Clone();
    }

    private static JsonElement MakeParams(string adapter)
    {
        using var doc = JsonDocument.Parse($$"""{"adapter":"{{adapter}}"}""");
        return doc.RootElement.Clone();
    }

    [Fact]
    public async Task List_ReturnsMaskedSecrets()
    {
        var dir = NewDir();
        try
        {
            var store = MakeStore(dir);
            store.Save("claude-code", new List<AdapterEnvVar> { new("API_KEY", "secret123", true, "id1") });

            var request = new ControlRequest("1", "cove://commands/adapter-env.list", MakeParams("claude-code"));
            var response = await EngineCommandRouter.RouteAsync(request, adapterEnv: store);

            Assert.NotNull(response);
            Assert.True(response!.Ok);
            var entries = response.Data!.Value.GetProperty("entries");
            Assert.Equal(1, entries.GetArrayLength());
            Assert.Equal("****", entries[0].GetProperty("value").GetString());
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public async Task Save_PreservesSecretOnMaskSentinel()
    {
        var dir = NewDir();
        try
        {
            var store = MakeStore(dir);
            store.Save("claude-code", new List<AdapterEnvVar> { new("API_KEY", "real-secret", true, "id1") });

            var incoming = MakeParams("claude-code", new List<AdapterEnvVar> { new("API_KEY", "****", true, "id1") });
            var request = new ControlRequest("1", "cove://commands/adapter-env.save", incoming);
            var response = await EngineCommandRouter.RouteAsync(request, adapterEnv: store);

            Assert.NotNull(response);
            Assert.True(response!.Ok);
            var loaded = store.Load("claude-code");
            Assert.Equal("real-secret", loaded[0].Value);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public async Task Save_InvalidAdapter_ReturnsInvalidParams()
    {
        var dir = NewDir();
        try
        {
            var store = MakeStore(dir);
            var incoming = MakeParams("../../etc", new List<AdapterEnvVar>());
            var request = new ControlRequest("1", "cove://commands/adapter-env.save", incoming);
            var response = await EngineCommandRouter.RouteAsync(request, adapterEnv: store);

            Assert.NotNull(response);
            Assert.False(response!.Ok);
            Assert.Equal("invalid_params", response.Error!.Code);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public async Task Resolve_AppliesPrecedence()
    {
        var dir = NewDir();
        try
        {
            var store = MakeStore(dir);
            store.Save("claude-code", new List<AdapterEnvVar> { new("MY_VAR", "adapter-value") });

            var request = new ControlRequest("1", "cove://commands/adapter-env.resolve", MakeParams("claude-code"));
            var response = await EngineCommandRouter.RouteAsync(request, adapterEnv: store);

            Assert.NotNull(response);
            Assert.True(response!.Ok);
            var vars = response.Data!.Value.GetProperty("vars");
            var myVar = vars.EnumerateArray().First(v => v.GetProperty("key").GetString() == "MY_VAR");
            Assert.Equal("adapter-value", myVar.GetProperty("value").GetString());
            var cove = vars.EnumerateArray().First(v => v.GetProperty("key").GetString() == "COVE");
            Assert.Equal("1", cove.GetProperty("value").GetString());
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public async Task List_WithoutStore_ReturnsNotReady()
    {
        var request = new ControlRequest("1", "cove://commands/adapter-env.list");
        var response = await EngineCommandRouter.RouteAsync(request);

        Assert.NotNull(response);
        Assert.False(response!.Ok);
        Assert.Equal("not_ready", response.Error!.Code);
    }
}
