using System.Text;
using Cove.Persistence;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Persistence.Tests;

public sealed class StateStoreTests : IDisposable
{
    private readonly string _dir;
    private readonly string _journalDir;
    private readonly string _path;
    private byte[] _content = Encoding.UTF8.GetBytes("value-0");
    private bool _hydrated = true;
    private int _serializeCount;

    public StateStoreTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "cove-statestore-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _journalDir = Path.Combine(_dir, "journal");
        _path = Path.Combine(_dir, "state.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private StateStore NewStore(TimeSpan? debounce = null, int keep = 10)
    {
        var store = new StateStore(_journalDir, NullLogger.Instance, debounce, keep);
        store.Register("state", _path, () => { Interlocked.Increment(ref _serializeCount); return _content; }, () => _hydrated);
        return store;
    }

    [Fact]
    public void Flush_WritesRegisteredState()
    {
        using var store = NewStore();
        _content = Encoding.UTF8.GetBytes("value-1");
        store.MarkDirty("state");
        store.Flush();

        Assert.True(File.Exists(_path));
        Assert.Equal("value-1", File.ReadAllText(_path));
    }

    [Fact]
    public void Unhydrated_IsNotWritten_ThenWritesAfterHydration()
    {
        using var store = NewStore();
        _hydrated = false;
        store.MarkDirty("state");
        store.Flush();
        Assert.False(File.Exists(_path));

        _hydrated = true;
        store.MarkDirty("state");
        store.Flush();
        Assert.True(File.Exists(_path));
    }

    [Fact]
    public void Debounce_CoalescesRapidMarks_IntoOneWrite()
    {
        using var store = NewStore(debounce: TimeSpan.FromMilliseconds(100));
        for (int i = 0; i < 5; i++)
            store.MarkDirty("state");

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (Volatile.Read(ref _serializeCount) < 1 && DateTime.UtcNow < deadline)
            Thread.Sleep(25);

        Assert.Equal(1, Volatile.Read(ref _serializeCount));
        Thread.Sleep(400);
        Assert.Equal(1, Volatile.Read(ref _serializeCount));
    }

    [Fact]
    public void CorruptCurrentFile_LoadRecoversFromJournal()
    {
        using var store = NewStore();
        _content = Encoding.UTF8.GetBytes("value-good");
        store.MarkDirty("state");
        store.Flush();

        File.WriteAllText(_path, "!!corrupt!!");
        if (File.Exists(_path + ".bak")) File.WriteAllText(_path + ".bak", "!!corrupt!!");

        var recovered = store.Load<string>(_path, "state", static bytes =>
        {
            var s = Encoding.UTF8.GetString(bytes);
            return s.StartsWith("value", StringComparison.Ordinal) ? s : null;
        });

        Assert.Equal("value-good", recovered);
    }

    [Fact]
    public void JournalRotation_KeepsAtMostKeep()
    {
        using var store = NewStore(keep: 3);
        for (int i = 0; i < 6; i++)
        {
            _content = Encoding.UTF8.GetBytes("value-" + i);
            store.MarkDirty("state");
            store.Flush();
            Thread.Sleep(3);
        }

        var journals = Directory.GetFiles(_journalDir, "state.*.json");
        Assert.True(journals.Length <= 3, $"expected <= 3 journal entries, found {journals.Length}");
    }

    [Fact]
    public void StrayTmpFromKilledWrite_LoadReturnsOldContent_AndNextFlushRecovers()
    {
        using var store = NewStore();
        _content = Encoding.UTF8.GetBytes("value-good");
        store.MarkDirty("state");
        store.Flush();

        var strayTmp = Path.Combine(_dir, ".state.json.tmp-deadprocess");
        File.WriteAllBytes(strayTmp, Encoding.UTF8.GetBytes("!!partial-dead!!"));

        var recovered = store.Load<string>(_path, "state", static bytes =>
        {
            var s = Encoding.UTF8.GetString(bytes);
            return s.StartsWith("value", StringComparison.Ordinal) ? s : null;
        });
        Assert.Equal("value-good", recovered);

        _content = Encoding.UTF8.GetBytes("value-updated");
        store.MarkDirty("state");
        store.Flush();

        var afterRecover = store.Load<string>(_path, "state", static bytes =>
        {
            var s = Encoding.UTF8.GetString(bytes);
            return s.StartsWith("value", StringComparison.Ordinal) ? s : null;
        });
        Assert.Equal("value-updated", afterRecover);
    }
}
