using System.Text.Json;
using Cove.Adapters;
using Xunit;

namespace Cove.Adapters.Tests;

public sealed class RegistryServiceTests
{
    private static readonly RegistryEntry SampleEntry = new()
    {
        Name = "claude-code",
        DisplayName = "Claude Code",
        Description = "test",
        Accent = "#D97757",
        Binary = "claude",
        SdkVersion = 2,
        Version = "1.0.0",
        Official = true,
    };

    [Fact]
    public void Parse_RoundTripsRegistryJson()
    {
        var json = """
        {
          "schemaVersion": 1,
          "adapters": [
            {
              "name": "claude-code",
              "displayName": "Claude Code",
              "description": "test",
              "accent": "#D97757",
              "binary": "claude",
              "sdkVersion": 2,
              "version": "1.0.0",
              "official": true
            }
          ]
        }
        """;
        var registry = RegistryService.ParseRegistry(json);

        Assert.NotNull(registry);
        Assert.Equal(1, registry!.SchemaVersion);
        Assert.Single(registry.Adapters);
        Assert.Equal("claude-code", registry.Adapters[0].Name);
    }

    [Fact]
    public void ParityCheck_MatchingVersions_Passes()
    {
        var result = RegistryService.CheckParity(SampleEntry, "1.0.0");
        Assert.True(result.ParityOk);
        Assert.False(result.UpdateAvailable);
    }

    [Fact]
    public void ParityCheck_MismatchedVersions_Fails()
    {
        var result = RegistryService.CheckParity(SampleEntry, "0.9.0");
        Assert.False(result.ParityOk);
        Assert.True(result.UpdateAvailable);
    }

    [Fact]
    public void ParityCheck_RegistryHigherVersion_UpdateAvailable()
    {
        var registryEntry = SampleEntry with { Version = "2.0.0" };
        var result = RegistryService.CheckParity(registryEntry, "1.0.0");
        Assert.False(result.ParityOk);
        Assert.True(result.UpdateAvailable);
    }

    [Fact]
    public void ParityCheck_InstalledHigherVersion_NoUpdate()
    {
        var registryEntry = SampleEntry with { Version = "0.5.0" };
        var result = RegistryService.CheckParity(registryEntry, "1.0.0");
        Assert.False(result.ParityOk);
        Assert.False(result.UpdateAvailable);
    }

    [Fact]
    public void ParityCheck_DriftWarning_WhenVersionsDiffer()
    {
        var result = RegistryService.CheckParity(SampleEntry, "0.9.0");
        Assert.NotNull(result.DriftWarning);
        Assert.Contains("0.9.0", result.DriftWarning!);
        Assert.Contains("1.0.0", result.DriftWarning!);
    }

    [Fact]
    public void MinAppVersion_Respected()
    {
        var entry = SampleEntry with { Version = "1.0.0", MinAppVersion = "2.0.0" };
        var result = RegistryService.CheckParity(entry, "0.9.0", appVersion: "1.0.0");
        Assert.False(result.Compatible);
    }

    [Fact]
    public void MinAppVersion_Null_IsCompatible()
    {
        var result = RegistryService.CheckParity(SampleEntry, "1.0.0", appVersion: "1.0.0");
        Assert.True(result.Compatible);
    }

    [Fact]
    public async Task FetchAsync_CachesToDisk_OfflineFallback()
    {
        var dir = Path.Combine(Path.GetTempPath(), "cove-registry-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var fetcher = new TestFetcher("""{"schemaVersion":1,"adapters":[{"name":"x","displayName":"X","description":"","accent":"#000000","binary":"x","sdkVersion":2,"version":"1.0.0","official":false}]}""");
            var svc = new RegistryService(Path.Combine(dir, "registry-cache.json"), fetcher);

            var first = await svc.FetchAsync();
            Assert.NotNull(first);
            Assert.Single(first!.Adapters);

            var second = await svc.FetchAsync();
            Assert.NotNull(second);
            Assert.Single(second!.Adapters);

            Assert.Equal(1, fetcher.CallCount);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public async Task FetchAsync_OfflineFallsBackToDiskCache()
    {
        var dir = Path.Combine(Path.GetTempPath(), "cove-registry-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var fetcher = new TestFetcher("""{"schemaVersion":1,"adapters":[{"name":"x","displayName":"X","description":"","accent":"#000000","binary":"x","sdkVersion":2,"version":"1.0.0","official":false}]}""");
            var svc = new RegistryService(Path.Combine(dir, "registry-cache.json"), fetcher);
            await svc.FetchAsync();

            var offlineFetcher = new TestFetcher(throwOnFetch: true);
            var svcOffline = new RegistryService(Path.Combine(dir, "registry-cache.json"), offlineFetcher);
            var result = await svcOffline.FetchAsync();

            Assert.NotNull(result);
            Assert.Single(result!.Adapters);
            Assert.Equal("x", result.Adapters[0].Name);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }
}

internal sealed class TestFetcher : IRegistryFetcher
{
    private readonly string _json;
    public int CallCount;
    public bool ThrowOnFetch;

    public TestFetcher(string json = "", bool throwOnFetch = false)
    {
        _json = json;
        ThrowOnFetch = throwOnFetch;
    }

    public Task<string?> FetchAsync(CancellationToken ct = default)
    {
        if (ThrowOnFetch)
            throw new HttpRequestException("offline");
        CallCount++;
        return Task.FromResult<string?>(_json);
    }
}
