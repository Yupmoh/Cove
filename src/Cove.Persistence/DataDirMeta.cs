namespace Cove.Persistence;

public sealed record DataDirMeta(
    int DataDirSchemaVersion,
    long CreatedAtUnixMs,
    string CoveVersionAtCreate);
