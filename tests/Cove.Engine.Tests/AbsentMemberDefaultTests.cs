using System.Text.Json;
using Cove.Engine.Bays;
using Cove.Engine.Knowledge;
using Cove.Engine.Restart;
using Xunit;

namespace Cove.Engine.Tests;

public class AbsentMemberDefaultTests
{
    [Fact]
    public void BayModel_AbsentMembers_KeepDefaults()
    {
        var json = "{\"id\":\"w\",\"name\":\"n\",\"projectDir\":\"/p\"}";
        var m = JsonSerializer.Deserialize(json, BaysJsonContext.Default.BayModel)!;
        Assert.Equal(1, m.SchemaVersion);
        Assert.Equal("default", m.CollectionId);
        Assert.NotNull(m.Nooks);
        Assert.Empty(m.Nooks);
    }

    [Fact]
    public void RegistryModel_AbsentMembers_KeepDefaults()
    {
        var m = JsonSerializer.Deserialize("{}", BaysJsonContext.Default.RegistryModel)!;
        Assert.Equal(1, m.SchemaVersion);
        Assert.NotNull(m.OpenBays);
        Assert.Empty(m.OpenBays);
        Assert.NotNull(m.Collections);
        Assert.Empty(m.Collections);
        Assert.Equal("default", m.ActiveCollectionId);
    }

    [Fact]
    public void ShoreSnapshot_AbsentWingId_KeepsMain()
    {
        var json = "{\"id\":\"r\",\"name\":\"n\",\"layoutTree\":{\"kind\":\"leaf\",\"nookId\":\"p1\"}}";
        var r = JsonSerializer.Deserialize(json, Cove.Persistence.CoveJsonContext.Default.ShoreSnapshot)!;
        Assert.Equal("main", r.WingId);
        Assert.False(r.Pinned);
    }

    [Fact]
    public void NookRecord_AbsentMembers_KeepDefaults()
    {
        var r = JsonSerializer.Deserialize("{\"nookId\":\"p\"}", BaysJsonContext.Default.NookRecord)!;
        Assert.Equal("none", r.ResidentScope);
    }

    [Fact]
    public void VaultSettings_AbsentMembers_KeepDefaults()
    {
        var v = JsonSerializer.Deserialize("{}", VaultSettingsJsonContext.Default.VaultSettings)!;
        Assert.Equal("standard", v.Depth);
        Assert.Equal(30, v.Horizon);
    }

    [Fact]
    public void NoteMeta_AbsentKind_KeepsMarkdown()
    {
        var json = "{\"title\":\"t\",\"bayId\":\"w\",\"source\":\"s\"}";
        var meta = JsonSerializer.Deserialize(json, NoteFileJsonContext.Default.NoteMeta)!;
        Assert.Equal("markdown", meta.Kind);
    }

    [Fact]
    public void SketchScene_AbsentElements_KeepEmpty()
    {
        var s = JsonSerializer.Deserialize("{}", SketchSceneJsonContext.Default.SketchScene)!;
        Assert.NotNull(s.Elements);
        Assert.Empty(s.Elements);
    }

    [Fact]
    public void LauncherOverrides_AbsentCollections_KeepEmpty()
    {
        var o = JsonSerializer.Deserialize("{}", AgentResumeJsonContext.Default.LauncherOverrides)!;
        Assert.NotNull(o.Env);
        Assert.Empty(o.Env);
        Assert.NotNull(o.ExtraFlags);
        Assert.Empty(o.ExtraFlags);
    }
}
