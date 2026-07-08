using System.Text.Json;
using Cove.Engine.Knowledge;
using Cove.Protocol;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class TimelineSynthesizerTests
{
    private static string NewDir() => System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cove-synth-" + System.Guid.NewGuid().ToString("N"));

    private static (string dir, TimelineStore timeline, NoteStore notes, TimelineSynthesizer synth) NewStack()
    {
        var dir = NewDir();
        System.IO.Directory.CreateDirectory(dir);
        var kernel = new KnowledgePersistenceKernel(dir, NullLogger.Instance);
        kernel.EnsureAllSchemas();
        var timeline = new TimelineStore(dir, NullLogger.Instance);
        var notes = new NoteStore(dir);
        var synth = new TimelineSynthesizer(timeline, notes, NullLogger.Instance);
        return (dir, timeline, notes, synth);
    }

    [Fact]
    public void CreateBrief_ProducesTypedEntryWithSummaryOnlyBodyAndBackingNote()
    {
        var (_, timeline, notes, synth) = NewStack();
        var result = synth.CreateBrief("ws1", "last-week", "Shipped M5 task system with 949 tests", "Full prose: M5 delivered dispatch saga, signaling, resume, schedules, GUI kanban...");

        Assert.Equal("synthesis.brief", result.Entry.Kind);
        Assert.Equal("Shipped M5 task system with 949 tests", result.Entry.Summary);
        Assert.NotNull(result.Entry.JsonPayload);

        var meta = JsonSerializer.Deserialize(result.Entry.JsonPayload!, CoveJsonContext.Default.BriefMeta);
        Assert.NotNull(meta);
        Assert.Equal("last-week", meta!.Window);
        Assert.Equal(result.BackingNoteId, meta.BackingNoteId);

        var backingNote = notes.Get(result.BackingNoteId);
        Assert.NotNull(backingNote);
        Assert.Contains("Full prose", backingNote!.Content);
        Assert.Equal("markdown", backingNote.Kind);

        var list = timeline.ListByWorkspace("ws1");
        Assert.Single(list);
    }

    [Fact]
    public void CreateRecap_ProducesTypedEntryWithWindowRange()
    {
        var (_, _, _, synth) = NewStack();
        var start = new System.DateTimeOffset(2026, 7, 1, 0, 0, 0, System.TimeSpan.Zero);
        var end = new System.DateTimeOffset(2026, 7, 7, 0, 0, 0, System.TimeSpan.Zero);

        var result = synth.CreateRecap("ws1", start, end, "Week of M5 completion", "Full recap prose...");

        Assert.Equal("synthesis.recap", result.Entry.Kind);
        var meta = JsonSerializer.Deserialize(result.Entry.JsonPayload!, CoveJsonContext.Default.RecapMeta);
        Assert.NotNull(meta);
        Assert.Equal(start.ToString("o"), meta!.WindowStart);
        Assert.Equal(end.ToString("o"), meta.WindowEnd);
    }

    [Fact]
    public void CreateUpdate_ProducesTypedEntryWithAudience()
    {
        var (_, _, _, synth) = NewStack();
        var result = synth.CreateUpdate("ws1", "stakeholders", "M5 shipped, M6 underway", "Full update prose...");

        Assert.Equal("synthesis.update", result.Entry.Kind);
        var meta = JsonSerializer.Deserialize(result.Entry.JsonPayload!, CoveJsonContext.Default.UpdateMeta);
        Assert.NotNull(meta);
        Assert.Equal("stakeholders", meta!.Audience);
    }

    [Fact]
    public void AllSynthesisEntries_HaveValidTags()
    {
        var (_, _, _, synth) = NewStack();
        var brief = synth.CreateBrief("ws1", "w", "s", "p");
        var recap = synth.CreateRecap("ws1", System.DateTimeOffset.UtcNow, System.DateTimeOffset.UtcNow, "s", "p");
        var update = synth.CreateUpdate("ws1", "aud", "s", "p");

        foreach (var entry in new[] { brief.Entry, recap.Entry, update.Entry })
        {
            Assert.NotNull(entry.Tags);
            Assert.Contains("type:synthesis", entry.Tags!);
        }
    }
}
