using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Cove.Adapters;
using Cove.Engine;
using Cove.Protocol;
using Xunit;

public class LaunchProfileCommandTests
{
    private static string NewDir() => Path.Combine(Path.GetTempPath(), "cove-launchcmd-" + Guid.NewGuid().ToString("N"));

    private static LaunchProfileStore MakeStore(string dir)
    {
        Directory.CreateDirectory(dir);
        return new LaunchProfileStore(dir);
    }

    [Fact]
    public async Task List_ReturnsProfilesForAdapter()
    {
        var dir = NewDir();
        try
        {
            var store = MakeStore(dir);
            store.Save(new LaunchProfile("Default", "default", "claude-code", true, "opus", "high", Array.Empty<string>(), new Dictionary<string, string>(), new Dictionary<string, bool>(), Array.Empty<string>(), null, 1));
            store.Save(new LaunchProfile("Fast", "fast", "claude-code", false, null, null, Array.Empty<string>(), new Dictionary<string, string>(), new Dictionary<string, bool>(), Array.Empty<string>(), null, 1));

            var prm = JsonSerializer.SerializeToElement(new { adapter = "claude-code" });
            var request = new ControlRequest("1", "cove://commands/launch-profile.list", prm);
            var response = await EngineCommandRouter.RouteAsync(request, launchProfiles: store);

            Assert.NotNull(response);
            Assert.True(response!.Ok);
            var profiles = response.Data!.Value.GetProperty("profiles");
            Assert.Equal(2, profiles.GetArrayLength());
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public async Task SetDefault_PromotesNewDemotesOld()
    {
        var dir = NewDir();
        try
        {
            var store = MakeStore(dir);
            store.Save(new LaunchProfile("Default", "default", "claude-code", true, null, null, Array.Empty<string>(), new Dictionary<string, string>(), new Dictionary<string, bool>(), Array.Empty<string>(), null, 1));
            store.Save(new LaunchProfile("Fast", "fast", "claude-code", false, null, null, Array.Empty<string>(), new Dictionary<string, string>(), new Dictionary<string, bool>(), Array.Empty<string>(), null, 1));

            var prm = JsonSerializer.SerializeToElement(new { adapter = "claude-code", slug = "fast" });
            var request = new ControlRequest("1", "cove://commands/launch-profile.set-default", prm);
            var response = await EngineCommandRouter.RouteAsync(request, launchProfiles: store);

            Assert.NotNull(response);
            Assert.True(response!.Ok);
            var profiles = store.List("claude-code");
            Assert.False(profiles.First(p => p.Slug == "default").IsDefault);
            Assert.True(profiles.First(p => p.Slug == "fast").IsDefault);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public async Task Delete_RemovesProfile_PromotesSurvivor()
    {
        var dir = NewDir();
        try
        {
            var store = MakeStore(dir);
            store.Save(new LaunchProfile("Default", "default", "claude-code", true, null, null, Array.Empty<string>(), new Dictionary<string, string>(), new Dictionary<string, bool>(), Array.Empty<string>(), null, 1));
            store.Save(new LaunchProfile("Fast", "fast", "claude-code", false, null, null, Array.Empty<string>(), new Dictionary<string, string>(), new Dictionary<string, bool>(), Array.Empty<string>(), null, 1));

            var prm = JsonSerializer.SerializeToElement(new { adapter = "claude-code", slug = "default" });
            var request = new ControlRequest("1", "cove://commands/launch-profile.delete", prm);
            var response = await EngineCommandRouter.RouteAsync(request, launchProfiles: store);

            Assert.NotNull(response);
            Assert.True(response!.Ok);
            var profiles = store.List("claude-code");
            Assert.Single(profiles);
            Assert.True(profiles[0].IsDefault);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public async Task List_WithoutStore_ReturnsNotReady()
    {
        var request = new ControlRequest("1", "cove://commands/launch-profile.list");
        var response = await EngineCommandRouter.RouteAsync(request);

        Assert.NotNull(response);
        Assert.False(response!.Ok);
        Assert.Equal("not_ready", response.Error!.Code);
    }

    [Fact]
    public async Task SetDefault_MissingParams_ReturnsInvalidParams()
    {
        var dir = NewDir();
        try
        {
            var store = MakeStore(dir);
            var request = new ControlRequest("1", "cove://commands/launch-profile.set-default");
            var response = await EngineCommandRouter.RouteAsync(request, launchProfiles: store);

            Assert.NotNull(response);
            Assert.False(response!.Ok);
            Assert.Equal("invalid_params", response.Error!.Code);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }
}
