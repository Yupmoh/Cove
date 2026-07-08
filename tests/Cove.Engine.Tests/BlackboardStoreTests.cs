using Cove.Engine.Knowledge;
using Cove.Protocol;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class BlackboardStoreTests
{
    private static string NewDir() => System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cove-bb-" + System.Guid.NewGuid().ToString("N"));

    private static (string dir, BlackboardStore store) NewStore()
    {
        var dir = NewDir();
        System.IO.Directory.CreateDirectory(dir);
        var kernel = new KnowledgePersistenceKernel(dir, NullLogger.Instance);
        kernel.EnsureAllSchemas();
        return (dir, new BlackboardStore(dir, NullLogger.Instance));
    }

    [Fact]
    public void Post_ThenShow_ReturnsPost()
    {
        var (_, store) = NewStore();
        var post = store.Post("ws1", "observation", "workspace", "The build is failing on CI");

        var shown = store.Show("ws1");
        Assert.Single(shown);
        Assert.Equal(post.Id, shown[0].Id);
        Assert.Equal("observation", shown[0].Kind);
        Assert.Equal("The build is failing on CI", shown[0].Content);
    }

    [Fact]
    public void TtlExpiredPost_IsSweptOnShow()
    {
        var (_, store) = NewStore();
        store.Post("ws1", "claim", "workspace", "temporary claim", ttl: System.TimeSpan.FromMilliseconds(1));
        System.Threading.Thread.Sleep(50);

        var shown = store.Show("ws1");
        Assert.Empty(shown);
    }

    [Fact]
    public void NonExpiringPost_SurvivesShow()
    {
        var (_, store) = NewStore();
        store.Post("ws1", "claim", "workspace", "permanent claim");

        System.Threading.Thread.Sleep(50);
        var shown = store.Show("ws1");
        Assert.Single(shown);
    }

    [Fact]
    public void CorrectionWithRef_ShowsAlongsideOriginal()
    {
        var (_, store) = NewStore();
        var original = store.Post("ws1", "claim", "workspace", "The API returns JSON");
        var correction = store.Post("ws1", "correction", "workspace", "Actually the API returns XML", refId: original.Id);

        var shown = store.Show("ws1");
        Assert.Equal(2, shown.Count);

        var orig = shown.FirstOrDefault(p => p.Id == original.Id);
        var corr = shown.FirstOrDefault(p => p.Id == correction.Id);
        Assert.NotNull(orig);
        Assert.NotNull(corr);
        Assert.Equal(original.Id, corr!.RefId);
        Assert.Equal("The API returns JSON", orig!.Content);
        Assert.Equal("Actually the API returns XML", corr.Content);
    }

    [Fact]
    public void Show_FiltersByAudience()
    {
        var (_, store) = NewStore();
        store.Post("ws1", "observation", "workspace", "for workspace");
        store.Post("ws1", "observation", "room:abc", "for room");

        var wsPosts = store.Show("ws1", "workspace");
        var roomPosts = store.Show("ws1", "room:abc");
        Assert.Single(wsPosts);
        Assert.Single(roomPosts);
    }
}
