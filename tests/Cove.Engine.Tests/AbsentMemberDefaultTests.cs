using System.Text.Json;
using Cove.Engine.Knowledge;
using Cove.Engine.Restart;
using Cove.Engine.Workspaces;
using Xunit;

namespace Cove.Engine.Tests;

public class AbsentMemberDefaultTests
{
    [Fact]
    public void WorkspaceModel_AbsentMembers_KeepDefaults()
    {
        var json = "{\"id\":\"w\",\"name\":\"n\",\"projectDir\":\"/p\"}";
        var m = JsonSerializer.Deserialize(json, WorkspacesJsonContext.Default.WorkspaceModel)!;
        Assert.Equal(1, m.SchemaVersion);
        Assert.Equal("default", m.CollectionId);
        Assert.NotNull(m.Wings);
        Assert.Empty(m.Wings);
        Assert.NotNull(m.Rooms);
        Assert.Empty(m.Rooms);
        Assert.NotNull(m.Panes);
        Assert.Empty(m.Panes);
    }

    [Fact]
    public void RegistryModel_AbsentMembers_KeepDefaults()
    {
        var m = JsonSerializer.Deserialize("{}", WorkspacesJsonContext.Default.RegistryModel)!;
        Assert.Equal(1, m.SchemaVersion);
        Assert.NotNull(m.OpenWorkspaces);
        Assert.Empty(m.OpenWorkspaces);
        Assert.NotNull(m.Collections);
        Assert.Empty(m.Collections);
        Assert.Equal("default", m.ActiveCollectionId);
    }

    [Fact]
    public void Room_AbsentWingId_KeepsMain()
    {
        var json = "{\"id\":\"r\",\"name\":\"n\",\"layoutTree\":{\"kind\":\"leaf\",\"paneId\":\"p1\"}}";
        var r = JsonSerializer.Deserialize(json, WorkspacesJsonContext.Default.Room)!;
        Assert.Equal(WorkspaceModel.MainWingId, r.WingId);
    }

    [Fact]
    public void PaneRecord_AbsentMembers_KeepDefaults()
    {
        var r = JsonSerializer.Deserialize("{\"paneId\":\"p\"}", WorkspacesJsonContext.Default.PaneRecord)!;
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
        var json = "{\"title\":\"t\",\"workspaceId\":\"w\",\"source\":\"s\"}";
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
