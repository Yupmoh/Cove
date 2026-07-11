using Cove.Engine.Activity;
using Xunit;
namespace Cove.Engine.Tests;

public sealed class OmniChatStoreTests
{
    private static string NewDir() => Path.Combine(Path.GetTempPath(), "cove-omnichat-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Append_PersistsMessage_AndHistoryLoadsIt()
    {
        var dir = NewDir();
        try
        {
            var store = new OmniChatStore(dir);
            store.Append("nook-1", new OmniChatMessage("user", "hello", DateTimeOffset.UtcNow));
            store.Append("nook-1", new OmniChatMessage("assistant", "hi there", DateTimeOffset.UtcNow));

            var fresh = new OmniChatStore(dir);
            var history = fresh.LoadHistory("nook-1");

            Assert.Equal(2, history.Count);
            Assert.Equal("hello", history[0].Body);
            Assert.Equal("hi there", history[1].Body);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void LoadHistory_UnknownNook_ReturnsEmpty()
    {
        var dir = NewDir();
        try
        {
            var store = new OmniChatStore(dir);
            Assert.Empty(store.LoadHistory("never-seen"));
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void Clear_RemovesConversationForNook()
    {
        var dir = NewDir();
        try
        {
            var store = new OmniChatStore(dir);
            store.Append("nook-1", new OmniChatMessage("user", "msg", DateTimeOffset.UtcNow));
            store.Clear("nook-1");

            Assert.Empty(store.LoadHistory("nook-1"));
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void Append_RejectsInvalidNookId()
    {
        var dir = NewDir();
        try
        {
            var store = new OmniChatStore(dir);
            store.Append("../evil", new OmniChatMessage("user", "msg", DateTimeOffset.UtcNow));

            Assert.Empty(store.LoadHistory("../evil"));
            Assert.False(File.Exists(Path.Combine(dir, "..", "evil.json")));
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void History_OrderedOldestFirst()
    {
        var dir = NewDir();
        try
        {
            var store = new OmniChatStore(dir);
            var t1 = DateTimeOffset.UtcNow;
            var t2 = t1.AddSeconds(1);
            store.Append("nook-1", new OmniChatMessage("user", "second", t2));
            store.Append("nook-1", new OmniChatMessage("user", "first", t1));

            var history = store.LoadHistory("nook-1");

            Assert.Equal("first", history[0].Body);
            Assert.Equal("second", history[1].Body);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }
}
