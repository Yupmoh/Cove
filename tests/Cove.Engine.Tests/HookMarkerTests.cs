using Cove.Adapters;
using Cove.Engine.Hooks;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class HookMarkerTests
{
    [Fact]
    public void MarkerPrefix_IsCoveRuntimeHook()
    {
        Assert.Equal("COVE_HOOK_MARKER=cove-runtime-hook", HookMarker.Prefix);
    }

    [Fact]
    public void OwnsLine_MatchesCoveMarker()
    {
        Assert.True(HookMarker.OwnsLine("COVE_HOOK_MARKER=cove-runtime-hook cove hook emit session-start --adapter claude-code"));
    }

    [Fact]
    public void OwnsLine_MatchesLegacyShapes()
    {
        Assert.True(HookMarker.OwnsLine("COVE_HOOK_MARKER=atrium-runtime-hook some-cmd"));
        Assert.True(HookMarker.OwnsLine("COVE_HOOK_MARKER=cove-runtime-hook"));
    }

    [Fact]
    public void OwnsLine_RejectsUserHook()
    {
        Assert.False(HookMarker.OwnsLine("/usr/local/bin/my-script.sh"));
        Assert.False(HookMarker.OwnsLine("echo hello"));
        Assert.False(HookMarker.OwnsLine("COVE_HOOK_MARKER=other-tool hook emit"));
    }

    [Fact]
    public void OwnsLine_RejectsEmpty()
    {
        Assert.False(HookMarker.OwnsLine(""));
        Assert.False(HookMarker.OwnsLine("   "));
    }
}

public sealed class HookConfigMergerTests
{
    [Fact]
    public void Merge_IntoEmpty_AddsCoveEntry()
    {
        var existing = new Dictionary<string, System.Text.Json.JsonElement>();
        var merged = HookConfigMerger.Merge(existing, "hooks", "cove-entry");
        Assert.True(merged.ContainsKey("hooks"));
    }

    [Fact]
    public void Merge_PreservesUserHooks_StripsPriorCove()
    {
        var existingJson = """{"hooks":["user-hook.sh","COVE_HOOK_MARKER=cove-runtime-hook old-cove-cmd","COVE_HOOK_MARKER=cove-runtime-hook other-cove"]}""";
        using var doc = System.Text.Json.JsonDocument.Parse(existingJson);
        var existing = new Dictionary<string, System.Text.Json.JsonElement> { ["hooks"] = doc.RootElement.GetProperty("hooks").Clone() };

        var merged = HookConfigMerger.Merge(existing, "hooks", "COVE_HOOK_MARKER=cove-runtime-hook new-cove-cmd");
        var hooksArr = merged["hooks"];
        var values = hooksArr.EnumerateArray().Select(e => e.GetString()!).ToList();
        Assert.Contains("user-hook.sh", values);
        Assert.Contains("COVE_HOOK_MARKER=cove-runtime-hook new-cove-cmd", values);
        Assert.DoesNotContain("COVE_HOOK_MARKER=cove-runtime-hook old-cove-cmd", values);
        Assert.DoesNotContain("COVE_HOOK_MARKER=cove-runtime-hook other-cove", values);
    }

    [Fact]
    public void Uninstall_PrunesCoveEntries_PreservesUser()
    {
        var existingJson = """{"hooks":["user-hook.sh","COVE_HOOK_MARKER=cove-runtime-hook cove-cmd","COVE_HOOK_MARKER=cove-runtime-hook another"]}""";
        using var doc = System.Text.Json.JsonDocument.Parse(existingJson);
        var existing = new Dictionary<string, System.Text.Json.JsonElement> { ["hooks"] = doc.RootElement.GetProperty("hooks").Clone() };

        var pruned = HookConfigMerger.Uninstall(existing, "hooks");
        var values = pruned["hooks"].EnumerateArray().Select(e => e.GetString()!).ToList();
        Assert.Contains("user-hook.sh", values);
        Assert.DoesNotContain("COVE_HOOK_MARKER=cove-runtime-hook cove-cmd", values);
    }

    [Fact]
    public void Uninstall_RemovesEmptyArrays()
    {
        var existingJson = """{"hooks":["COVE_HOOK_MARKER=cove-runtime-hook cove-cmd"]}""";
        using var doc = System.Text.Json.JsonDocument.Parse(existingJson);
        var existing = new Dictionary<string, System.Text.Json.JsonElement> { ["hooks"] = doc.RootElement.GetProperty("hooks").Clone() };

        var pruned = HookConfigMerger.Uninstall(existing, "hooks");
        Assert.False(pruned.ContainsKey("hooks"));
    }

    [Fact]
    public void Merge_PreservesObjectHookEntries()
    {
        var existingJson = """{"hooks":[{"type":"command","command":"user-script.sh"},"COVE_HOOK_MARKER=cove-runtime-hook old-cove"]}""";
        using var doc = System.Text.Json.JsonDocument.Parse(existingJson);
        var existing = new Dictionary<string, System.Text.Json.JsonElement> { ["hooks"] = doc.RootElement.GetProperty("hooks").Clone() };

        var merged = HookConfigMerger.Merge(existing, "hooks", "COVE_HOOK_MARKER=cove-runtime-hook new-cove");
        var items = merged["hooks"].EnumerateArray().ToList();
        Assert.Equal(2, items.Count);
        Assert.Equal(System.Text.Json.JsonValueKind.Object, items[0].ValueKind);
        Assert.Equal("user-script.sh", items[0].GetProperty("command").GetString());
        Assert.Equal("COVE_HOOK_MARKER=cove-runtime-hook new-cove", items[1].GetString());
    }

    [Fact]
    public void Merge_ObjectShapedCoveEntry_StripsPriorCoveObject()
    {
        var existingJson = """{"hooks":[{"type":"command","command":"COVE_HOOK_MARKER=cove-runtime-hook old-cove --adapter x"}]}""";
        using var doc = System.Text.Json.JsonDocument.Parse(existingJson);
        var existing = new Dictionary<string, System.Text.Json.JsonElement> { ["hooks"] = doc.RootElement.GetProperty("hooks").Clone() };

        var merged = HookConfigMerger.Merge(existing, "hooks", "COVE_HOOK_MARKER=cove-runtime-hook new-cove");
        var items = merged["hooks"].EnumerateArray().ToList();
        Assert.Single(items);
        Assert.Equal("COVE_HOOK_MARKER=cove-runtime-hook new-cove", items[0].GetString());
    }

    [Fact]
    public void WriteAtomic_WritesFileTempThenMove()
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cove-atomic-" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dir);
        try
        {
            var path = System.IO.Path.Combine(dir, "config.json");
            using var doc = System.Text.Json.JsonDocument.Parse("""{"hooks":["a"]}""");
            HookConfigMerger.WriteAtomic(path, doc.RootElement.Clone());

            Assert.True(System.IO.File.Exists(path));
            Assert.False(System.IO.File.Exists(path + ".tmp"));
            var content = System.IO.File.ReadAllText(path);
            Assert.Contains("hooks", content);
        }
        finally { try { System.IO.Directory.Delete(dir, true); } catch { } }
    }
}
