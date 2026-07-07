using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Cove.Adapters;
using Cove.Engine.Agents;
using Cove.Engine.Browser;
using Cove.Engine.Hooks;
using Cove.Engine.Knowledge;
using Cove.Engine.Launch;
using Cove.Engine.Lifecycle;
using Cove.Engine.Panes;
using Cove.Engine.Pty;
using Cove.Engine.Sessions;
using Cove.Engine.Tasks;
using Cove.Protocol;

namespace Cove.Engine;

public sealed class EngineDispatchContext
{
    public EngineDispatchContext(
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
        AgentMessageRouter? agentRouter = null,
        Cove.Engine.Activity.ActivityAggregate? activity = null,
        SessionResumeOrchestrator? sessions = null,
        AgentLifecycleController? lifecycle = null,
        LaunchOrchestrator? launcher = null,
        TaskStore? tasks = null,
        NoteStore? notes = null,
        TimelineStore? timeline = null,
        PaneTypeRegistry? paneTypes = null,
        BrowserPaneManager? browser = null)
    {
        Request = request;
        Panes = panes;
        Layout = layout;
        Workspaces = workspaces;
        RunCommands = runCommands;
        Restoration = restoration;
        Snapshots = snapshots;
        Skills = skills;
        Agents = agents;
        LaunchProfiles = launchProfiles;
        AdapterEnv = adapterEnv;
        HookServer = hookServer;
        HookRouter = hookRouter;
        AgentRouter = agentRouter;
        Activity = activity;
        Sessions = sessions;
        Lifecycle = lifecycle;
        Launcher = launcher;
        Tasks = tasks;
        Notes = notes;
        Timeline = timeline;
        PaneTypes = paneTypes;
        Browser = browser;
    }

    public ControlRequest Request { get; }
    public PaneRegistry? Panes { get; }
    public Cove.Engine.Layout.LayoutService? Layout { get; }
    public Cove.Engine.Workspaces.WorkspaceManager? Workspaces { get; }
    public Cove.Engine.Workspaces.RunCommandService? RunCommands { get; }
    public Cove.Engine.Restart.RestorationService? Restoration { get; }
    public Cove.Engine.Snapshots.SnapshotService? Snapshots { get; }
    public Cove.Engine.Skills.SkillsService? Skills { get; }
    public AgentDefinitionStore? Agents { get; }
    public LaunchProfileStore? LaunchProfiles { get; }
    public AdapterEnvStore? AdapterEnv { get; }
    public HookHttpServer? HookServer { get; }
    public HookEventRouter? HookRouter { get; }
    public AgentMessageRouter? AgentRouter { get; }
    public Cove.Engine.Activity.ActivityAggregate? Activity { get; }
    public SessionResumeOrchestrator? Sessions { get; }
    public AgentLifecycleController? Lifecycle { get; }
    public LaunchOrchestrator? Launcher { get; }
    public TaskStore? Tasks { get; }
    public NoteStore? Notes { get; }
    public TimelineStore? Timeline { get; }
    public PaneTypeRegistry? PaneTypes { get; }
    public BrowserPaneManager? Browser { get; }

    public ControlResponse Ok<T>(T data, JsonTypeInfo<T> typeInfo)
        => new ControlResponse(Request.Id, true, JsonSerializer.SerializeToElement(data, typeInfo));

    public ControlResponse Ok()
        => new ControlResponse(Request.Id, true, null);

    public ControlResponse Fail(string code, string message)
        => new ControlResponse(Request.Id, false, null, new ControlError(code, message));
}
