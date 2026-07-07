using System.IO;
using System.Text.Json;
using Cove.Adapters;
using Xunit;

namespace Cove.Adapters.Tests;

public sealed class LaunchProfileTests
{
    private static string NewDir() => Path.Combine(Path.GetTempPath(), "cove-launch-" + Guid.NewGuid().ToString("N"));

    private static LaunchProfile MakeProfile(string name, string slug, string adapter, bool isDefault = false, string? model = null, string? effort = null, string[]? cliArgs = null, Dictionary<string, string>? env = null, string? agent = null)
        => new(name, slug, adapter, isDefault, model, effort, cliArgs ?? Array.Empty<string>(), env ?? new Dictionary<string, string>(), new Dictionary<string, bool>(), Array.Empty<string>(), agent, 1);

    [Fact]
    public void Validate_AcceptsValidProfile()
    {
        var profile = MakeProfile("Fast", "fast", "claude-code");
        var errors = LaunchProfileValidator.Validate(profile);
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_RejectsInvalidSlug()
    {
        var profile = MakeProfile("Bad", "Bad_Slug", "claude-code");
        var errors = LaunchProfileValidator.Validate(profile);
        Assert.Contains(errors, e => e.Field == "slug");
    }

    [Fact]
    public void Validate_RejectsMissingAdapter()
    {
        var profile = MakeProfile("Test", "test", "");
        var errors = LaunchProfileValidator.Validate(profile);
        Assert.Contains(errors, e => e.Field == "adapter");
    }

    [Fact]
    public void Store_SaveAndLoad_RoundTrips()
    {
        var dir = NewDir();
        try
        {
            var store = new LaunchProfileStore(dir);
            var profile = MakeProfile("Default", "default", "claude-code", isDefault: true, model: "opus", effort: "high");
            store.Save(profile);

            var loaded = store.Load("claude-code", "default");
            Assert.NotNull(loaded);
            Assert.Equal("Default", loaded!.Name);
            Assert.True(loaded.IsDefault);
            Assert.Equal("opus", loaded.Model);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void Store_List_ReturnsProfilesForAdapter()
    {
        var dir = NewDir();
        try
        {
            var store = new LaunchProfileStore(dir);
            store.Save(MakeProfile("Default", "default", "claude-code", isDefault: true));
            store.Save(MakeProfile("Fast", "fast", "claude-code"));
            store.Save(MakeProfile("Default", "default", "codex", isDefault: true));

            var claudeProfiles = store.List("claude-code");
            Assert.Equal(2, claudeProfiles.Count);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void Store_SetDefault_PromotesNewDemotesOld()
    {
        var dir = NewDir();
        try
        {
            var store = new LaunchProfileStore(dir);
            store.Save(MakeProfile("Default", "default", "claude-code", isDefault: true));
            store.Save(MakeProfile("Fast", "fast", "claude-code", isDefault: false));

            store.SetDefault("claude-code", "fast");

            var profiles = store.List("claude-code");
            Assert.False(profiles.First(p => p.Slug == "default").IsDefault);
            Assert.True(profiles.First(p => p.Slug == "fast").IsDefault);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void Store_DeleteDefault_PromotesFirstSurvivor()
    {
        var dir = NewDir();
        try
        {
            var store = new LaunchProfileStore(dir);
            store.Save(MakeProfile("Default", "default", "claude-code", isDefault: true));
            store.Save(MakeProfile("Fast", "fast", "claude-code", isDefault: false));

            store.Delete("claude-code", "default");

            var profiles = store.List("claude-code");
            Assert.Single(profiles);
            Assert.True(profiles[0].IsDefault);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void Store_DeleteLastDefault_LeavesEmpty()
    {
        var dir = NewDir();
        try
        {
            var store = new LaunchProfileStore(dir);
            store.Save(MakeProfile("Default", "default", "claude-code", isDefault: true));

            store.Delete("claude-code", "default");

            Assert.Empty(store.List("claude-code"));
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void Store_GetDefault_ReturnsDefaultProfile()
    {
        var dir = NewDir();
        try
        {
            var store = new LaunchProfileStore(dir);
            store.Save(MakeProfile("Default", "default", "claude-code", isDefault: true));
            store.Save(MakeProfile("Fast", "fast", "claude-code"));

            var def = store.GetDefault("claude-code");
            Assert.NotNull(def);
            Assert.Equal("default", def!.Slug);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void Store_GetDefault_NoProfiles_ReturnsSynthesized()
    {
        var dir = NewDir();
        try
        {
            var store = new LaunchProfileStore(dir);
            var def = store.GetDefault("claude-code");
            Assert.NotNull(def);
            Assert.True(def!.IsDefault);
            Assert.Equal("claude-code", def.Adapter);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }
}
