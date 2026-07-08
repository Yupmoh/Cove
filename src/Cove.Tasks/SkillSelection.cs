namespace Cove.Tasks;

public sealed record SkillSelection(string Provenance, string Name, string Mode);
public sealed record TaskBinding(string? AgentRef, string? ProfileSlug, System.Collections.Generic.IReadOnlyList<SkillSelection> Skills);
public sealed record TaskProfilePayload(string? AgentRef, string? ProfileSlug, System.Collections.Generic.IReadOnlyList<SkillSelection> Skills);
