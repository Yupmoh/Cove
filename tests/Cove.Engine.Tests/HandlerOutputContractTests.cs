using System.Text.Json;
using Cove.Engine.Knowledge;
using Cove.Engine.Snapshots;
using Cove.Protocol;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class HandlerOutputContractTests
{
    [Fact]
    public void Knowledge_Dtos_DecodeIdentically_AcrossContexts()
    {
        var sample = new
        {
            id = "cmt_abc123",
            rootId = "cmt_abc123",
            parentId = (string?)null,
            commitSha = "abc1234",
            filePath = "src/file.cs",
            line = 10,
            author = "alice",
            body = "needs null check",
            state = "open",
            createdAt = "2026-07-08T12:00:00Z",
            orphanedAt = (string?)null,
            hunkId = (string?)null,
            contextHash = (string?)null,
        };

        var json = JsonSerializer.Serialize(sample);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("cmt_abc123", root.GetProperty("id").GetString());
        Assert.Equal("cmt_abc123", root.GetProperty("rootId").GetString());
        Assert.Equal("src/file.cs", root.GetProperty("filePath").GetString());
        Assert.Equal(10, root.GetProperty("line").GetInt32());
        Assert.Equal("open", root.GetProperty("state").GetString());
    }

    [Fact]
    public void Attribution_Dto_CamelCase_RoundTrips()
    {
        var dto = new AttributionEntryDto("id-1", "session-1", "tool-1", "file.cs", 1, 10, "2026-07-08T12:00:00Z");
        var json = JsonSerializer.Serialize(dto, Cove.Protocol.CoveJsonContext.Default.AttributionEntryDto);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("id-1", root.GetProperty("id").GetString());
        Assert.Equal("session-1", root.GetProperty("sessionId").GetString());
        Assert.Equal("tool-1", root.GetProperty("toolUseId").GetString());
        Assert.Equal("file.cs", root.GetProperty("filePath").GetString());
        Assert.Equal(1, root.GetProperty("startLine").GetInt32());
        Assert.Equal(10, root.GetProperty("endLine").GetInt32());
    }

    [Fact]
    public void Snapshot_Diff_Dto_CamelCase_RoundTrips()
    {
        var dto = new SnapshotDiffItem("config.timeout", "30", "60", "changed");
        var json = JsonSerializer.Serialize(dto, SnapshotVerbJsonContext.Default.SnapshotDiffItem);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("config.timeout", root.GetProperty("key").GetString());
        Assert.Equal("30", root.GetProperty("oldValue").GetString());
        Assert.Equal("60", root.GetProperty("newValue").GetString());
        Assert.Equal("changed", root.GetProperty("changeType").GetString());
    }

    [Fact]
    public void Vault_Settings_Store_PersistsAcrossInstances()
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"cove-contract-{System.Guid.NewGuid():N}");
        System.IO.Directory.CreateDirectory(dir);
        try
        {
            var store1 = new VaultSettingsStore(dir, NullLogger.Instance);
            store1.Set("depth", "deep");
            store1.Set("horizon", "90");

            var store2 = new VaultSettingsStore(dir, NullLogger.Instance);
            var settings = store2.Get();

            Assert.Equal("deep", settings.Depth);
            Assert.Equal(90, settings.Horizon);
        }
        finally { try { System.IO.Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void Comment_Anchor_ContextHash_Deterministic()
    {
        var hash1 = CommentAnchorEngine.ComputeContextHash("var x = 1;");
        var hash2 = CommentAnchorEngine.ComputeContextHash("var x = 1;");
        var hash3 = CommentAnchorEngine.ComputeContextHash("var x = 2;");

        Assert.Equal(hash1, hash2);
        Assert.NotEqual(hash1, hash3);
    }

    [Fact]
    public void Library_Secret_Redaction_CatchesAllKnownFormats()
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"cove-contract-{System.Guid.NewGuid():N}");
        System.IO.Directory.CreateDirectory(dir);
        try
        {
            var store = new LibraryStore(dir, NullLogger.Instance);
            var redacted = store.RedactSecrets("password=secret123 api_key=sk-abc AKIAIOSFODNN7EXAMPLE ghp_token xyz");
            Assert.DoesNotContain("secret123", redacted);
            Assert.DoesNotContain("AKIAIOSFODNN7EXAMPLE", redacted);
            Assert.Contains("[REDACTED]", redacted);
        }
        finally { try { System.IO.Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public async Task Review_Store_Lifecycle_MaintainsAuditTrail()
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"cove-contract-{System.Guid.NewGuid():N}");
        System.IO.Directory.CreateDirectory(dir);
        try
        {
            var store = new ReviewStore(dir, NullLogger.Instance);
            var comment = store.AddComment("sha1", "file.cs", 10, "alice", "fix needed", null);
            store.ResolveComment(comment.Id, "alice");
            store.ReopenComment(comment.Id, "bob");
            store.ResolveComment(comment.Id, "bob");

            var audit = store.GetAuditTrail(comment.Id);
            Assert.Equal(3, audit.Count);
            Assert.Equal("resolved", audit[0].ToState);
            Assert.Equal("open", audit[1].ToState);
            Assert.Equal("resolved", audit[2].ToState);
        }
        finally { try { System.IO.Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public async Task Snapshot_Service_RoundTrip_WithUndo()
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"cove-contract-{System.Guid.NewGuid():N}");
        System.IO.Directory.CreateDirectory(dir);
        try
        {
            var svc = new SnapshotService(dir, System.IO.Path.Combine(dir, "snapshots"),
                new Cove.Engine.Workspaces.ProcessGitRunner(), NullLogger.Instance);
            var state = new Dictionary<string, string> { ["workspace.json"] = "v1" };
            var snap = await svc.TakeAsync(state, SnapshotTrigger.Manual);
            Assert.NotNull(snap);

            var restored = await svc.RestoreAsync(snap!.Id);
            Assert.NotNull(restored);
            Assert.Contains("v1", restored!.Values.First());

            var afterRestore = await svc.ListAsync();
            Assert.Contains(afterRestore, s => s.Trigger == SnapshotTrigger.PreRestore);
        }
        finally { try { System.IO.Directory.Delete(dir, true); } catch { } }
    }
}
