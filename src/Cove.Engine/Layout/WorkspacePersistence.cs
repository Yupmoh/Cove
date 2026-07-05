using System.Collections.Generic;
using System.IO;
using Cove.Persistence;
using Microsoft.Extensions.Logging;

namespace Cove.Engine.Layout;

public static class WorkspacePersistence
{
    public static void Save(WorkspaceSnapshot layout, PaneDescriptor[] panes, string wsDir)
    {
        AtomicJsonStore.Write(Path.Combine(wsDir, "workspace.json"), layout, CoveJsonContext.Default.WorkspaceSnapshot);
        foreach (var d in panes)
            AtomicJsonStore.Write(Path.Combine(wsDir, "panes", d.PaneId, "session.json"), d, CoveJsonContext.Default.PaneDescriptor);
    }

    public static (WorkspaceSnapshot? Layout, Dictionary<string, PaneDescriptor> Sessions) Load(string wsDir, ILogger logger)
    {
        var layout = AtomicJsonStore.Read<WorkspaceSnapshot>(Path.Combine(wsDir, "workspace.json"), CoveJsonContext.Default.WorkspaceSnapshot, logger);
        var sessions = new Dictionary<string, PaneDescriptor>(System.StringComparer.Ordinal);
        var panesDir = Path.Combine(wsDir, "panes");
        if (Directory.Exists(panesDir))
        {
            foreach (var dir in Directory.EnumerateDirectories(panesDir))
            {
                var sf = Path.Combine(dir, "session.json");
                if (!File.Exists(sf))
                    continue;
                var desc = AtomicJsonStore.Read<PaneDescriptor>(sf, CoveJsonContext.Default.PaneDescriptor, logger);
                if (desc is not null)
                    sessions[desc.PaneId] = desc;
            }
        }
        return (layout, sessions);
    }

    public static void SaveScrollback(string paneId, byte[] bytes, string wsDir)
    {
        var dir = Path.Combine(wsDir, "panes", paneId);
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "scrollback.bin");
        var tmp = path + ".tmp";
        File.WriteAllBytes(tmp, bytes);
        File.Move(tmp, path, true);
    }

    public static byte[]? LoadScrollback(string paneId, string wsDir)
    {
        var path = Path.Combine(wsDir, "panes", paneId, "scrollback.bin");
        return File.Exists(path) ? File.ReadAllBytes(path) : null;
    }
}
