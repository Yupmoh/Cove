using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Cove.Engine.Pty;
using Cove.Protocol;

namespace Cove.Engine;

public sealed class EngineDispatchContext
{
    public EngineDispatchContext(ControlRequest request, PaneRegistry? panes = null, Cove.Engine.Layout.LayoutService? layout = null, Cove.Engine.Workspaces.WorkspaceManager? workspaces = null, Cove.Engine.Workspaces.RunCommandService? runCommands = null, Cove.Engine.Restart.RestorationService? restoration = null, Cove.Engine.Snapshots.SnapshotService? snapshots = null, Cove.Engine.Skills.SkillsService? skills = null, Cove.Adapters.AgentDefinitionStore? agents = null)
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
    }

    public ControlRequest Request { get; }
    public PaneRegistry? Panes { get; }
    public Cove.Engine.Layout.LayoutService? Layout { get; }
    public Cove.Engine.Workspaces.WorkspaceManager? Workspaces { get; }
    public Cove.Engine.Workspaces.RunCommandService? RunCommands { get; }
    public Cove.Engine.Restart.RestorationService? Restoration { get; }
    public Cove.Engine.Snapshots.SnapshotService? Snapshots { get; }
    public Cove.Engine.Skills.SkillsService? Skills { get; }
    public Cove.Adapters.AgentDefinitionStore? Agents { get; }

    public ControlResponse Ok<T>(T data, JsonTypeInfo<T> typeInfo)
        => new ControlResponse(Request.Id, true, JsonSerializer.SerializeToElement(data, typeInfo));

    public ControlResponse Ok()
        => new ControlResponse(Request.Id, true, null);

    public ControlResponse Fail(string code, string message)
        => new ControlResponse(Request.Id, false, null, new ControlError(code, message));
}
