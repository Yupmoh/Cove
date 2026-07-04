namespace Cove.Platform;

using Cove.Persistence;

public static class DataDirMetaStore
{
    public const int CurrentSchemaVersion = 1;

    public static void WriteInitial(CoveDataDir dataDir)
    {
        var meta = new DataDirMeta(
            DataDirSchemaVersion: CurrentSchemaVersion,
            CreatedAtUnixMs: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            CoveVersionAtCreate: CoveBuild.InformationalVersion);
        AtomicJsonStore.Write(dataDir.MetaJson, meta, CoveJsonContext.Default.DataDirMeta);
    }
}
