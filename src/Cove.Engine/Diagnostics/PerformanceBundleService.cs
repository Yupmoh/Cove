using System.IO.Compression;
using Microsoft.Extensions.Logging;

namespace Cove.Engine.Diagnostics;

public sealed record PerformanceBundle(
    string Id,
    string BundlePath,
    System.DateTimeOffset CreatedAt,
    long SizeBytes,
    int SnapshotCount,
    bool ContainsTrace);

public sealed class PerformanceBundleService
{
    private readonly ILogger _logger;
    private readonly DiagnosticsHub _hub;
    private readonly string _outputDir;

    public PerformanceBundleService(DiagnosticsHub hub, string outputDir, ILogger? logger = null)
    {
        _hub = hub;
        _outputDir = outputDir;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
        Directory.CreateDirectory(_outputDir);
    }

    public PerformanceBundle CreateBundle(string? tracePath = null)
    {
        var id = System.Guid.NewGuid().ToString("N");
        var createdAt = System.DateTimeOffset.UtcNow;
        var bundleName = $"perf-bundle-{createdAt:yyyyMMdd-HHmmss}-{id[..8]}";
        var bundlePath = Path.Combine(_outputDir, $"{bundleName}.zip");

        var snapshots = _hub.GetSnapshots();

        using (var archive = ZipFile.Open(bundlePath, ZipArchiveMode.Create))
        {
            var snapshotsEntry = archive.CreateEntry("diagnostics-snapshots.json");
            using (var sw = new StreamWriter(snapshotsEntry.Open()))
                sw.Write(_hub.ExportAllSnapshotsJson());

            var metaEntry = archive.CreateEntry("bundle-meta.json");
            using (var mw = new StreamWriter(metaEntry.Open()))
                mw.Write($$"""{"id":"{{id}}","createdAt":"{{createdAt:o}}","snapshotCount":{{snapshots.Count}},"containsTrace":{{(tracePath is not null).ToString().ToLowerInvariant()}}}""");

            if (tracePath is not null && File.Exists(tracePath))
            {
                var traceEntry = archive.CreateEntry(Path.GetFileName(tracePath));
                using var traceFs = traceEntry.Open();
                using var sourceFs = File.OpenRead(tracePath);
                sourceFs.CopyTo(traceFs);
            }
        }

        var sizeBytes = new FileInfo(bundlePath).Length;
        _logger.LogInformation("perf-bundle: created {path} ({count} snapshots, {size} bytes, trace={trace})",
            bundlePath, snapshots.Count, sizeBytes, tracePath is not null);

        return new PerformanceBundle(id, bundlePath, createdAt, sizeBytes, snapshots.Count, tracePath is not null);
    }

    public IReadOnlyList<PerformanceBundle> ListBundles()
    {
        var result = new List<PerformanceBundle>();
        foreach (var file in Directory.GetFiles(_outputDir, "perf-bundle-*.zip"))
        {
            try
            {
                using var archive = ZipFile.OpenRead(file);
                var metaEntry = archive.GetEntry("bundle-meta.json");
                if (metaEntry is null) continue;

                using var sr = new StreamReader(metaEntry.Open());
                var metaJson = sr.ReadToEnd();
                using var doc = System.Text.Json.JsonDocument.Parse(metaJson);

                var id = doc.RootElement.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? "" : "";
                var createdAtStr = doc.RootElement.TryGetProperty("createdAt", out var caEl) ? caEl.GetString() ?? "" : "";
                var snapshotCount = doc.RootElement.TryGetProperty("snapshotCount", out var scEl) ? scEl.GetInt32() : 0;
                var containsTrace = doc.RootElement.TryGetProperty("containsTrace", out var ctEl) && ctEl.GetBoolean();

                var createdAt = System.DateTimeOffset.TryParse(createdAtStr, out var ca) ? ca : System.DateTimeOffset.UtcNow;
                var sizeBytes = new FileInfo(file).Length;

                result.Add(new PerformanceBundle(id, file, createdAt, sizeBytes, snapshotCount, containsTrace));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "perf-bundle: failed to read bundle {path}", file);
            }
        }
        return result.OrderByDescending(b => b.CreatedAt).ToList();
    }

    public bool DeleteBundle(string bundlePath)
    {
        if (!File.Exists(bundlePath))
        {
            _logger.LogWarning("perf-bundle: not found {path}", bundlePath);
            return false;
        }

        try
        {
            File.Delete(bundlePath);
            _logger.LogInformation("perf-bundle: deleted {path}", bundlePath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "perf-bundle: failed to delete {path}", bundlePath);
            return false;
        }
    }
}
