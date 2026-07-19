namespace Cove.Platform;

public sealed record DataDirMeta(
    int DataDirSchemaVersion,
    long CreatedAtUnixMs,
    string CoveVersionAtCreate);
