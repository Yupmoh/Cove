using Cove.Engine.Layout;
using Cove.Engine.Workspaces;

namespace Cove.Engine.Protocol;

public sealed class ScopeResolver
{
    private readonly WorkspaceManager? _workspaces;

    public ScopeResolver(WorkspaceManager? workspaces = null) => _workspaces = workspaces;

    public (string? WorkspaceId, string? RoomId) ResolvePaneLocation(string? paneId)
    {
        if (paneId is null || _workspaces is null)
            return (null, null);
        foreach (var ws in _workspaces.ListWorkspaces())
        {
            var actor = _workspaces.Get(ws.Id);
            if (actor is null) continue;
            var model = actor.State;
            foreach (var room in model.Rooms)
                foreach (var leaf in MosaicOps.Leaves(room.LayoutTree))
                    if (leaf.PaneId == paneId)
                        return (ws.Id, room.Id);
        }
        return (null, null);
    }
}
