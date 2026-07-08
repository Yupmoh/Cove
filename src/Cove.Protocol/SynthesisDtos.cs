using System.Text.Json.Serialization;

namespace Cove.Protocol;

public sealed record BriefMeta(string Window, string? BackingNoteId, string? Author);

public sealed record RecapMeta(string WindowStart, string WindowEnd, string? BackingNoteId, string? Author);

public sealed record UpdateMeta(string Audience, string? BackingNoteId, string? Author);
