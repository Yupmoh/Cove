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
public sealed record StateChangedEvent(string Uri);

public sealed record ControlErrorFrame(string Code, string Message, ulong? StreamId = null);

public sealed record HelloParams(int ProtocolVersion, string ClientKind, string ClientVersion, string Channel);
public sealed record HelloResult(int ProtocolVersion, string EngineVersion, int EnginePid, string Channel);

public sealed record DaemonStatusResult(int Pid, string Channel, string EngineVersion, int Connections, int Sessions, long UptimeSeconds);

public sealed record PaneInfo(string PaneId, string Command, int Cols, int Rows, bool Alive, string? Cwd = null, string? Title = null);

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
public sealed record PaneRenameParams(string PaneId, string Title);
public sealed record PaneReadParams(string PaneId, long Offset = 0, int MaxBytes = 65536);
public sealed record PaneReadResult(string DataBase64, long NextOffset, long Head);
public sealed record ConfigGetParams(string Key);
public sealed record ConfigGetResult(string Value);
public sealed record ConfigSetParams(string Key, string Value);
public sealed record ExtensionRunParams(string Command, string? Params = null);
public sealed record ExtensionRunResult(string Output);
public sealed record AttachRawParams(string Session);
public sealed record ExecuteCommandParams(string Command, System.Text.Json.JsonElement? Params = null);
public sealed record BackendState(string Version, string Mode, bool Headless);
public sealed record EmitEventParams(string Event, System.Text.Json.JsonElement? Payload = null);
public sealed record IpcEventEntry(string Event, long TimestampMs, System.Text.Json.JsonElement? Payload = null);
public sealed record IpcEventLog(IpcEventEntry[] Events);

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
public sealed record SkillsResolveManifestParams(string Name);
public sealed record SkillManifestResult(string Name, string Body, string Source);
public sealed record StateReadParams(string Scope, string Namespace, string Id);
public sealed record StateReadResult(bool Exists, string? Value);
public sealed record StateWriteParams(string Scope, string Namespace, string Id, string? Value);
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

public sealed record AgentMessageParams(string Target, string Body, string? FromPaneId, string? FromAdapter, string? FromName, bool NoFrame, int? SubmitPauseMs);
public sealed record AgentListDto(string PaneId, string Adapter, string? Name, string? Workspace, string? Room, string Status, string McpAccessScope);
public sealed record AgentListResult(System.Collections.Generic.IReadOnlyList<AgentListDto> Agents);

public sealed record ActivityCardDto(string PaneId, string Adapter, string? Name, string? Workspace, string? Room, string Status, string? StopReason, int ActiveSubagents, string? LastEvent, System.DateTimeOffset LastEventAt);
public sealed record ActivityListResult(System.Collections.Generic.IReadOnlyList<ActivityCardDto> Cards);

public sealed record SessionStateDto(string PaneId, string Adapter, string? SessionId, string Lifecycle, bool Resumable);
public sealed record SessionListResult(System.Collections.Generic.IReadOnlyList<SessionStateDto> Sessions);

public sealed record ReplayInfoDto(string Command, int? ExitCode, int? Signal);
public sealed record SpawnedPanesResult(System.Collections.Generic.IReadOnlyList<string> PaneIds);

public sealed record LaunchBuildParams(string Adapter, string ProfileSlug, bool Yolo, string? WorkingDir, string[] ExtraFlags, Dictionary<string, string> Env);
public sealed record LaunchResumeParams(string Adapter, string ProfileSlug, string SessionId, bool Yolo, string? WorkingDir, string[] ExtraFlags, Dictionary<string, string> Env);
public sealed record LaunchOverrideSaveParams(string PaneId, bool Yolo, string? WorkingDir, string[] ExtraFlags, Dictionary<string, string> Env);
public sealed record LaunchOverrideGetParams(string PaneId);
public sealed record ResumeCommandDto(string Command, string[] Args, string Cwd);
public sealed record LauncherOverridesDto(bool Yolo, string? WorkingDir, string[] ExtraFlags, Dictionary<string, string> Env);

