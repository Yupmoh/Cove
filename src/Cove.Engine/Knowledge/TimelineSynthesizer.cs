using System.Text.Json;
using Cove.Protocol;
using Microsoft.Extensions.Logging;

namespace Cove.Engine.Knowledge;

public sealed class SynthesisResult(TimelineEntry Entry, string BackingNoteId, string BackingNotePath)
{
    public TimelineEntry Entry { get; } = Entry;
    public string BackingNoteId { get; } = BackingNoteId;
    public string BackingNotePath { get; } = BackingNotePath;
}

public sealed class TimelineSynthesizer
{
    private readonly TimelineStore _timeline;
    private readonly NoteStore _notes;
    private readonly ILogger _logger;

    public TimelineSynthesizer(TimelineStore timeline, NoteStore notes, ILogger logger)
    {
        _timeline = timeline;
        _notes = notes;
        _logger = logger;
    }

    public SynthesisResult CreateBrief(string workspaceId, string window, string summary, string fullProse, string? author = null)
    {
        var backingNote = _notes.Create(new Note
        {
            Title = $"Brief: {window}",
            WorkspaceId = workspaceId,
            Content = fullProse,
            Source = "synthesizer",
            Kind = "markdown",
        });

        var meta = new BriefMeta(window, backingNote.Id, author);
        var entry = _timeline.Append(new TimelineEntry
        {
            WorkspaceId = workspaceId,
            Kind = "synthesis.brief",
            Source = "synthesizer",
            Scope = "workspace",
            Summary = summary,
            JsonPayload = JsonSerializer.Serialize(meta, CoveJsonContext.Default.BriefMeta),
            Tags = ["type:synthesis", "subtype:brief"],
        });

        _logger.LogWarning("synthesis: created brief for {ws}, backing note {noteId}", workspaceId, backingNote.Id);
        return new SynthesisResult(entry, backingNote.Id, backingNote.Id);
    }

    public SynthesisResult CreateRecap(string workspaceId, System.DateTimeOffset windowStart, System.DateTimeOffset windowEnd, string summary, string fullProse, string? author = null)
    {
        var backingNote = _notes.Create(new Note
        {
            Title = $"Recap: {windowStart:yyyy-MM-dd} to {windowEnd:yyyy-MM-dd}",
            WorkspaceId = workspaceId,
            Content = fullProse,
            Source = "synthesizer",
            Kind = "markdown",
        });

        var meta = new RecapMeta(windowStart.ToString("o"), windowEnd.ToString("o"), backingNote.Id, author);
        var entry = _timeline.Append(new TimelineEntry
        {
            WorkspaceId = workspaceId,
            Kind = "synthesis.recap",
            Source = "synthesizer",
            Scope = "workspace",
            Summary = summary,
            JsonPayload = JsonSerializer.Serialize(meta, CoveJsonContext.Default.RecapMeta),
            Tags = ["type:synthesis", "subtype:recap"],
        });

        _logger.LogWarning("synthesis: created recap for {ws}, backing note {noteId}", workspaceId, backingNote.Id);
        return new SynthesisResult(entry, backingNote.Id, backingNote.Id);
    }

    public SynthesisResult CreateUpdate(string workspaceId, string audience, string summary, string fullProse, string? author = null)
    {
        var backingNote = _notes.Create(new Note
        {
            Title = $"Update for {audience}",
            WorkspaceId = workspaceId,
            Content = fullProse,
            Source = "synthesizer",
            Kind = "markdown",
        });

        var meta = new UpdateMeta(audience, backingNote.Id, author);
        var entry = _timeline.Append(new TimelineEntry
        {
            WorkspaceId = workspaceId,
            Kind = "synthesis.update",
            Source = "synthesizer",
            Scope = "workspace",
            Summary = summary,
            JsonPayload = JsonSerializer.Serialize(meta, CoveJsonContext.Default.UpdateMeta),
            Tags = ["type:synthesis", "subtype:update"],
        });

        _logger.LogWarning("synthesis: created update for {ws} → {audience}, backing note {noteId}", workspaceId, audience, backingNote.Id);
        return new SynthesisResult(entry, backingNote.Id, backingNote.Id);
    }
}
