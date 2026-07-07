using Cove.Adapters;
using Cove.Engine.Hooks;
using Cove.Engine.Pty;
using Cove.Generated;
using Cove.Protocol;

namespace Cove.Engine;

public static class EngineCommandRouter
{
    public static async Task<ControlResponse?> RouteAsync(
        ControlRequest request,
        PaneRegistry? panes = null,
        Cove.Engine.Layout.LayoutService? layout = null,
        Cove.Engine.Workspaces.WorkspaceManager? workspaces = null,
        Cove.Engine.Workspaces.RunCommandService? runCommands = null,
        Cove.Engine.Restart.RestorationService? restoration = null,
        Cove.Engine.Snapshots.SnapshotService? snapshots = null,
        Cove.Engine.Skills.SkillsService? skills = null,
        AgentDefinitionStore? agents = null,
        LaunchProfileStore? launchProfiles = null,
        AdapterEnvStore? adapterEnv = null,
        HookHttpServer? hookServer = null,
        HookEventRouter? hookRouter = null,
        System.Threading.CancellationToken cancellationToken = default)
    {
        System.Func<EngineDispatchContext, System.Threading.Tasks.Task<ControlResponse>> typed;
        try
        {
            if (!CoveCommandRegistry.Handlers.TryGetValue(request.Uri, out var handler))
                return null;
            typed = (System.Func<EngineDispatchContext, System.Threading.Tasks.Task<ControlResponse>>)handler;
        }
        catch
        {
            return null;
        }
        try
        {
            return await typed(new EngineDispatchContext(request, panes, layout, workspaces, runCommands, restoration, snapshots, skills, agents, launchProfiles, adapterEnv, hookServer, hookRouter));
        }
        catch (System.Exception ex)
        {
            return new ControlResponse(request.Id, false, null, new ControlError("handler_error", ex.Message));
        }
    }
}
