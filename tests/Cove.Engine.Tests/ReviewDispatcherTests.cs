using Cove.Engine.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class ReviewDispatcherTests
{
    [Fact]
    public async Task Dispatch_WritesRenderedMessageToTargetNook()
    {
        var dispatcher = new ReviewDispatcher(NullLogger.Instance);
        string? writtenToNook = null;
        var request = new ReviewDispatchRequest("nook-1", "ws-1", "session-1", null, "Fix the null check on line 42", "abc1234");

        var result = await dispatcher.DispatchAsync(request, (nookId, bytes) =>
        {
            writtenToNook = nookId;
            return Task.CompletedTask;
        });

        Assert.Equal("nook-1", writtenToNook);
        Assert.False(string.IsNullOrEmpty(result.DispatchId));
        Assert.Equal("nook-1", result.TargetNookId);
        Assert.Equal("session-1", result.SessionId);
    }

    [Fact]
    public async Task Dispatch_IncludesTaskRunId_WhenProvided()
    {
        var dispatcher = new ReviewDispatcher(NullLogger.Instance);
        var request = new ReviewDispatchRequest("nook-1", "ws-1", "session-1", "task-run-99", "Fix this", null);

        var result = await dispatcher.DispatchAsync(request, (_, _) => Task.CompletedTask);

        Assert.Equal("task-run-99", result.TaskRunId);
    }

    [Fact]
    public async Task Dispatch_RenderedMessage_ContainsReviewContent()
    {
        var dispatcher = new ReviewDispatcher(NullLogger.Instance);
        byte[]? writtenBytes = null;
        var request = new ReviewDispatchRequest("nook-1", "ws-1", "session-1", null, "Fix the null check", "abc1234");

        await dispatcher.DispatchAsync(request, (_, bytes) =>
        {
            writtenBytes = bytes;
            return Task.CompletedTask;
        });

        var writtenText = System.Text.Encoding.UTF8.GetString(writtenBytes!);
        Assert.Contains("Review Dispatch", writtenText);
        Assert.Contains("Fix the null check", writtenText);
        Assert.Contains("session-1", writtenText);
        Assert.Contains("abc1234", writtenText);
        Assert.EndsWith("\r", writtenText);
    }

    [Fact]
    public async Task Dispatch_GeneratesUniqueDispatchId()
    {
        var dispatcher = new ReviewDispatcher(NullLogger.Instance);
        var request = new ReviewDispatchRequest("nook-1", "ws-1", "session-1", null, "Fix", null);

        var result1 = await dispatcher.DispatchAsync(request, (_, _) => Task.CompletedTask);
        var result2 = await dispatcher.DispatchAsync(request, (_, _) => Task.CompletedTask);

        Assert.NotEqual(result1.DispatchId, result2.DispatchId);
    }

    [Fact]
    public async Task Dispatch_WithoutCommitSha_RendersGracefully()
    {
        var dispatcher = new ReviewDispatcher(NullLogger.Instance);
        byte[]? writtenBytes = null;
        var request = new ReviewDispatchRequest("nook-1", "ws-1", "session-1", null, "Review needed", null);

        await dispatcher.DispatchAsync(request, (_, bytes) =>
        {
            writtenBytes = bytes;
            return Task.CompletedTask;
        });

        var writtenText = System.Text.Encoding.UTF8.GetString(writtenBytes!);
        Assert.Contains("current", writtenText);
    }
}
