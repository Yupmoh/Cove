namespace Cove.Platform;

using System.Text.Json;
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
        var bytes = JsonSerializer.SerializeToUtf8Bytes(meta, PlatformJsonContext.Default.DataDirMeta);
        AtomicFile.Replace(dataDir.MetaJson, bytes, logger);
        (logger ?? NullLogger.Instance).CoveTreeMetaWritten(dataDir.MetaJson, CurrentSchemaVersion);
    }
}