public sealed record AdapterListItemDto(string Name, string DisplayName, string Accent, string Binary);
public sealed record AdapterListResult(System.Collections.Generic.IReadOnlyList<AdapterListItemDto> Adapters);
public sealed record RegistryEntryDto(string Name, string DisplayName, string Version, bool Official);
public sealed record RegistryFetchResult(RegistryEntryDto[] Adapters);
public sealed record NeedsInputSignalDto(string PaneId, string Adapter);
public sealed record OmniChatAppendParams(string PaneId, string Role, string Body);
public sealed record OmniChatHistoryParams(string PaneId);
public sealed record OmniChatHistoryResult(OmniChatMessageDto[] Messages);
public sealed record OmniChatMessageDto(string Role, string Body, System.DateTimeOffset SentAt);
public sealed record OmniChatClearParams(string PaneId);
public sealed record PaneScopeGetParams(string PaneId);
public sealed record PaneScopeSetParams(string PaneId, string Scope);
public sealed record PaneScopeResult(string PaneId, string Scope);
public sealed record LauncherOptionsParams(string Adapter);
public sealed record LauncherOptionsResponse(LauncherOptionDto[] Options);
public sealed record LauncherOptionDto(string Key, string Label, string Type, string? DefaultValueRaw, LauncherOptionChoiceDto[]? Choices);
public sealed record LauncherOptionChoiceDto(string Value, string? Label);

public sealed record ProtocolResolveParams(string Uri, string? FocusedPaneId, string? ActiveRoomId);
public sealed record ProtocolResolveResult(string Uri, System.Text.Json.JsonElement? Params);

