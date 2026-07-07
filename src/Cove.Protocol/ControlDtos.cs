using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cove.Protocol;

public sealed record ControlRequest(
    string Id,
    string Uri,
    JsonElement? Params = null,
    string? Source = null,
    string? CallerPaneId = null);

public sealed record ControlResponse(
    string Id,
    bool Ok,
    JsonElement? Data = null,
    ControlError? Error = null);

public sealed record ControlError(string Code, string Message);

public sealed record ControlEvent(string Channel, JsonElement Payload);

public sealed record ControlErrorFrame(string Code, string Message, ulong? StreamId = null);

public sealed record HelloParams(int ProtocolVersion, string ClientKind, string ClientVersion, string Channel);
public sealed record HelloResult(int ProtocolVersion, string EngineVersion, int EnginePid, string Channel);

public sealed record DaemonStatusResult(int Pid, string Channel, string EngineVersion, int Connections, int Sessions, long UptimeSeconds);

public sealed record PaneInfo(string PaneId, string Command, int Cols, int Rows, bool Alive, string? Cwd = null);

public sealed record PaneListResult(PaneInfo[] Panes);

public sealed record SpawnParams(
    string Command,
    string[] Args,
    string? Cwd = null,
    Dictionary<string, string>? Env = null,
    int Cols = 80,
    int Rows = 24,
    string? InheritCwdFrom = null,
    string? Adapter = null,
    string? AgentName = null,
    string? Workspace = null,
    string? Room = null,
    string McpAccessScope = "same-tab",
    bool McpVisible = true);

public sealed record SubscribeParams(string PaneId, ulong SinceOffset = 0);
public sealed record SubscribeResult(ulong StreamId, ulong BaseOffset, int Window);

public sealed record WriteParams(ulong StreamId, string DataBase64);
public sealed record ResizeParams(string PaneId, int Cols, int Rows);

public sealed record PaneWriteParams(string PaneId, string DataBase64);
public sealed record PaneRefParams(string PaneId);

public sealed record LayoutMutateParams(string Op, string? RoomId = null, string? TargetPaneId = null, string? NewPaneId = null, string? Orientation = null, string? Name = null, string? PaneId = null, int Dir = 1);
public sealed record LayoutMutateResult(string? RoomId = null);
public sealed record SessionStateResult(string PaneId, string Command, int Cols, int Rows, bool Alive, string? Cwd = null);
public sealed record SearchParams(string PaneId, string Query, bool CaseSensitive = false);
public sealed record SearchMatch(int Line, string Text);
public sealed record SearchResult(SearchMatch[] Matches);
public sealed record SkillIndexItem(string Name, string Description, string Source, string Provenance, string? Adapter);
public sealed record SkillsIndexResult(SkillIndexItem[] Skills);
public sealed record ResolvedSigil(string Name, string? Scope, string Body);
public sealed record SigilResolutionResult(ResolvedSigil[] Resolved, string[] Unresolved);
public sealed record AgentDefinitionListItem(string Slug, string Name, string Description, string Adapter, int SkillCount);
public sealed record AgentDefinitionListResult(AgentDefinitionListItem[] Agents);
public sealed record AgentDefinitionShowResult(string Slug, string Name, string Description, string Adapter, string Prompt, string[] AttachedSkills);
public sealed record LaunchProfileListItem(string Slug, string Name, string Adapter, bool IsDefault, string? Model, string? Effort, int ArgCount, int EnvCount);
public sealed record LaunchProfileListResult(LaunchProfileListItem[] Profiles);
public sealed record AdapterEnvVarItem(string Key, string Value, bool Enabled, string? Id);
public sealed record AdapterEnvListResult(AdapterEnvVarItem[] Entries);
public sealed record ResolvedEnvVar(string Key, string Value);
public sealed record AdapterEnvResolveResult(ResolvedEnvVar[] Vars);
public sealed record AdapterEnvVar(string Key, string Value, bool Enabled = true, string? Id = null);
public sealed record HookStateResult(int Port, bool Running);
public sealed record PaneStateItem(string PaneId, string Adapter, string Status, int ActiveSubagents, System.DateTimeOffset LastEventAt);
public sealed record PaneStatesResult(PaneStateItem[] Panes);

