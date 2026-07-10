using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cove.Persistence;
using Microsoft.Extensions.Logging;

namespace Cove.Engine.Layout;

public sealed record LoadedWorkspace(WorkspaceSnapshot Snapshot, Dictionary<string, PaneDescriptor> Sessions, string WorkspaceDir);

public static class WorkspaceStartup
{
    public static IReadOnlyList<LoadedWorkspace> Enumerate(string workspacesRoot, ILogger logger)
    {
        var result = new List<LoadedWorkspace>();
        if (!Directory.Exists(workspacesRoot))
        {
            logger.LogWarning("workspace startup: no workspaces root at {Root}", workspacesRoot);
            return result;
        }
        foreach (var dir in Directory.EnumerateDirectories(workspacesRoot).OrderBy(d => d, System.StringComparer.Ordinal))
        {
            if (!File.Exists(Path.Combine(dir, "workspace.json")))
                continue;
            var (layout, sessions) = WorkspacePersistence.Load(dir, logger);
            if (layout is null)
            {
                logger.LogWarning("workspace startup: unreadable workspace.json in {Dir}", dir);
                continue;
            }
            result.Add(new LoadedWorkspace(layout, sessions, dir));
        }
        return result;
    }

    public static string DisplayName(WorkspaceSnapshot snap, string fallbackProjectDir)
    {
        if (!string.IsNullOrWhiteSpace(snap.Name) && snap.Name != LayoutService.DefaultWorkspaceId)
            return snap.Name;
        var dir = string.IsNullOrWhiteSpace(snap.ProjectDir) ? fallbackProjectDir : snap.ProjectDir;
        var baseName = string.IsNullOrWhiteSpace(dir) ? "" : Path.GetFileName(dir.TrimEnd('/', '\\'));
        return string.IsNullOrWhiteSpace(baseName) ? "Workspace" : baseName;
    }
}