public enum TaskPriority { Low, Medium, High, Critical }
public enum TaskSize { Xs, S, M, L, Xl }
public sealed record TaskCard
{
    public string Id { get; set; } = "";
    public required string Title { get; init; }
    public string Description { get; init; } = "";
    public string StatusId { get; set; } = "todo";
    public TaskPriority Priority { get; init; } = TaskPriority.Medium;
    public TaskSize Size { get; init; } = TaskSize.M;
    public string? Assignee { get; init; }
    public required string Source { get; init; }
    public required string WorkspaceId { get; init; }
    public int TaskNumber { get; init; }
    public string? CurrentPrimaryRunId { get; init; }
    public System.DateTimeOffset CreatedAt { get; init; }
    public System.DateTimeOffset UpdatedAt { get; init; }
    public string HumanId => $"COVE-{TaskNumber}";
}
public sealed record TaskCreateParams(string Title, string WorkspaceId, string Source, string? Description, string? Priority, string? Size, string? Assignee);
public sealed record TaskRefParams(string? Id, string? HumanId, string? WorkspaceId);
public sealed record TaskListParams(string WorkspaceId);
public sealed record TaskUpdateParams(string Id, string? Title, string? StatusId, string? Description, string? Assignee, string? Source);
public sealed record TaskListResult(System.Collections.Generic.IReadOnlyList<TaskCard> Cards);
public sealed record TaskPingParams(string Echo, string? Kind);
public sealed record TaskPingResult(string Echo, string? Kind, string Status);
public sealed record StatusInfo(string WorkspaceId, string Id, string Name, string HexColor, double Position, bool Hidden, bool IsLooping, bool IsInProgress, bool IsReview, bool IsCompletion);
public sealed record StatusListResult(System.Collections.Generic.IReadOnlyList<StatusInfo> Statuses);
public sealed record StatusListParams(string WorkspaceId);
public sealed record StatusCreateParams(string WorkspaceId, string Id, string Name, string HexColor, double Position);
public sealed record StatusRefParams(string WorkspaceId, string Id);
public sealed record StatusDeleteParams(string WorkspaceId, string Id, string? RehomeToStatusId);
public sealed record StatusReorderParams(string WorkspaceId, string[] OrderedIds);
public sealed record LabelInfo(string WorkspaceId, string Id, string Name, string HexColor, double Position);
public sealed record LabelListResult(System.Collections.Generic.IReadOnlyList<LabelInfo> Labels);
public sealed record LabelListParams(string WorkspaceId);
public sealed record LabelCreateParams(string WorkspaceId, string Id, string Name, string HexColor, double Position);
public sealed record LabelRefParams(string WorkspaceId, string Id);
public sealed record LabelAssignParams(string CardId, string LabelId);
public sealed record LabelReorderParams(string WorkspaceId, string[] OrderedIds);
public sealed record LabelFilterParams(string WorkspaceId, string LabelId);
public sealed record LabelFilterResult(System.Collections.Generic.IReadOnlyList<string> CardIds);
public sealed record CommentInfo(string Id, string CardId, string Kind, string Body, string Source, string CreatedAt);
public sealed record CommentListResult(System.Collections.Generic.IReadOnlyList<CommentInfo> Comments);
public sealed record CommentAddParams(string CardId, string Kind, string Body, string Source);
public sealed record CommentListParams(string CardId);
public sealed record CommentRefParams(string Id);
public sealed record CommentCountResult(int Count);
public sealed record LaunchConfigInfo(string? Adapter, string? ProfileSlug, string ExecutionMode, string? InProgressStatusId, string? ReviewStatusId, string? CompletionStatusId, string? MergeTarget, string? WorktreeBranchSource, string? WorktreeBranchName);
public sealed record LaunchConfigSetParams(string CardId, string? Adapter, string? ProfileSlug, string? ExecutionMode, string? InProgressStatusId, string? ReviewStatusId, string? CompletionStatusId, string? MergeTarget, string? WorktreeBranchSource, string? WorktreeBranchName);
public sealed record LaunchConfigGetParams(string CardId);
public sealed record LaunchConfigValidationResultDto(bool IsValid, string[] Errors);
public sealed record SkillSelectionDto(string Provenance, string Name, string Mode);
public sealed record TaskBindingInfo(string? AgentRef, string? ProfileSlug, SkillSelectionDto[] Skills);
public sealed record TaskBindingSetParams(string CardId, string? AgentRef, string? ProfileSlug, SkillSelectionDto[] Skills);
public sealed record TaskBindingGetParams(string CardId);
public sealed record TaskProfileResolveParams(string CardId);
public sealed record TaskProfilePayloadDto(string? AgentRef, string? ProfileSlug, SkillSelectionDto[] Skills);
public sealed record RunInfo(string Id, string CardId, string WorkspaceId, string RunFamilyId, string State, bool Backgrounded, string? LaunchProfileJson, string StartedAt, string? EndedAt, string CreatedAt);
public sealed record RunListResult(System.Collections.Generic.IReadOnlyList<RunInfo> Runs);
public sealed record RunListParams(string? TaskId, string? WorkspaceId, string? State);
public sealed record RunRefParams(string Id);
public sealed record RunCompleteParams(string Id, string? Source);
public sealed record RunSegmentInfo(string Id, string RunId, string? PaneId, string? AdapterSessionId, string StartedAt, string? EndedAt);
public sealed record RunSegmentListResult(System.Collections.Generic.IReadOnlyList<RunSegmentInfo> Segments);
public sealed record TaskLaunchParams(string CardId, string? ExecutionModeOverride);
public sealed record TaskLaunchResult(bool Success, string? RunId, string? Error, string ReachedStep);
public sealed record TaskSetInReviewParams(string? RunId, string? PaneId, string? WorkspaceId);
public sealed record TaskSetDoneParams(string? RunId, string? PaneId, string? WorkspaceId);
public sealed record TaskClaimParams(string CardId, string? PaneId);
public sealed record TaskClaimResult(bool Success, string? RunId, string? Error);
public sealed record RunResumeParams(string Id, string? PaneId, string? AdapterOverride);
public sealed record RunResumeResult(bool Success, string? NewSegmentId, string? Error, string Outcome);
public sealed record StatusSetHiddenParams(string WorkspaceId, string Id, bool Hidden);

public sealed record Note
{
    public string Id { get; set; } = "";
    public required string Title { get; init; }
    public string Content { get; init; } = "";
    public required string WorkspaceId { get; init; }
    public required string Source { get; init; }
    public string Kind { get; init; } = "markdown";
    public System.DateTimeOffset CreatedAt { get; init; }
    public System.DateTimeOffset UpdatedAt { get; init; }
}
public sealed record TimelineEntry
{
    public string Id { get; init; } = "";
    public required string WorkspaceId { get; init; }
    public required string Kind { get; init; }
    public required string Source { get; init; }
    public string? Scope { get; init; }
    public string? Summary { get; init; }
    public string? JsonPayload { get; init; }
    public System.DateTimeOffset Timestamp { get; init; }
}
public sealed record NoteCreateParams(string Title, string WorkspaceId, string Source, string? Content, string? Kind);
public sealed record NoteRefParams(string Id);
public sealed record NoteListParams(string WorkspaceId);
public sealed record NoteUpdateParams(string Id, string? Title, string? Content);
public sealed record NoteListResult(System.Collections.Generic.IReadOnlyList<Note> Notes);
public sealed record TimelineAppendParams(string WorkspaceId, string Kind, string Source, string? Scope, string? Summary);
public sealed record TimelineListParams(string WorkspaceId);
public sealed record TimelineListResult(System.Collections.Generic.IReadOnlyList<TimelineEntry> Entries);

