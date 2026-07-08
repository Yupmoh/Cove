using Cove.Engine.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class LibraryStoreTests
{
    private static string NewDir() => System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cove-lib-" + System.Guid.NewGuid().ToString("N"));

    private static (string dir, LibraryStore store) NewStore()
    {
        var dir = NewDir();
        System.IO.Directory.CreateDirectory(dir);
        var store = new LibraryStore(dir, NullLogger.Instance);
        store.EnsureSchema();
        return (dir, store);
    }

    [Fact]
    public void SaveEntry_ThenList_ReturnsEntry()
    {
        var (_, store) = NewStore();
        var entry = store.SaveEntry("ws1", "pane-1", "terminal", "My Session", """{"scroll":0}""", "some output", "history");

        var list = store.ListByWorkspace("ws1");
        Assert.Single(list);
        Assert.Equal(entry.Id, list[0].Id);
        Assert.Equal("terminal", list[0].PaneType);
    }

    [Fact]
    public void SaveEntry_RedactsPasswordInStateJson()
    {
        var (_, store) = NewStore();
        var entry = store.SaveEntry("ws1", "pane-1", "terminal", "Test", """{"password":"secret123"}""", null, "history");

        Assert.DoesNotContain("secret123", entry.StateJson);
        Assert.Contains("[REDACTED]", entry.StateJson);
    }

    [Fact]
    public void SaveEntry_RedactsSecretInScrollback()
    {
        var (_, store) = NewStore();
        var entry = store.SaveEntry("ws1", "pane-1", "terminal", "Test", null, "Authorization: Bearer my-secret-token", "history");

        Assert.DoesNotContain("my-secret-token", entry.Scrollback);
        Assert.Contains("[REDACTED]", entry.Scrollback);
    }

    [Fact]
    public void SaveEntry_RedactsApiKey()
    {
        var (_, store) = NewStore();
        var entry = store.SaveEntry("ws1", "pane-1", "terminal", "Test", """{"api_key":"sk-abc123xyz"}""", null, "history");

        Assert.DoesNotContain("sk-abc123xyz", entry.StateJson);
        Assert.Contains("[REDACTED]", entry.StateJson);
    }

    [Fact]
    public void SaveEntry_DoesNotRedactNonSecretContent()
    {
        var (_, store) = NewStore();
        var entry = store.SaveEntry("ws1", "pane-1", "terminal", "Test", """{"name":"test","count":42}""", "normal output", "history");

        Assert.Contains("test", entry.StateJson!);
        Assert.Contains("42", entry.StateJson!);
        Assert.Contains("normal output", entry.Scrollback!);
    }

    [Fact]
    public void SeededSecretNotPersisted()
    {
        var (dir, store) = NewStore();
        var secretValue = "super-secret-password-12345";
        store.SaveEntry("ws1", "pane-1", "terminal", "Test", $@"{{""password"":""{secretValue}""}}", $"Enter password: {secretValue}", "history");

        var catalogPath = System.IO.Path.Combine(dir, "library", "catalog.db");
        var entriesDir = System.IO.Path.Combine(dir, "library", "entries");

        using var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={catalogPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT state_json, scrollback FROM catalog";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var state = reader.IsDBNull(0) ? "" : reader.GetString(0);
            var sb = reader.IsDBNull(1) ? "" : reader.GetString(1);
            Assert.DoesNotContain(secretValue, state);
            Assert.DoesNotContain(secretValue, sb);
        }

        foreach (var file in System.IO.Directory.GetFiles(entriesDir, "*.json"))
        {
            var content = System.IO.File.ReadAllText(file);
            Assert.DoesNotContain(secretValue, content);
        }
    }

    [Fact]
    public void Pin_MarksEntryAsPinned()
    {
        var (_, store) = NewStore();
        var entry = store.SaveEntry("ws1", "pane-1", "terminal", "Test", null, null, "saved");
        store.Pin(entry.Id);
        var list = store.ListByWorkspace("ws1");
        Assert.Equal(entry.Id, list[0].Id);
    }

    [Fact]
    public void Archive_RemovesFromDefaultList()
    {
        var (_, store) = NewStore();
        var entry = store.SaveEntry("ws1", "pane-1", "terminal", "Test", null, null, "history");
        store.Archive(entry.Id);
        var list = store.ListByWorkspace("ws1");
        Assert.Empty(list);
    }

    [Fact]
    public void List_FiltersByKind()
    {
        var (_, store) = NewStore();
        store.SaveEntry("ws1", "pane-1", "terminal", "A", null, null, "saved");
        store.SaveEntry("ws1", "pane-2", "terminal", "B", null, null, "history");

        var saved = store.ListByWorkspace("ws1", "saved");
        var history = store.ListByWorkspace("ws1", "history");
        Assert.Single(saved);
        Assert.Single(history);
    }

    [Fact]
    public void SaveEntry_RedactsKeywordFreeAwsKey()
    {
        var (_, store) = NewStore();
        var awsKey = "AKIAIOSFODNN7EXAMPLE";
        var entry = store.SaveEntry("ws1", "pane-1", "terminal", "Test", null, $"export AWS_ACCESS_KEY_ID={awsKey}", "history");

        Assert.DoesNotContain(awsKey, entry.Scrollback);
        Assert.Contains("[REDACTED]", entry.Scrollback);
    }

    [Fact]
    public void SaveEntry_RedactsKeywordFreeGitHubToken()
    {
        var (_, store) = NewStore();
        var ghToken = "ghp_1234567890abcdefghijklmnopqrstuvwxyz1234";
        var entry = store.SaveEntry("ws1", "pane-1", "terminal", "Test", null, $"echo {ghToken}", "history");

        Assert.DoesNotContain(ghToken, entry.Scrollback);
        Assert.Contains("[REDACTED]", entry.Scrollback);
    }

    [Fact]
    public void SeededKeywordFreeSecretNotPersisted()
    {
        var (dir, store) = NewStore();
        var awsKey = "AKIAIOSFODNN7EXAMPLE";
        store.SaveEntry("ws1", "pane-1", "terminal", "Test", null, $"AWS_ACCESS_KEY_ID={awsKey}", "history");

        var catalogPath = System.IO.Path.Combine(dir, "library", "catalog.db");
        using var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={catalogPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT scrollback FROM catalog";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var sb = reader.IsDBNull(0) ? "" : reader.GetString(0);
            Assert.DoesNotContain(awsKey, sb);
        }
    }
}
