namespace Cove.Persistence;

public readonly record struct ProbeRow(string Id, long CreatedAt, string Title, string Body);
public readonly record struct ProbeHit(string Id, string Title);
public readonly record struct MatchArg(string Q);
