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
        finally { Cove.Testing.TestDirectory.Delete(dir); }
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
        finally { Cove.Testing.TestDirectory.Delete(dir); }
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
        finally { Cove.Testing.TestDirectory.Delete(dir); }
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
        finally { Cove.Testing.TestDirectory.Delete(dir); }
    }

    [Fact]
    public async Task Get_ReturnsFullProfile()
    {
        var dir = NewDir();
        try
        {
            var store = MakeStore(dir);
            store.Save(new LaunchProfile("GLM Umans", "glm-umans", "claude-code", true, "glm", "high",
                new[] { "--settings" }, new Dictionary<string, string> { ["ANTHROPIC_BASE_URL"] = "https://umans.ai" },
                new Dictionary<string, bool>(), Array.Empty<string>(), null, 1));

            var prm = JsonSerializer.SerializeToElement(new { adapter = "claude-code", slug = "glm-umans" });
            var request = new ControlRequest("1", "cove://commands/launch-profile.get", prm);
            var response = await EngineCommandRouter.RouteAsync(request, launchProfiles: store);

            Assert.NotNull(response);
            Assert.True(response!.Ok);
            Assert.Equal("glm-umans", response.Data!.Value.GetProperty("slug").GetString());
            Assert.Equal("glm", response.Data!.Value.GetProperty("model").GetString());
            Assert.Equal("https://umans.ai", response.Data!.Value.GetProperty("env").GetProperty("ANTHROPIC_BASE_URL").GetString());
        }
        finally { Cove.Testing.TestDirectory.Delete(dir); }
    }

    [Fact]
    public async Task Get_UnknownProfile_ReturnsNotFound()
    {
        var dir = NewDir();
        try
        {
            var store = MakeStore(dir);
            var prm = JsonSerializer.SerializeToElement(new { adapter = "claude-code", slug = "missing" });
            var request = new ControlRequest("1", "cove://commands/launch-profile.get", prm);
            var response = await EngineCommandRouter.RouteAsync(request, launchProfiles: store);

            Assert.NotNull(response);
            Assert.False(response!.Ok);
            Assert.Equal("not_found", response.Error!.Code);
        }
        finally { Cove.Testing.TestDirectory.Delete(dir); }
    }

    [Fact]
    public async Task Create_PersistsProfile_AndFirstProfileBecomesDefault()
    {
        var dir = NewDir();
        try
        {
            var store = MakeStore(dir);
            var prm = JsonSerializer.SerializeToElement(new
            {
                adapter = "claude-code",
                slug = "glm-umans",
                name = "GLM Umans",
                model = "glm",
                env = new Dictionary<string, string> { ["ANTHROPIC_BASE_URL"] = "https://umans.ai" },
            });
            var request = new ControlRequest("1", "cove://commands/launch-profile.create", prm);
            var response = await EngineCommandRouter.RouteAsync(request, launchProfiles: store);

            Assert.NotNull(response);
            Assert.True(response!.Ok);
            var created = store.List("claude-code").Single(p => p.Slug == "glm-umans");
            Assert.Equal("GLM Umans", created.Name);
            Assert.Equal("glm", created.Model);
            Assert.Equal("https://umans.ai", created.Env["ANTHROPIC_BASE_URL"]);
            Assert.True(created.IsDefault);
        }
        finally { Cove.Testing.TestDirectory.Delete(dir); }
    }

    [Fact]
    public async Task Create_DuplicateSlug_ReturnsConflict()
    {
        var dir = NewDir();
        try
        {
            var store = MakeStore(dir);
            store.Save(new LaunchProfile("Existing", "glm-umans", "claude-code", true, null, null,
                Array.Empty<string>(), new Dictionary<string, string>(), new Dictionary<string, bool>(), Array.Empty<string>(), null, 1));

            var prm = JsonSerializer.SerializeToElement(new { adapter = "claude-code", slug = "glm-umans", name = "Dup" });
            var request = new ControlRequest("1", "cove://commands/launch-profile.create", prm);
            var response = await EngineCommandRouter.RouteAsync(request, launchProfiles: store);

            Assert.NotNull(response);
            Assert.False(response!.Ok);
            Assert.Equal("conflict", response.Error!.Code);
        }
        finally { Cove.Testing.TestDirectory.Delete(dir); }
    }

    [Fact]
    public async Task Update_MergesFields_PreservesDefault()
    {
        var dir = NewDir();
        try
        {
            var store = MakeStore(dir);
            store.Save(new LaunchProfile("GLM", "glm-umans", "claude-code", true, "glm", "high",
                Array.Empty<string>(), new Dictionary<string, string>(), new Dictionary<string, bool>(), Array.Empty<string>(), null, 1));

            var prm = JsonSerializer.SerializeToElement(new
            {
                adapter = "claude-code",
                slug = "glm-umans",
                model = "glm-4.6",
                effort = "max",
                env = new Dictionary<string, string> { ["ANTHROPIC_BASE_URL"] = "https://umans.ai/v2" },
            });
            var request = new ControlRequest("1", "cove://commands/launch-profile.update", prm);
            var response = await EngineCommandRouter.RouteAsync(request, launchProfiles: store);

            Assert.NotNull(response);
            Assert.True(response!.Ok);
            var updated = store.List("claude-code").Single(p => p.Slug == "glm-umans");
            Assert.Equal("glm-4.6", updated.Model);
            Assert.Equal("max", updated.Effort);
            Assert.Equal("https://umans.ai/v2", updated.Env["ANTHROPIC_BASE_URL"]);
            Assert.True(updated.IsDefault);
        }
        finally { Cove.Testing.TestDirectory.Delete(dir); }
    }

    [Fact]
    public async Task Update_UnknownProfile_ReturnsNotFound()
    {
        var dir = NewDir();
        try
        {
            var store = MakeStore(dir);
            var prm = JsonSerializer.SerializeToElement(new { adapter = "claude-code", slug = "missing", name = "x" });
            var request = new ControlRequest("1", "cove://commands/launch-profile.update", prm);
            var response = await EngineCommandRouter.RouteAsync(request, launchProfiles: store);

            Assert.NotNull(response);
            Assert.False(response!.Ok);
            Assert.Equal("not_found", response.Error!.Code);
        }
        finally { Cove.Testing.TestDirectory.Delete(dir); }
    }

    [Fact]
    public async Task Create_InvalidSlug_ReturnsInvalidParams()
    {
        var dir = NewDir();
        try
        {
            var store = MakeStore(dir);
            var prm = JsonSerializer.SerializeToElement(new { adapter = "claude-code", slug = "Bad Slug!", name = "x" });
            var request = new ControlRequest("1", "cove://commands/launch-profile.create", prm);
            var response = await EngineCommandRouter.RouteAsync(request, launchProfiles: store);

            Assert.NotNull(response);
            Assert.False(response!.Ok);
            Assert.Equal("invalid_params", response.Error!.Code);
        }
        finally { Cove.Testing.TestDirectory.Delete(dir); }
    }
}
