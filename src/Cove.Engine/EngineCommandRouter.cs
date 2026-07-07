using System;
using System.Threading;
using System.Threading.Tasks;
using Cove.Engine.Pty;
using Cove.Generated;
using Cove.Protocol;

namespace Cove.Engine;

public static class EngineCommandRouter
{
    public static async Task<ControlResponse?> RouteAsync(ControlRequest request, PaneRegistry? panes = null, Cove.Engine.Layout.LayoutService? layout = null, Cove.Engine.Workspaces.WorkspaceManager? workspaces = null, Cove.Engine.Workspaces.RunCommandService? runCommands = null, Cove.Engine.Restart.RestorationService? restoration = null, Cove.Engine.Snapshots.SnapshotService? snapshots = null, Cove.Engine.Skills.SkillsService? skills = null, Cove.Adapters.AgentDefinitionStore? agents = null, CancellationToken cancellationToken = default)
    {
        Func<EngineDispatchContext, Task<ControlResponse>> typed;
        try
        {
            if (!CoveCommandRegistry.Handlers.TryGetValue(request.Uri, out var handler))
                return null;
            typed = (Func<EngineDispatchContext, Task<ControlResponse>>)handler;
        }
        catch
        {
            return null;
        }
        try
        {
            return await typed(new EngineDispatchContext(request, panes, layout, workspaces, runCommands, restoration, snapshots, skills, agents));
        }
        catch (Exception ex)
        {
            return new ControlResponse(request.Id, false, null, new ControlError("handler_error", ex.Message));
        }
    }

}