public sealed record AgentMessageParams(string Target, string Body, string? FromPaneId, string? FromAdapter, string? FromName, bool NoFrame);
public sealed record AgentListDto(string PaneId, string Adapter, string? Name, string? Workspace, string? Room, string Status, string McpAccessScope);
public sealed record AgentListResult(System.Collections.Generic.IReadOnlyList<AgentListDto> Agents);

public sealed record ActivityCardDto(string PaneId, string Adapter, string? Name, string? Workspace, string? Room, string Status, string? StopReason, int ActiveSubagents, string? LastEvent, System.DateTimeOffset LastEventAt);
public sealed record ActivityListResult(System.Collections.Generic.IReadOnlyList<ActivityCardDto> Cards);

public sealed record SessionStateDto(string PaneId, string Adapter, string? SessionId, string Lifecycle, bool Resumable);
public sealed record SessionListResult(System.Collections.Generic.IReadOnlyList<SessionStateDto> Sessions);

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    GenerationMode = JsonSourceGenerationMode.Default)]
[JsonSerializable(typeof(ControlRequest))]
[JsonSerializable(typeof(ControlResponse))]
[JsonSerializable(typeof(ControlEvent))]
[JsonSerializable(typeof(ControlErrorFrame))]
[JsonSerializable(typeof(HelloParams))]
[JsonSerializable(typeof(HelloResult))]
[JsonSerializable(typeof(DaemonStatusResult))]
[JsonSerializable(typeof(PaneInfo[]))]
[JsonSerializable(typeof(PaneListResult))]
[JsonSerializable(typeof(SpawnParams))]
[JsonSerializable(typeof(SubscribeParams))]
[JsonSerializable(typeof(SubscribeResult))]
[JsonSerializable(typeof(WriteParams))]
[JsonSerializable(typeof(ResizeParams))]
[JsonSerializable(typeof(PaneInfo))]
[JsonSerializable(typeof(PaneWriteParams))]
[JsonSerializable(typeof(PaneRefParams))]
[JsonSerializable(typeof(LayoutMutateParams))]
[JsonSerializable(typeof(LayoutMutateResult))]
[JsonSerializable(typeof(SessionStateResult))]
[JsonSerializable(typeof(SearchParams))]
[JsonSerializable(typeof(SearchMatch))]
[JsonSerializable(typeof(SearchResult))]
[JsonSerializable(typeof(SkillIndexItem))]
[JsonSerializable(typeof(SkillsIndexResult))]
[JsonSerializable(typeof(ResolvedSigil))]
[JsonSerializable(typeof(SigilResolutionResult))]
[JsonSerializable(typeof(AgentDefinitionListItem))]
[JsonSerializable(typeof(AgentDefinitionListResult))]
[JsonSerializable(typeof(AgentDefinitionShowResult))]
[JsonSerializable(typeof(LaunchProfileListItem))]
[JsonSerializable(typeof(LaunchProfileListResult))]
[JsonSerializable(typeof(AdapterEnvVar))]
[JsonSerializable(typeof(List<AdapterEnvVar>))]
[JsonSerializable(typeof(AdapterEnvVarItem))]
[JsonSerializable(typeof(AdapterEnvListResult))]
[JsonSerializable(typeof(ResolvedEnvVar))]
[JsonSerializable(typeof(AdapterEnvResolveResult))]
[JsonSerializable(typeof(HookStateResult))]
[JsonSerializable(typeof(PaneStateItem))]
[JsonSerializable(typeof(PaneStatesResult))]
[JsonSerializable(typeof(AgentMessageParams))]
[JsonSerializable(typeof(AgentListDto))]
[JsonSerializable(typeof(AgentListResult))]
[JsonSerializable(typeof(ActivityCardDto))]
[JsonSerializable(typeof(ActivityListResult))]
[JsonSerializable(typeof(SessionStateDto))]
[JsonSerializable(typeof(SessionListResult))]
[JsonSerializable(typeof(JsonElement))]
public sealed partial class CoveJsonContext : JsonSerializerContext;
