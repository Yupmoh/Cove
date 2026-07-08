using Cove.Engine.Browser;
using Xunit;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cove.Engine.Tests;

public sealed class BrowserAnnotationDispatcherTests
{
    private static string NewDir() => System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"cove-ann-disp-{System.Guid.NewGuid():N}");

    [Fact]
    public async Task DispatchAsync_SendsAnnotationsToTargetPane()
    {
        var dir = NewDir();
        var store = new BrowserStore(dir, NullLogger.Instance);
        var ann1 = store.AddAnnotation("https://example.com/page", "comment", """{"selector":"#p1"}""", "Fix this paragraph", "user");
        var ann2 = store.AddAnnotation("https://example.com/page", "question", """{"selector":"#p2"}""", "What does this do?", "user");
        var dispatcher = new BrowserAnnotationDispatcher(store, NullLogger.Instance);

        var request = new AnnotationDispatchRequest("pane-agent-1", "https://example.com/page", new[] { ann1.Id, ann2.Id }, null, "Please review these notes");
        byte[]? sentBytes = null;
        string? sentPaneId = null;

        var result = await dispatcher.DispatchAsync(request, (paneId, bytes) => { sentPaneId = paneId; sentBytes = bytes; return Task.CompletedTask; });

        Assert.Equal("pane-agent-1", result.TargetPaneId);
        Assert.Equal(2, result.AnnotationCount);
        Assert.Equal("pane-agent-1", sentPaneId);
        Assert.NotNull(sentBytes);
        var sentText = System.Text.Encoding.UTF8.GetString(sentBytes!);
        Assert.Contains("Fix this paragraph", sentText);
        Assert.Contains("What does this do?", sentText);
        Assert.Contains("https://example.com/page", sentText);
        Assert.Contains("Please review these notes", sentText);
        Assert.Contains("Browser Annotation Dispatch", sentText);
    }

    [Fact]
    public async Task DispatchAsync_ThrowsWhenNoAnnotationsFound()
    {
        var dir = NewDir();
        var store = new BrowserStore(dir, NullLogger.Instance);
        var dispatcher = new BrowserAnnotationDispatcher(store, NullLogger.Instance);

        var request = new AnnotationDispatchRequest("pane-1", "https://nonexistent.com", new[] { "fake-id" }, null, "");
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await dispatcher.DispatchAsync(request, (_, _) => Task.CompletedTask));
    }

    [Fact]
    public async Task DispatchAsync_ThrowsWhenTargetPaneEmpty()
    {
        var dir = NewDir();
        var store = new BrowserStore(dir, NullLogger.Instance);
        store.AddAnnotation("https://example.com", "comment", "{}", "test", "user");
        var dispatcher = new BrowserAnnotationDispatcher(store, NullLogger.Instance);

        var request = new AnnotationDispatchRequest("", "https://example.com", new[] { "any" }, null, "");
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await dispatcher.DispatchAsync(request, (_, _) => Task.CompletedTask));
    }

    [Fact]
    public async Task DispatchAsync_RendersAnchorAndSource()
    {
        var dir = NewDir();
        var store = new BrowserStore(dir, NullLogger.Instance);
        var ann = store.AddAnnotation("https://example.com", "issue", """{"x":10,"y":20}""", "Broken layout", "agent");
        var dispatcher = new BrowserAnnotationDispatcher(store, NullLogger.Instance);

        var request = new AnnotationDispatchRequest("pane-1", "https://example.com", new[] { ann.Id }, "task-run-42", "");
        byte[]? sentBytes = null;
        await dispatcher.DispatchAsync(request, (_, bytes) => { sentBytes = bytes; return Task.CompletedTask; });

        var sentText = System.Text.Encoding.UTF8.GetString(sentBytes!);
        Assert.Contains("Task Run: task-run-42", sentText);
        Assert.Contains("""{"x":10,"y":20}""", sentText);
        Assert.Contains("Source: agent", sentText);
        Assert.Contains("Resolved: no", sentText);
    }

    [Fact]
    public async Task DispatchAsync_FiltersToOnlyRequestedAnnotations()
    {
        var dir = NewDir();
        var store = new BrowserStore(dir, NullLogger.Instance);
        var ann1 = store.AddAnnotation("https://example.com", "comment", "{}", "keep this", "user");
        var ann2 = store.AddAnnotation("https://example.com", "comment", "{}", "skip this", "user");
        var dispatcher = new BrowserAnnotationDispatcher(store, NullLogger.Instance);

        var request = new AnnotationDispatchRequest("pane-1", "https://example.com", new[] { ann1.Id }, null, "");
        var result = await dispatcher.DispatchAsync(request, (_, _) => Task.CompletedTask);

        Assert.Equal(1, result.AnnotationCount);
    }
}