public sealed record PaneTypeDto(string Name, string DisplayName, string ContentSource, bool IsDockable);
public sealed record PaneTypeListResult(System.Collections.Generic.IReadOnlyList<PaneTypeDto> PaneTypes);

public sealed record BrowserOpenParams(string PaneId, string Url);
public sealed record BrowserNavigateParams(string PaneId, string Url);
public sealed record BrowserPaneRefParams(string PaneId);
public sealed record BrowserPaneDto(string PaneId, string CurrentUrl, System.Collections.Generic.IReadOnlyList<string> History, int HistoryIndex, bool CanGoBack, bool CanGoForward);

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    GenerationMode = JsonSourceGenerationMode.Default)]
[JsonSerializable(typeof(ControlRequest))]
[JsonSerializable(typeof(ControlResponse))]
[JsonSerializable(typeof(ControlEvent))]
[JsonSerializable(typeof(StateChangedEvent))]
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
[JsonSerializable(typeof(PaneRenameParams))]
[JsonSerializable(typeof(PaneReadParams))]
[JsonSerializable(typeof(PaneReadResult))]
[JsonSerializable(typeof(ConfigGetParams))]
[JsonSerializable(typeof(ConfigGetResult))]
[JsonSerializable(typeof(ConfigSetParams))]
[JsonSerializable(typeof(ExtensionRunParams))]
[JsonSerializable(typeof(AttachRawParams))]
[JsonSerializable(typeof(ExtensionRunResult))]
[JsonSerializable(typeof(ExecuteCommandParams))]
[JsonSerializable(typeof(BackendState))]
[JsonSerializable(typeof(EmitEventParams))]
[JsonSerializable(typeof(IpcEventEntry))]
[JsonSerializable(typeof(IpcEventLog))]
[JsonSerializable(typeof(System.Collections.Generic.Dictionary<string, string>))]
[JsonSerializable(typeof(System.Collections.Generic.List<string>))]
[JsonSerializable(typeof(LayoutMutateParams))]
[JsonSerializable(typeof(LayoutMutateResult))]
[JsonSerializable(typeof(SessionStateResult))]
[JsonSerializable(typeof(SearchParams))]
[JsonSerializable(typeof(SearchMatch))]
[JsonSerializable(typeof(SearchResult))]
[JsonSerializable(typeof(SkillIndexItem))]
[JsonSerializable(typeof(StateReadParams))]
[JsonSerializable(typeof(StateReadResult))]
[JsonSerializable(typeof(StateWriteParams))]
[JsonSerializable(typeof(SkillsIndexResult))]
[JsonSerializable(typeof(SkillsResolveManifestParams))]
[JsonSerializable(typeof(SkillManifestResult))]
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
[JsonSerializable(typeof(ReplayInfoDto))]
[JsonSerializable(typeof(SpawnedPanesResult))]
[JsonSerializable(typeof(LaunchBuildParams))]
[JsonSerializable(typeof(LaunchResumeParams))]
[JsonSerializable(typeof(LaunchOverrideSaveParams))]
[JsonSerializable(typeof(LaunchOverrideGetParams))]
[JsonSerializable(typeof(ResumeCommandDto))]
[JsonSerializable(typeof(LauncherOverridesDto))]
[JsonSerializable(typeof(AdapterListResult))]
[JsonSerializable(typeof(RegistryFetchResult))]
[JsonSerializable(typeof(NeedsInputSignalDto))]
[JsonSerializable(typeof(LauncherOptionsParams))]
[JsonSerializable(typeof(LauncherOptionsResponse))]
[JsonSerializable(typeof(OmniChatAppendParams))]
[JsonSerializable(typeof(OmniChatHistoryParams))]
[JsonSerializable(typeof(OmniChatHistoryResult))]
[JsonSerializable(typeof(OmniChatClearParams))]
[JsonSerializable(typeof(PaneScopeGetParams))]
[JsonSerializable(typeof(PaneScopeSetParams))]
[JsonSerializable(typeof(PaneScopeResult))]
[JsonSerializable(typeof(ProtocolResolveParams))]
[JsonSerializable(typeof(ProtocolResolveResult))]
[JsonSerializable(typeof(TaskCard))]
[JsonSerializable(typeof(TaskCreateParams))]
[JsonSerializable(typeof(TaskRefParams))]
[JsonSerializable(typeof(TaskListParams))]
[JsonSerializable(typeof(TaskUpdateParams))]
[JsonSerializable(typeof(TaskListResult))]
[JsonSerializable(typeof(TaskPingParams))]
[JsonSerializable(typeof(TaskPingResult))]
[JsonSerializable(typeof(StatusInfo))]
[JsonSerializable(typeof(StatusListParams))]
[JsonSerializable(typeof(StatusListResult))]
[JsonSerializable(typeof(StatusCreateParams))]
[JsonSerializable(typeof(StatusRefParams))]
[JsonSerializable(typeof(StatusDeleteParams))]
[JsonSerializable(typeof(StatusReorderParams))]
[JsonSerializable(typeof(StatusSetHiddenParams))]
[JsonSerializable(typeof(LabelInfo))]
[JsonSerializable(typeof(LabelListParams))]
[JsonSerializable(typeof(LabelListResult))]
[JsonSerializable(typeof(LabelCreateParams))]
[JsonSerializable(typeof(LabelRefParams))]
[JsonSerializable(typeof(LabelAssignParams))]
[JsonSerializable(typeof(LabelReorderParams))]
[JsonSerializable(typeof(LabelFilterParams))]
[JsonSerializable(typeof(RunInfo))]
[JsonSerializable(typeof(RunListResult))]
[JsonSerializable(typeof(RunListParams))]
[JsonSerializable(typeof(TaskSetInReviewParams))]
[JsonSerializable(typeof(TaskSetDoneParams))]
[JsonSerializable(typeof(RunResumeParams))]
[JsonSerializable(typeof(RunResumeResult))]
[JsonSerializable(typeof(TaskClaimParams))]
[JsonSerializable(typeof(TaskClaimResult))]
[JsonSerializable(typeof(RunRefParams))]
[JsonSerializable(typeof(RunCompleteParams))]
[JsonSerializable(typeof(TaskLaunchParams))]
[JsonSerializable(typeof(TaskLaunchResult))]
[JsonSerializable(typeof(RunSegmentInfo))]
[JsonSerializable(typeof(RunSegmentListResult))]
[JsonSerializable(typeof(LabelFilterResult))]
[JsonSerializable(typeof(SkillSelectionDto))]
[JsonSerializable(typeof(TaskBindingInfo))]
[JsonSerializable(typeof(TaskBindingSetParams))]
[JsonSerializable(typeof(TaskBindingGetParams))]
[JsonSerializable(typeof(TaskProfileResolveParams))]
[JsonSerializable(typeof(TaskProfilePayloadDto))]
[JsonSerializable(typeof(CommentInfo))]
[JsonSerializable(typeof(CommentListResult))]
[JsonSerializable(typeof(LaunchConfigInfo))]
[JsonSerializable(typeof(LaunchConfigSetParams))]
[JsonSerializable(typeof(LaunchConfigGetParams))]
[JsonSerializable(typeof(LaunchConfigValidationResultDto))]
[JsonSerializable(typeof(CommentAddParams))]
[JsonSerializable(typeof(CommentListParams))]
[JsonSerializable(typeof(CommentRefParams))]
[JsonSerializable(typeof(CommentCountResult))]
[JsonSerializable(typeof(Note))]
[JsonSerializable(typeof(NoteCreateParams))]
[JsonSerializable(typeof(NoteRefParams))]
[JsonSerializable(typeof(NoteListParams))]
[JsonSerializable(typeof(NoteUpdateParams))]
[JsonSerializable(typeof(NoteListResult))]
[JsonSerializable(typeof(TimelineEntry))]
[JsonSerializable(typeof(TimelineAppendParams))]
[JsonSerializable(typeof(TimelineListParams))]
[JsonSerializable(typeof(TimelineListResult))]
[JsonSerializable(typeof(PaneTypeDto))]
[JsonSerializable(typeof(PaneTypeListResult))]
[JsonSerializable(typeof(BrowserOpenParams))]
[JsonSerializable(typeof(BrowserNavigateParams))]
[JsonSerializable(typeof(BrowserPaneRefParams))]
[JsonSerializable(typeof(BrowserPaneDto))]
[JsonSerializable(typeof(JsonElement))]
public sealed partial class CoveJsonContext : JsonSerializerContext;
