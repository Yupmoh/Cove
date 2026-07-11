using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cove.Persistence;
using Microsoft.Extensions.Logging;

namespace Cove.Engine.Layout;

public sealed record LoadedBay(BaySnapshot Snapshot, Dictionary<string, NookDescriptor> Sessions, string BayDir);

public static class BayStartup
{
    public static IReadOnlyList<LoadedBay> Enumerate(string baysRoot, ILogger logger)
    {
        var result = new List<LoadedBay>();
        if (!Directory.Exists(baysRoot))
        {
            logger.LogWarning("bay startup: no bays root at {Root}", baysRoot);
            return result;
        }
        foreach (var dir in Directory.EnumerateDirectories(baysRoot).OrderBy(d => d, System.StringComparer.Ordinal))
        {
            if (!File.Exists(Path.Combine(dir, "bay.json")))
                continue;
            var (layout, sessions) = BayPersistence.Load(dir, logger);
            if (layout is null)
            {
                logger.LogWarning("bay startup: unreadable bay.json in {Dir}", dir);
                continue;
            }
            result.Add(new LoadedBay(layout, sessions, dir));
        }
        return result;
    }

    public static string DisplayName(BaySnapshot snap, string fallbackProjectDir)
    {
        if (!string.IsNullOrWhiteSpace(snap.Name) && snap.Name != LayoutService.DefaultBayId)
            return snap.Name;
        var dir = string.IsNullOrWhiteSpace(snap.ProjectDir) ? fallbackProjectDir : snap.ProjectDir;
        var baseName = string.IsNullOrWhiteSpace(dir) ? "" : Path.GetFileName(dir.TrimEnd('/', '\\'));
        return string.IsNullOrWhiteSpace(baseName) ? "Bay" : baseName;
    }
}
