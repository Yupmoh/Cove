namespace Cove.Platform;

using Cove.Persistence;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

public static class DataDirMetaStore
{
    public const int CurrentSchemaVersion = 1;

    public static void WriteInitial(CoveDataDir dataDir, ILogger? logger = null)
    {
        var meta = new DataDirMeta(
            DataDirSchemaVersion: CurrentSchemaVersion,
            CreatedAtUnixMs: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            CoveVersionAtCreate: CoveBuild.InformationalVersion);
        AtomicJsonStore.Write(dataDir.MetaJson, meta, CoveJsonContext.Default.DataDirMeta);
        (logger ?? NullLogger.Instance).CoveTreeMetaWritten(dataDir.MetaJson, CurrentSchemaVersion);
    }
}
