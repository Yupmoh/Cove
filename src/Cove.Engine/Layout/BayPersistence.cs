using System.Collections.Generic;
using System.IO;
using Cove.Persistence;
using Microsoft.Extensions.Logging;

namespace Cove.Engine.Layout;

public static class BayPersistence
{
    public static void Save(BaySnapshot layout, NookDescriptor[] nooks, string wsDir)
    {
        AtomicJsonStore.Write(Path.Combine(wsDir, "bay.json"), layout, CoveJsonContext.Default.BaySnapshot);
        foreach (var d in nooks)
            AtomicJsonStore.Write(Path.Combine(wsDir, "nooks", d.NookId, "session.json"), d, CoveJsonContext.Default.NookDescriptor);
    }

    public static (BaySnapshot? Layout, Dictionary<string, NookDescriptor> Sessions) Load(string wsDir, ILogger logger)
    {
        var layout = AtomicJsonStore.Read<BaySnapshot>(Path.Combine(wsDir, "bay.json"), CoveJsonContext.Default.BaySnapshot, logger);
        var sessions = new Dictionary<string, NookDescriptor>(System.StringComparer.Ordinal);
        var nooksDir = Path.Combine(wsDir, "nooks");
        if (Directory.Exists(nooksDir))
        {
            foreach (var dir in Directory.EnumerateDirectories(nooksDir))
            {
                var sf = Path.Combine(dir, "session.json");
                if (!File.Exists(sf))
                    continue;
                var desc = AtomicJsonStore.Read<NookDescriptor>(sf, CoveJsonContext.Default.NookDescriptor, logger);
                if (desc is not null)
                    sessions[desc.NookId] = desc;
            }
        }
        return (layout, sessions);
    }

    public static void SaveScrollback(string nookId, byte[] bytes, string wsDir)
    {
        var dir = Path.Combine(wsDir, "nooks", nookId);
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "scrollback.bin");
        var tmp = path + ".tmp";
        File.WriteAllBytes(tmp, bytes);
        File.Move(tmp, path, true);
    }

    public static byte[]? LoadScrollback(string nookId, string wsDir)
    {
        var path = Path.Combine(wsDir, "nooks", nookId, "scrollback.bin");
        return File.Exists(path) ? File.ReadAllBytes(path) : null;
    }
}
