using Cove.Engine.Captures;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class CaptureBundleTests
{
    private static string NewDir()
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"cove-cap-{System.Guid.NewGuid():N}");
        System.IO.Directory.CreateDirectory(dir);
        return dir;
    }

    [Fact]
    public void StartCapture_CreatesBundleDir()
    {
        var store = new CaptureStore(NewDir(), NullLogger.Instance);
        var cap = store.StartCapture("ws-1", "fullscreen", true, true, true);

        Assert.True(cap.Number > 0);
        Assert.True(System.IO.Directory.Exists(cap.BundleDir));
    }

    [Fact]
    public void StartCapture_MonotonicNumbering()
    {
        var dir = NewDir();
        var store = new CaptureStore(dir, NullLogger.Instance);
        var cap1 = store.StartCapture("ws-1", "fullscreen", false, false, false);
        var cap2 = store.StartCapture("ws-1", "region", false, false, false);

        Assert.Equal(cap1.Number + 1, cap2.Number);
    }

    [Fact]
    public void StartCapture_WritesMetaJson()
    {
        var dir = NewDir();
        var store = new CaptureStore(dir, NullLogger.Instance);
        var cap = store.StartCapture("ws-1", "fullscreen", true, false, true);

        var metaPath = System.IO.Path.Combine(cap.BundleDir, "meta.json");
        Assert.True(System.IO.File.Exists(metaPath));
        var json = System.IO.File.ReadAllText(metaPath);
        Assert.Contains("\"id\":", json);
        Assert.Contains("\"number\":", json);
        Assert.Contains("\"region\":", json);
        Assert.Contains("\"bayId\":", json);
        Assert.Contains("\"ws-1\"", json);
    }

    [Fact]
    public void StopCapture_SetsDurationAndStatus()
    {
        var dir = NewDir();
        var store = new CaptureStore(dir, NullLogger.Instance);
        var cap = store.StartCapture("ws-1", "fullscreen", false, false, false);

        System.Threading.Thread.Sleep(50);
        var stopped = store.StopCapture(cap.Id);

        Assert.Equal("stopped", stopped!.Status);
        Assert.True(stopped.Duration > System.TimeSpan.Zero);
    }

    [Fact]
    public void ListCaptures_ReturnsAllSortedByNumber()
    {
        var dir = NewDir();
        var store = new CaptureStore(dir, NullLogger.Instance);
        store.StartCapture("ws-1", "fullscreen", false, false, false);
        store.StartCapture("ws-1", "region", false, false, false);
        store.StartCapture("ws-1", "fullscreen", false, false, false);

        var list = store.ListCaptures();
        Assert.Equal(3, list.Count);
        Assert.True(list[0].Number < list[1].Number);
        Assert.True(list[1].Number < list[2].Number);
    }

    [Fact]
    public void DeleteCapture_RemovesBundleAndIndex()
    {
        var dir = NewDir();
        var store = new CaptureStore(dir, NullLogger.Instance);
        var cap = store.StartCapture("ws-1", "fullscreen", false, false, false);

        Assert.True(store.DeleteCapture(cap.Id));
        Assert.False(System.IO.Directory.Exists(cap.BundleDir));
        Assert.Empty(store.ListCaptures());
    }

    [Fact]
    public void DeleteCapture_Nonexistent_ReturnsFalse()
    {
        var store = new CaptureStore(NewDir(), NullLogger.Instance);
        Assert.False(store.DeleteCapture("nonexistent"));
    }

    [Fact]
    public void AttachToTask_RecordsAttachment()
    {
        var dir = NewDir();
        var store = new CaptureStore(dir, NullLogger.Instance);
        var cap = store.StartCapture("ws-1", "fullscreen", false, false, false);

        store.AttachToTask(cap.Id, "task-42");

        var attachments = store.GetTaskAttachments("task-42");
        Assert.Single(attachments);
        Assert.Equal(cap.Id, attachments[0]);
    }

    [Fact]
    public void AttachToTask_MultipleCaptures()
    {
        var dir = NewDir();
        var store = new CaptureStore(dir, NullLogger.Instance);
        var cap1 = store.StartCapture("ws-1", "fullscreen", false, false, false);
        var cap2 = store.StartCapture("ws-1", "region", false, false, false);

        store.AttachToTask(cap1.Id, "task-42");
        store.AttachToTask(cap2.Id, "task-42");

        var attachments = store.GetTaskAttachments("task-42");
        Assert.Equal(2, attachments.Count);
    }

    [Fact]
    public void StartCapture_StoresAudioMicCursorInMeta()
    {
        var dir = NewDir();
        var store = new CaptureStore(dir, NullLogger.Instance);
        var cap = store.StartCapture("ws-1", "region", audio: true, mic: true, cursor: true);

        var metaPath = System.IO.Path.Combine(cap.BundleDir, "meta.json");
        var json = System.IO.File.ReadAllText(metaPath);
        Assert.Contains("\"audio\": true", json);
        Assert.Contains("\"mic\": true", json);
        Assert.Contains("\"cursor\": true", json);
        Assert.Contains("\"region\"", json);
    }

    [Fact]
    public void FlagCapture_MarksChapter()
    {
        var dir = NewDir();
        var store = new CaptureStore(dir, NullLogger.Instance);
        var cap = store.StartCapture("ws-1", "fullscreen", false, false, false);
        System.Threading.Thread.Sleep(20);
        store.FlagCapture(cap.Id, "important moment");

        var chaptersPath = System.IO.Path.Combine(cap.BundleDir, "chapters.json");
        Assert.True(System.IO.File.Exists(chaptersPath));
        var json = System.IO.File.ReadAllText(chaptersPath);
        Assert.Contains("important moment", json);
    }

    [Fact]
    public void GetCapture_ReturnsById()
    {
        var dir = NewDir();
        var store = new CaptureStore(dir, NullLogger.Instance);
        var cap = store.StartCapture("ws-1", "fullscreen", false, false, false);

        var retrieved = store.GetCapture(cap.Id);
        Assert.NotNull(retrieved);
        Assert.Equal(cap.Id, retrieved!.Id);
    }

    [Fact]
    public void GetCapture_Nonexistent_ReturnsNull()
    {
        var store = new CaptureStore(NewDir(), NullLogger.Instance);
        Assert.Null(store.GetCapture("nonexistent"));
    }
}
