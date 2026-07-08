using System.Collections.Generic;
using System.IO;
using Cove.Persistence;
using Cove.Platform;
using Microsoft.Extensions.Logging;
namespace Cove.Engine.Migrations;

public sealed record MigrationResult(bool NoOp, int FromVersion, int ToVersion);

public sealed class MigrationRunner
{
    private readonly string _dataDir;
    private readonly ILogger _logger;
    private readonly Dictionary<int, (string Name, System.Action<string> Run)> _migrations = new();

    public MigrationRunner(string dataDir, ILogger logger, int? targetVersion = null)
    {
        _dataDir = dataDir;
        _logger = logger;
        _targetVersion = targetVersion;
    }
    private readonly int? _targetVersion;

    public void Register(int toVersion, string name, System.Action<string> run)
    {
        _migrations[toVersion] = (name, run);
    }
    public MigrationResult Migrate()
    {
        var fromVersion = ReadCurrentVersion();
        var toVersion = fromVersion;
        var ran = false;
        var targetVersion = _targetVersion ?? System.Math.Min(MaxRegisteredVersion(), DataDirMetaStore.CurrentSchemaVersion);
        for (var v = fromVersion + 1; v <= targetVersion; v++)
        {
            if (_migrations.TryGetValue(v, out var migration))
            {
                _logger.MigrationRunning(v, migration.Name);
                migration.Run(_dataDir);
                toVersion = v;
                ran = true;
            }
        }
        if (ran)
            WriteCurrentVersion(toVersion);
        return new MigrationResult(!ran, fromVersion, toVersion);
    }

    private int MaxRegisteredVersion()
    {
        var max = DataDirMetaStore.CurrentSchemaVersion;
        foreach (var v in _migrations.Keys)
            if (v > max) max = v;
        return max;
    }

    private int ReadCurrentVersion()
    {
        var metaPath = Path.Combine(_dataDir, "meta.json");
        if (!File.Exists(metaPath))
        {
            var initial = new DataDirMeta(DataDirMetaStore.CurrentSchemaVersion, System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), Cove.Platform.CoveBuild.InformationalVersion);
            AtomicJsonStore.Write(metaPath, initial, CoveJsonContext.Default.DataDirMeta);
            return DataDirMetaStore.CurrentSchemaVersion;
        }
        var meta = AtomicJsonStore.Read(metaPath, CoveJsonContext.Default.DataDirMeta, _logger);
        return meta?.DataDirSchemaVersion ?? DataDirMetaStore.CurrentSchemaVersion;
    }

    private void WriteCurrentVersion(int version)
    {
        var metaPath = Path.Combine(_dataDir, "meta.json");
        var existing = AtomicJsonStore.Read(metaPath, CoveJsonContext.Default.DataDirMeta, _logger);
        var updated = new DataDirMeta(version, existing?.CreatedAtUnixMs ?? System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), existing?.CoveVersionAtCreate ?? Cove.Platform.CoveBuild.InformationalVersion);
        AtomicJsonStore.Write(metaPath, updated, CoveJsonContext.Default.DataDirMeta);
    }
}
