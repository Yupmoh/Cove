using Cove.Engine.Knowledge;
using Cove.Protocol;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class CanvasFormBindingTests
{
    private static string NewDir() => System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cove-canvas-" + System.Guid.NewGuid().ToString("N"));

    private static (string dir, NoteFileStore store) NewStore()
    {
        var dir = NewDir();
        System.IO.Directory.CreateDirectory(dir);
        var kernel = new KnowledgePersistenceKernel(dir, NullLogger.Instance);
        kernel.EnsureAllSchemas();
        var snapshots = new NoteSnapshotService(dir, NullLogger.Instance);
        return (dir, new NoteFileStore(dir, NullLogger.Instance, snapshots));
    }

    [Fact]
    public void FormBindingState_RoundTripsThroughStateJson()
    {
        var (_, store) = NewStore();
        var note = store.Create(new Note { Title = "Canvas Form", BayId = "ws1", Content = "{}", Source = "gui", Kind = "canvas" });

        var formState = """{"username":"testuser","agree":true,"volume":75,"enabled":false}""";
        store.SaveState("ws1", note.Id, formState);

        var loaded = store.LoadState("ws1", note.Id);
        Assert.NotNull(loaded);
        Assert.Contains("testuser", loaded);
        Assert.Contains("\"agree\":true", loaded);
        Assert.Contains("\"volume\":75", loaded);
    }

    [Fact]
    public void CanvasNote_PersistsAndReloadsWithState()
    {
        var (_, store) = NewStore();
        var canvasJson = """{"root":{"elements":[{"id":"el1","type":"form.input","props":{"bind":"username","label":"Username"}}]},"state":{"username":"initial"}}""";
        var note = store.Create(new Note { Title = "Form Canvas", BayId = "ws1", Content = canvasJson, Source = "gui", Kind = "canvas" });

        store.SaveState("ws1", note.Id, """{"username":"changed"}""");

        var reloaded = store.Get("ws1", note.Id);
        Assert.NotNull(reloaded);
        Assert.Contains("form.input", reloaded!.Content);
        Assert.Contains("username", reloaded.Content);

        var state = store.LoadState("ws1", note.Id);
        Assert.Contains("changed", state);
    }

    [Fact]
    public void CanvasNote_StateSurvivesNoteUpdate()
    {
        var (_, store) = NewStore();
        var note = store.Create(new Note { Title = "Stateful", BayId = "ws1", Content = "{}", Source = "gui", Kind = "canvas" });

        store.SaveState("ws1", note.Id, """{"formState":{"name":"test"}}""");
        store.Update("ws1", note.Id, n => n with { Content = """{"root":{"elements":[]},"state":{}}""" });

        var state = store.LoadState("ws1", note.Id);
        Assert.NotNull(state);
        Assert.Contains("test", state);
    }
}
