using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cove.Protocol;

public sealed record ControlRequest(
    string Id,
    string Uri,
    JsonElement? Params = null,
    string? Source = null,
    string? CallerNookId = null);

public sealed record ControlResponse(
    string Id,
    bool Ok,
    JsonElement? Data = null,
    ControlError? Error = null);

public sealed record ControlError(string Code, string Message);

public sealed record ControlEvent(string Channel, JsonElement Payload);
public sealed record StateChangedEvent(string Uri);
public sealed record WorkspaceChangedEvent(long Revision, string Uri);
public sealed record SessionRecentsChangedEvent(long Revision);
public sealed record ConfigChangedEvent(string Key);
public sealed record AgentChangedEvent(string NookId);

public sealed record ControlErrorFrame(string Code, string Message, ulong? StreamId = null);

public sealed record HelloParams(int ProtocolVersion, string ClientKind, string ClientVersion, string Channel, string? NookId = null, string? NookToken = null, string? ControlToken = null);
public sealed record HelloResult(int ProtocolVersion, string EngineVersion, int EnginePid, string Channel);

public sealed record DaemonStatusResult(int Pid, string Channel, string EngineVersion, int Connections, int Sessions, long UptimeSeconds);

public sealed record NookInfo(string NookId, string Command, int Cols, int Rows, bool Alive, string? Cwd = null, string? Title = null);

public sealed record NookListResult(NookInfo[] Nooks);

public sealed record SpawnParams(
    string? Command,
    string[] Args,
    string? Cwd = null,
    Dictionary<string, string>? Env = null,
    int Cols = 80,
    int Rows = 24,
    string? InheritCwdFrom = null,
    string? Adapter = null,
    string? AgentName = null,
    string? Bay = null,
    string? Shore = null,
    string McpAccessScope = "same-tab",
    bool McpVisible = true,
    string? SessionId = null,
    bool Yolo = false,
    string? ShellCommand = null);

public sealed record HookEmitParams(
    string Adapter,
    string Event,
    string? NookId,
    JsonElement Payload);

public sealed record DictationAudioPayload(
    byte[] Pcm,
    int SampleRate,
    int Offset = 0);

public sealed record DictationSessionParams(string SessionId);
public sealed record DictationPartialParams(
    string SessionId,
    DictationAudioPayload Audio);
public sealed record DictationStopParams(
    string SessionId,
    DictationAudioPayload Audio);
public sealed record DictationStatusResult(
    string State,
    bool ModelReady);
public sealed record DictationEnsureModelResult(bool Started);
public sealed record DictationModelReadyResult(bool Ready);
public sealed record DictationBeginResult(string SessionId);
public sealed record DictationTranscriptionResult(
    string Text,
    double AudioSeconds,
    long TranscribeMs);
public sealed record DictationPartialResult(string Text);
public sealed record DictationStateEvent(bool Recording);
public sealed record DictationProgressEvent(double Percent);
public sealed record DictationModelEvent(bool Ready);

public sealed record SubscribeParams(string NookId, ulong SinceOffset = 0);
public sealed record SubscribeResult(ulong StreamId, ulong BaseOffset, int Window, ulong ReplayUntilOffset = 0, string TerminalModePreambleBase64 = "", string TerminalCheckpointBase64 = "", int CheckpointCols = 0, int CheckpointRows = 0, bool AuthoritativeInitialResync = false);

public sealed record WriteParams(ulong StreamId, string DataBase64);
public sealed record ResizeParams(string NookId, int Cols, int Rows);

public sealed record NookWriteParams(string NookId, string DataBase64);
public sealed record NookRefParams(string NookId);
public sealed record WorkspaceContextParams(string? NookId = null);
public sealed record WorkspaceContextResult(
    string NookId,
    string? Adapter,
    string? SessionId,
    string? BayId,
    string? ShoreId,
    string? FocusedNookId,
    string ActiveBayId,
    string? ActiveShoreId,
    long LayoutRevision,
    string? Cwd,
    string EffectiveAccessScope);
public sealed record NookOpenParams(
    string NookType,
    string? Command,
    string[] Args,
    string? Cwd = null,
    string? RelativeToNookId = null,
    string Placement = "right",
    string? BayId = null,
    int Cols = 80,
    int Rows = 24,
    string? Url = null);
public sealed record NookOpenResult(
    string NookId,
    string NookType,
    string BayId,
    string ShoreId,
    string Placement);
public sealed record NookCloseResult(
    string NookId,
    string NookType,
    string BayId,
    string ShoreId);
public sealed record NookStackParams(
    string NookId,
    string Placement);
public sealed record NookStackResult(
    string NookId,
    string BayId,
    string ShoreId,
    string Placement,
    int Nooks);
public sealed record AgentLaunchParams(
    string Mode,
    string Adapter,
    string? Profile = null,
    string? SessionId = null,
    string? Cwd = null,
    string? RelativeToNookId = null,
    string Placement = "right",
    string? BayId = null,
    string? Name = null,
    bool Yolo = false,
    int Cols = 80,
    int Rows = 24,
    string AccessScope = "same-bay",
    string? Model = null,
    string? Effort = null);
public sealed record AgentLaunchResult(
    string NookId,
    string Adapter,
    string? SessionId,
    string BayId,
    string ShoreId,
    string Placement,
    bool Resumed);
public sealed record NookRestartParams(
    string NookId,
    string Mode,
    bool PreserveScrollback = true,
    string? Command = null,
    System.Collections.Generic.IReadOnlyList<string>? Args = null,
    string? Cwd = null,
    string ResumeFallback = "none");
public sealed record NookRestartResult(
    string NookId,
    string Mode,
    string Outcome,
    bool FallbackUsed,
    string? Adapter,
    string? SessionId,
    string? BayId,
    string? ShoreId,
    int PreservedScrollbackBytes);
public sealed record NookRenameParams(string NookId, string Title);
public sealed record NookReadParams(string NookId, long Offset = 0, int MaxBytes = 65536);
public sealed record NookCheckpointParams(string NookId, string DataBase64, long Offset, int Cols, int Rows, int ScrollbackLines);
public sealed record NookReadResult(string DataBase64, long NextOffset, long Head);
public sealed record ConfigGetParams(string Key);
public sealed record ConfigGetResult(string Value);
public sealed record ConfigSetParams(string Key, string Value);
public sealed record ConfigSchemaEntryDto(string Key, string Label, string Tab, string Control, string? Description, string Type, string[]? Options = null);
public sealed record ConfigSchemaResult(System.Collections.Generic.IReadOnlyList<ConfigSchemaEntryDto> Entries);
public sealed record ThemeDto(string Name, string Type, string TerminalBackground, string TerminalForeground, string ChromeSurface, string ChromeText, string ChromeAccent, string[]? Ansi = null);
public sealed record ThemeListResult(System.Collections.Generic.IReadOnlyList<ThemeDto> Themes);
public sealed record ThemeRefParams(string Name);
public sealed record ThemeSaveParams(string Name, string Type, string TerminalBackground, string TerminalForeground, string ChromeSurface, string ChromeText, string ChromeAccent, string[]? Ansi = null);
public sealed record ThemeActiveResult(ThemeDto? Theme);
public sealed record ThemeBuiltinResult(bool IsBuiltin);
public sealed record KeybindDto(string Chord, string ActionType, string Action, string? Description);
public sealed record KeybindListResult(System.Collections.Generic.IReadOnlyList<KeybindDto> Bindings, System.Collections.Generic.IReadOnlyList<string> Conflicts);
public sealed record KeybindSetParams(string Chord, string ActionType, string Action, string? Description = null);
public sealed record KeybindClearParams(string Chord);
public sealed record KeybindChordParams(string Chord);
public sealed record KeybindWarningDto(string Warning);
public sealed record KeybindSetResult(bool Success, KeybindWarningDto? Warning);
public sealed record KeybindConflictsResult(System.Collections.Generic.IReadOnlyList<string> Conflicts);
public sealed record KeybindReservedResult(bool IsReserved);
public sealed record ExtensionRunParams(string Command, string? Params = null);
public sealed record ExtensionRunResult(string Output);
public sealed record AttachRawParams(string Session);
public sealed record ExecuteCommandParams(string Command, System.Text.Json.JsonElement? Params = null);
public sealed record BackendState(string Version, string Mode, bool Headless);
public sealed record EmitEventParams(string Event, System.Text.Json.JsonElement? Payload = null);
public sealed record IpcEventEntry(string Event, long TimestampMs, System.Text.Json.JsonElement? Payload = null);
public sealed record IpcEventLog(IpcEventEntry[] Events);

public sealed record LayoutGetParams(string? BayId = null);
public sealed record LayoutMutateParams(string Op, string? ShoreId = null, string? TargetNookId = null, string? NewNookId = null, string? Orientation = null, string? Name = null, string? NookId = null, int Dir = 1, string? NookType = null, System.Collections.Generic.IReadOnlyList<string>? ShoreIds = null);
public sealed record LayoutMutateResult(string? ShoreId = null);
public sealed record SessionStateResult(string NookId, string Command, int Cols, int Rows, bool Alive, string? Cwd = null);
public sealed record SearchParams(string NookId, string Query, bool CaseSensitive = false);
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
public sealed record LaunchProfileDetail(string Slug, string Name, string Adapter, bool IsDefault, string? Model, string? Effort, string[] CliArgs, System.Collections.Generic.IReadOnlyDictionary<string, string> Env, System.Collections.Generic.IReadOnlyDictionary<string, bool> Permissions, string[] Skills, string? Agent, int SchemaVersion);
public sealed record LaunchProfileGetParams(string Adapter, string Slug);
public sealed record LaunchProfileCreateParams(string Adapter, string Slug, string Name, string? Model, string? Effort, string[]? CliArgs, Dictionary<string, string>? Env, Dictionary<string, bool>? Permissions, string[]? Skills, string? Agent, bool? IsDefault);
public sealed record LaunchProfileUpdateParams(string Adapter, string Slug, string? Name, string? Model, string? Effort, string[]? CliArgs, Dictionary<string, string>? Env, Dictionary<string, bool>? Permissions, string[]? Skills, string? Agent, bool? IsDefault);
public sealed record AdapterEnvVarItem(string Key, string Value, bool Enabled, string? Id);
public sealed record AdapterEnvListResult(AdapterEnvVarItem[] Entries);
public sealed record ResolvedEnvVar(string Key, string Value);
public sealed record AdapterEnvResolveResult(ResolvedEnvVar[] Vars);
public sealed record AdapterEnvVar(string Key, string Value, bool Enabled = true, string? Id = null);
public sealed record HookStateResult(int Port, bool Running);
public sealed record NookStateItem(string NookId, string Adapter, string Status, int ActiveSubagents, System.DateTimeOffset LastEventAt);
public sealed record NookStatesResult(NookStateItem[] Nooks);

public sealed record AgentMessageParams(string Target, string Body, string? FromNookId, string? FromAdapter, string? FromName, bool NoFrame, int? SubmitPauseMs);
public sealed record AgentListParams(string Scope = "same-tab");
public sealed record AgentListDto(string NookId, string Adapter, string? Name, string? Bay, string? Shore, string Status, string McpAccessScope);
public sealed record AgentListResult(System.Collections.Generic.IReadOnlyList<AgentListDto> Agents);

public sealed record ActivityCardDto(string NookId, string Adapter, string? Name, string? Bay, string? Shore, string Status, string? StopReason, int ActiveSubagents, string? LastEvent, System.DateTimeOffset LastEventAt);
public sealed record ActivityListResult(System.Collections.Generic.IReadOnlyList<ActivityCardDto> Cards);
public sealed record ActivityAcknowledgeParams(string NookId);
public sealed record ActivityAcknowledgeResult(bool Acknowledged);

public sealed record SessionStateDto(string NookId, string Adapter, string? SessionId, string Lifecycle, bool Resumable);
public sealed record SessionListResult(System.Collections.Generic.IReadOnlyList<SessionStateDto> Sessions);

public sealed record ReplayInfoDto(string Command, int? ExitCode, int? Signal);
public sealed record SpawnedNooksResult(System.Collections.Generic.IReadOnlyList<string> NookIds);

public sealed record LaunchBuildParams(string Adapter, string ProfileSlug, bool Yolo, string? WorkingDir, string[] ExtraFlags, Dictionary<string, string> Env, string? Model = null, string? Effort = null);
public sealed record LaunchResumeParams(string Adapter, string ProfileSlug, string SessionId, bool Yolo, string? WorkingDir, string[] ExtraFlags, Dictionary<string, string> Env);
public sealed record LaunchOverrideSaveParams(string NookId, bool Yolo, string? WorkingDir, string[] ExtraFlags, Dictionary<string, string> Env, string? Model = null, string? Effort = null);
public sealed record LaunchOverrideGetParams(string NookId);
public sealed record ResumeCommandDto(string Command, string[] Args, string Cwd);
public sealed record LauncherOverridesDto(bool Yolo, string? WorkingDir, string[] ExtraFlags, Dictionary<string, string> Env, string? Model = null, string? Effort = null);

public sealed record AdapterListItemDto(string Name, string DisplayName, string Accent, string Binary, string? Status = null, string? Version = null, string? BinaryPath = null, string? UpdateCommand = null, string? UninstallCommand = null, string? InstallCommand = null, string? Description = null);
public sealed record AdapterListResult(System.Collections.Generic.IReadOnlyList<AdapterListItemDto> Adapters);
public sealed record HarnessUpdateDto(string Name, string DisplayName, string InstalledVersion, string LatestVersion, string? UpdateCommand);
public sealed record HarnessUpdatesResult(System.Collections.Generic.IReadOnlyList<HarnessUpdateDto> Updates);
public sealed record RegistryEntryDto(string Name, string DisplayName, string Version, bool Official);
public sealed record RegistryFetchResult(RegistryEntryDto[] Adapters);

public sealed record HandoffCheckpointDto(string DataBase64, long Offset, int Cols, int Rows, int ScrollbackLines, string ModeSupplement);
public sealed record HandoffNookRecord(
    string NookId,
    int Pid,
    string Command,
    string[] Args,
    string SpawnCwd,
    string? Cwd,
    int Cols,
    int Rows,
    string? Title,
    string? Adapter,
    string? AgentName,
    long RingHead,
    int RingLength,
    string? SessionId,
    string? HookStatus,
    HandoffCheckpointDto? Checkpoint,
    string NookToken);
public sealed record HandoffBrowserNookDto(
    string NookId,
    string CurrentUrl,
    string[] History,
    int HistoryIndex);
public sealed record HandoffBeginResult(
    int NookCount,
    string SocketPath,
    System.Collections.Generic.IReadOnlyList<HandoffBrowserNookDto>? BrowserNooks = null);

public sealed record ToolsRetentionDto(bool Present, bool Editable, bool Hidden, string? Value, string? Recommended);
public sealed record ToolsAdapterDto(
    string Name,
    string DisplayName,
    string Accent,
    string Binary,
    string? Status,
    string? Version,
    string? BinaryPath,
    string? IconSvg,
    string InstallHint,
    bool Bundled,
    bool Removable,
    ToolsRetentionDto Retention);
public sealed record ToolsListResult(System.Collections.Generic.IReadOnlyList<ToolsAdapterDto> Adapters);
public sealed record AdapterNameParams(string Name);
public sealed record AdapterInstallLocalParams(string Path);
public sealed record AdapterInstallLocalResult(string Name);
public sealed record AdapterRemoveParams(string Name, bool PurgeSessions = false);
public sealed record AdapterRemoveResult(string Name, int PurgedSessions);
public sealed record AdapterRetentionSetParams(string Name, string Value);
public sealed record NeedsInputSignalDto(string NookId, string Adapter);
public sealed record NotificationDeliverDto(string Id, string Title, string Body, string NookId);
public sealed record OmniChatAppendParams(string NookId, string Role, string Body);
public sealed record OmniChatHistoryParams(string NookId);
public sealed record OmniChatHistoryResult(OmniChatMessageDto[] Messages);
public sealed record OmniChatMessageDto(string Role, string Body, System.DateTimeOffset SentAt);
public sealed record OmniChatClearParams(string NookId);
public sealed record NookScopeGetParams(string NookId);
public sealed record NookScopeSetParams(string NookId, string Scope);
public sealed record NookScopeResult(string NookId, string Scope);
public sealed record LauncherOptionsParams(string Adapter);
public sealed record LauncherOptionsResponse(LauncherOptionDto[] Options, LauncherSuggestedFlagDto[] SuggestedFlags);
public sealed record LauncherOptionDto(string Key, string Label, string Type, string? DefaultValueRaw, LauncherOptionChoiceDto[]? Choices);
public sealed record LauncherOptionChoiceDto(string Value, string? Label);
public sealed record LauncherSuggestedFlagDto(string Flag, string? Description, string[]? Values);

public sealed record ProtocolResolveParams(string Uri, string? FocusedNookId, string? ActiveShoreId);
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
    public required string BayId { get; init; }
    public int TaskNumber { get; init; }
    public string? CurrentPrimaryRunId { get; init; }
    public System.DateTimeOffset CreatedAt { get; init; }
    public System.DateTimeOffset UpdatedAt { get; init; }
    public string HumanId => $"COVE-{TaskNumber}";
}
public sealed record TaskCreateParams(string Title, string BayId, string Source, string? Description, string? Priority, string? Size, string? Assignee);
public sealed record TaskRefParams(string? Id, string? HumanId, string? BayId);
public sealed record TaskListParams(string BayId);
public sealed record TaskUpdateParams(string Id, string? Title, string? StatusId, string? Description, string? Assignee, string? Source);
public sealed record TaskListResult(System.Collections.Generic.IReadOnlyList<TaskCard> Cards);
public sealed record TaskPingParams(string Echo, string? Kind);
public sealed record TaskPingResult(string Echo, string? Kind, string Status);
public sealed record StatusInfo(string BayId, string Id, string Name, string HexColor, double Position, bool Hidden, bool IsLooping, bool IsInProgress, bool IsReview, bool IsCompletion);
public sealed record StatusListResult(System.Collections.Generic.IReadOnlyList<StatusInfo> Statuses);
public sealed record StatusListParams(string BayId);
public sealed record StatusCreateParams(string BayId, string Id, string Name, string HexColor, double Position);
public sealed record StatusRefParams(string BayId, string Id);
public sealed record StatusDeleteParams(string BayId, string Id, string? RehomeToStatusId);
public sealed record StatusReorderParams(string BayId, string[] OrderedIds);
public sealed record LabelInfo(string BayId, string Id, string Name, string HexColor, double Position);
public sealed record LabelListResult(System.Collections.Generic.IReadOnlyList<LabelInfo> Labels);
public sealed record LabelListParams(string BayId);
public sealed record LabelCreateParams(string BayId, string Id, string Name, string HexColor, double Position);
public sealed record LabelRefParams(string BayId, string Id);
public sealed record LabelAssignParams(string CardId, string LabelId);
public sealed record LabelReorderParams(string BayId, string[] OrderedIds);
public sealed record LabelFilterParams(string BayId, string LabelId);
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
public sealed record RunInfo(string Id, string CardId, string BayId, string RunFamilyId, string State, bool Backgrounded, string? LaunchProfileJson, string? PendingPrompt, string StartedAt, string? EndedAt, string CreatedAt);
public sealed record RunListResult(System.Collections.Generic.IReadOnlyList<RunInfo> Runs);
public sealed record RunListParams(string? TaskId, string? BayId, string? State);
public sealed record RunRefParams(string Id);
public sealed record RunCompleteParams(string Id, string? Source);
public sealed record RunSegmentInfo(string Id, string RunId, string? NookId, string? AdapterSessionId, string StartedAt, string? EndedAt);
public sealed record RunSegmentListResult(System.Collections.Generic.IReadOnlyList<RunSegmentInfo> Segments);
public sealed record TaskLaunchParams(string CardId, string? ExecutionModeOverride);
public sealed record TaskLaunchResult(bool Success, string? RunId, string? Error, string ReachedStep);
public sealed record TaskSetInReviewParams(string? RunId, string? NookId, string? BayId);
public sealed record TaskSetDoneParams(string? RunId, string? NookId, string? BayId);
public sealed record TaskClaimParams(string CardId, string? NookId);
public sealed record TaskClaimResult(bool Success, string? RunId, string? Error);
public sealed record RunResumeParams(string Id, string? NookId, string? AdapterOverride);
public sealed record RunResumeResult(bool Success, string? NewSegmentId, string? Error, string Outcome);
public sealed record RunSetPendingPromptParams(string Id, string? Prompt);
public sealed record ScheduleSetRouteParams(string CardId, string TriggerKind, string? Cron, string? Tz, string? At, string? CompletionRule, string? MarkDoneBy, bool? BlockOverlap, string? HomeStatusId);
public sealed record ScheduleGetParams(string CardId);
public sealed record ScheduleInfo(string CardId, string TriggerKind, string? Cron, string? Tz, string? At, string CompletionRule, string MarkDoneBy, bool BlockOverlap, string? HomeStatusId, bool Paused, bool SkipNext, string? NextFireAt, string? LastFiredAt, string Mode);
public sealed record ScheduleUpdateStateParams(string CardId, bool? Paused, bool? SkipNext);
public sealed record ScheduleValidationResultDto(bool IsValid, string[] Errors, string? NextFireAt);
public sealed record RunNowParams(string CardId);
public sealed record RunNowResult(bool Success, string? RunId, string? Error);
public sealed record RepeatContinueParams(string CardId);
public sealed record RepeatFinishParams(string CardId);
public sealed record TaskBoardExportParams(string ExportPath, int BayCount);
public sealed record TaskBoardExportResultDto(bool Success, string? ExportPath, string? ExportedAt, int SchemaVersion, int BayCount, string? Error);
public sealed record TaskBoardDiffParams(string ImportPath);
public sealed record TaskBoardDiffResultDto(bool Success, string[] Diffs, string? Error);
public sealed record StatusSetHiddenParams(string BayId, string Id, bool Hidden);

public sealed record Note
{
    public string Id { get; set; } = "";
    public required string Title { get; init; }
    public string Content { get; init; } = "";
    public required string BayId { get; init; }
    public required string Source { get; init; }
    public string Kind { get; init; } = "markdown";
    public System.DateTimeOffset CreatedAt { get; init; }
    public System.DateTimeOffset UpdatedAt { get; init; }
}
public sealed record TimelineEntry
{
    public string Id { get; init; } = "";
    public required string BayId { get; init; }
    public required string Kind { get; init; }
    public required string Source { get; init; }
    public string? Scope { get; init; }
    public string? Summary { get; init; }
    public string? JsonPayload { get; init; }
    public System.Collections.Generic.IReadOnlyList<string>? Tags { get; init; }
    public System.DateTimeOffset Timestamp { get; init; }
}
public sealed record NoteCreateParams(string Title, string BayId, string Source, string? Content, string? Kind);
public sealed record TimelineAppendParams(string BayId, string Kind, string Source, string? Scope, string? Summary, System.Collections.Generic.IReadOnlyList<string>? Tags);
public sealed record NoteRefParams(string Id);
public sealed record NoteListParams(string BayId);
public sealed record NoteUpdateParams(string Id, string? Title, string? Content);
public sealed record NoteListResult(System.Collections.Generic.IReadOnlyList<Note> Notes);
public sealed record NoteSearchParams(string BayId, string Query, int? Limit);
public sealed record NoteSearchResult(System.Collections.Generic.IReadOnlyList<Note> Notes);
public sealed record NoteReadParams(string BayId, string Id, string? Format);
public sealed record NoteReadResult(string Id, string Title, string Content, string Kind, string? Format);
public sealed record NoteWriteParams(string BayId, string Id, string? Title, string? Content, string? Kind);
public sealed record NoteHistoryParams(string BayId, string Id);
public sealed record NoteHistoryEntry(string Sha, string Message, System.DateTimeOffset Timestamp);
public sealed record NoteHistoryResult(System.Collections.Generic.IReadOnlyList<NoteHistoryEntry> Entries);
public sealed record NoteMediaSaveParams(string BayId, string Id, string FileName, string Base64Data);
public sealed record NoteMediaSaveResult(string MediaPath);
public sealed record NoteGetStateParams(string BayId, string Id);
public sealed record NoteGetStateResult(string? State);
public sealed record NoteSaveStateParams(string BayId, string Id, string StateJson);
public sealed record CanvasActionParams(string Action, string? TargetNook, string? Uri, string ActionId, string? Payload, System.Text.Json.JsonElement? State);
public sealed record CanvasActionResult(bool Dispatched, string? TargetNookId, string? ResolvedUri);
public sealed record MemoryAddParams(string BayId, string Kind, string Content, double? Confidence, string? Audience);
public sealed record MemorySearchParams(string BayId, string Query, int? Limit);
public sealed record MemorySearchResult(System.Collections.Generic.IReadOnlyList<RankedFactDto> Facts);
public sealed record RankedFactDto(string Id, string Kind, string Content, double Score, string? Snippet);
public sealed record MemoryRecallParams(string BayId, string Query, int? Limit);
public sealed record MemoryRecallResult(System.Collections.Generic.IReadOnlyList<RecallPreviewDto> Previews);
public sealed record RecallPreviewDto(string Id, string Kind, string Preview, double Score, string HowLongAgo);
public sealed record MemoryShowParams(string BayId, string Id);
public sealed record MemorySupersedeParams(string BayId, string OldFactId, string Kind, string Content, double? Confidence);
public sealed record MemoryReindexParams(string BayId);
public sealed record MemoryConsolidateParams(string BayId, bool DryRun);
public sealed record MemoryConsolidateResult(int Candidates);
public sealed record MemoryProposeParams(string BayId, string Kind, string Content);
public sealed record MemoryProposalTransitionParams(string ProposalId, string State);
public sealed record EditsFindParams(string FilePath, int? Limit);
public sealed record EditsFindResult(System.Collections.Generic.IReadOnlyList<EditRecordDto> Edits);
public sealed record EditRecordDto(string SessionId, string FilePath, string? Tool, string? Op, string OccurredAt, string? EditSummary);
public sealed record VaultSearchParams(string BayId, string Query, int? Limit);
public sealed record VaultSearchResult(System.Collections.Generic.IReadOnlyList<SessionCorpusEntryDto> Entries);
public sealed record SessionCorpusEntryDto(string Id, string BayId, string Adapter, string StartedAt, string? EndedAt, string? ExtractorVersion);
public sealed record VaultResumeParams(string Adapter, string SessionId, string Cwd, bool Yolo = false);
public sealed record VaultResumeResult(bool Ok, string Adapter, string[] Command, string Cwd, string Fallback, string? Error);
public sealed record VaultSetSettingParams(string BayId, string Key, string Value);
public sealed record VaultReindexParams(string BayId);
public sealed record LibraryListParams(string BayId, string? Kind);
public sealed record LibraryListResult(System.Collections.Generic.IReadOnlyList<LibraryEntryDto> Entries);
public sealed record LibraryEntryDto(string Id, string BayId, string NookId, string NookType, string? Title, string? StateJson, string? Scrollback, string Kind, string CapturedAt);
public sealed record LibraryMaterializeParams(string BayId, string EntryId);
public sealed record Fact
{
    public string Id { get; init; } = "";
    public required string BayId { get; init; }
    public required string Kind { get; init; }
    public required string Content { get; init; }
    public double Confidence { get; init; } = 0.5;
    public int AccessCount { get; init; }
    public string? Audience { get; init; }
    public string? Locus { get; init; }
    public string? FilePath { get; init; }
    public string? SupersededBy { get; init; }
    public System.DateTimeOffset CreatedAt { get; init; }
    public System.DateTimeOffset UpdatedAt { get; init; }
}
public sealed record Proposal(string Id, string BayId, string Kind, string Content, string State, System.DateTimeOffset CreatedAt);
public sealed record TimelineListParams(string BayId);
public sealed record TimelineListResult(System.Collections.Generic.IReadOnlyList<TimelineEntry> Entries);
public sealed record KnowledgePingParams(string? Echo);
public sealed record KnowledgePingResult(string Pong, string? Echo);
public sealed record BlackboardPostParams(string BayId, string Kind, string Audience, string Content, string? RefId, int? TtlSeconds);
public sealed record BlackboardShowParams(string BayId, string? Audience);
public sealed record BlackboardShowResult(System.Collections.Generic.IReadOnlyList<BlackboardPost> Posts);
public sealed record BlackboardPost
{
    public string Id { get; init; } = "";
    public required string BayId { get; init; }
    public required string Kind { get; init; }
    public required string Audience { get; init; }
    public required string Content { get; init; }
    public string? RefId { get; init; }
    public System.DateTimeOffset? ExpiresAt { get; init; }
    public System.DateTimeOffset CreatedAt { get; init; }
}

public sealed record NookTypeDto(string Name, string DisplayName, string ContentSource, bool IsDockable);
public sealed record NookTypeListResult(System.Collections.Generic.IReadOnlyList<NookTypeDto> NookTypes);

public sealed record BrowserOpenParams(string NookId, string Url);
public sealed record BrowserNavigateParams(string NookId, string Url);
public sealed record BrowserNookRefParams(string NookId);
public sealed record BrowserNookDto(string NookId, string CurrentUrl, System.Collections.Generic.IReadOnlyList<string> History, int HistoryIndex, bool CanGoBack, bool CanGoForward);
public sealed record BrowserCreateParams(string Url, string? Title = null, string? BayId = null);
public sealed record BrowserAutomationExecEvent(string RequestId, string NookId, string Kind, string? Ref = null, string? Value = null, string? Js = null);
public sealed record BrowserAutomationResultParams(string RequestId, string ResultJson);
public sealed record BrowserAutomationSnapshotParams(string NookId);
public sealed record BrowserAutomationClickParams(string NookId, string Ref);
public sealed record BrowserAutomationFillParams(string NookId, string Ref, string Value);
public sealed record BrowserAutomationEvalParams(string NookId, string Js);
public sealed record BrowserScreenshotParams(string NookId);
public sealed record BrowserSetUserAgentParams(string NookId, string UserAgent);
public sealed record BrowserAutomationClearParams(string NookId, string Ref);
public sealed record BrowserAutomationTypeParams(string NookId, string Ref, string Text);
public sealed record BrowserAutomationPressParams(string NookId, string Ref, string Key);
public sealed record BrowserAutomationSelectParams(string NookId, string Ref, string Value);
public sealed record BrowserAutomationScrollParams(string NookId, string? Ref = null, int? X = null, int? Y = null);
public sealed record BrowserAutomationWaitParams(string NookId, string? Ref = null, string? Text = null, int? TimeoutMs = null);
public sealed record BrowserAutomationGetParams(string NookId, string Ref, string Prop);
public sealed record BrowserAutomationIsParams(string NookId, string Ref, string State);
public sealed record BrowserScrollValue(int? X, int? Y);
public sealed record BrowserWaitValue(string? Text, int? TimeoutMs);
public sealed record ReviewAddCommentParams(string CommitSha, string FilePath, int Line, string Author, string Body, string? ParentId);
public sealed record ReviewListCommentsParams(string CommitSha, string? FilePath, string? State);
public sealed record ReviewTransitionParams(string CommentId, string Actor);
public sealed record ReviewReAnchorParams(string CommentId, int NewLine);
public sealed record ReviewTelemetryParams(string CommitSha, string SessionId, string Adapter, int FilesTouched);
public sealed record ReviewCommentDto(string Id, string RootId, string? ParentId, string CommitSha, string FilePath, int Line, string Author, string Body, string State, string CreatedAt, string? OrphanedAt, string? HunkId, string? ContextHash);
public sealed record ReviewListCommentsResult(System.Collections.Generic.IReadOnlyList<ReviewCommentDto> Comments);
public sealed record ReviewAuditDto(string Id, string CommentId, string FromState, string ToState, string Actor, string At, string? Note);
public sealed record ReviewAuditResult(System.Collections.Generic.IReadOnlyList<ReviewAuditDto> Entries);
public sealed record ReviewTelemetryDto(string SessionId, string Adapter, int FilesTouched);
public sealed record ReviewTelemetryResult(System.Collections.Generic.IReadOnlyList<ReviewTelemetryDto> Entries);
public sealed record AttributionRecordParams(string SessionId, string ToolUseId, string FilePath, int StartLine, int EndLine);
public sealed record AttributionFindByLineParams(string FilePath, int Line);
public sealed record AttributionFindByRangeParams(string FilePath, int StartLine, int EndLine);
public sealed record AttributionFindByToolUseParams(string ToolUseId);
public sealed record AttributionEntryDto(string Id, string SessionId, string ToolUseId, string FilePath, int StartLine, int EndLine, string At);
public sealed record AttributionListResult(System.Collections.Generic.IReadOnlyList<AttributionEntryDto> Entries);
public sealed record ReviewDispatchParams(string TargetNookId, string BayId, string SessionId, string? TaskRunId, string Message, string? CommitSha);
public sealed record ReviewDispatchResultDto(string DispatchId, string TargetNookId, string SessionId, string? TaskRunId, string DispatchedAt);
public sealed record DiagnosticsStatusResult(bool Enabled, bool WebInspectorOptIn, int MaxSnapshots, double SnapshotIntervalSeconds, int SnapshotCount);
public sealed record DiagnosticsSnapshotTakeParams(int? ActiveNooks = null, int? ActiveBays = null, int? ActiveAgents = null);
public sealed record DiagnosticsExportParams(string? Path = null);
public sealed record DiagnosticsExportResult(string Path);
public sealed record PerfBundleCreateParams(string? TracePath = null);
public sealed record PerfBundleDeleteParams(string BundlePath);
public sealed record PerfBundleDto(string Id, string BundlePath, string CreatedAt, long SizeBytes, int SnapshotCount, bool ContainsTrace);
public sealed record PerfBundleListResult(System.Collections.Generic.IReadOnlyList<PerfBundleDto> Bundles);
public sealed record DirectoryListParams(string Path, int? Cap = null);
public sealed record DirectoryEntryDto(string Name, bool IsDir);
public sealed record DirectoryListResult(System.Collections.Generic.IReadOnlyList<DirectoryEntryDto> Entries, bool Truncated, string? Error);
public sealed record GitSummaryParams(string Path);
public sealed record GitSummaryFileDto(string Path, string Status);
public sealed record GitSummaryResult(bool Ok, string Branch, int Ahead, int Behind, int Dirty, System.Collections.Generic.IReadOnlyList<GitSummaryFileDto> Files, string? Error);
public sealed record FeedbackSaveParams(string Json, string Slug);
public sealed record FeedbackSaveResult(string Path);
public sealed record PerformanceResultSaveParams(string Json, string Markdown);
public sealed record PerformanceResultSaveResult(string Directory);

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    GenerationMode = JsonSourceGenerationMode.Default)]
[JsonSerializable(typeof(ControlRequest))]
[JsonSerializable(typeof(ControlResponse))]
[JsonSerializable(typeof(ControlEvent))]
[JsonSerializable(typeof(StateChangedEvent))]
[JsonSerializable(typeof(WorkspaceChangedEvent))]
[JsonSerializable(typeof(SessionRecentsChangedEvent))]
[JsonSerializable(typeof(ConfigChangedEvent))]
[JsonSerializable(typeof(AgentChangedEvent))]
[JsonSerializable(typeof(ControlErrorFrame))]
[JsonSerializable(typeof(DaemonStatusResult))]
[JsonSerializable(typeof(HelloParams))]
[JsonSerializable(typeof(HelloResult))]
[JsonSerializable(typeof(NookInfo[]))]
[JsonSerializable(typeof(NookListResult))]
[JsonSerializable(typeof(SpawnParams))]
[JsonSerializable(typeof(WorkspaceContextParams))]
[JsonSerializable(typeof(WorkspaceContextResult))]
[JsonSerializable(typeof(NookOpenParams))]
[JsonSerializable(typeof(NookOpenResult))]
[JsonSerializable(typeof(NookCloseResult))]
[JsonSerializable(typeof(NookStackParams))]
[JsonSerializable(typeof(NookStackResult))]
[JsonSerializable(typeof(AgentLaunchParams))]
[JsonSerializable(typeof(AgentLaunchResult))]
[JsonSerializable(typeof(NookRestartParams))]
[JsonSerializable(typeof(NookRestartResult))]
[JsonSerializable(typeof(HookEmitParams))]
[JsonSerializable(typeof(DictationAudioPayload))]
[JsonSerializable(typeof(DictationSessionParams))]
[JsonSerializable(typeof(DictationPartialParams))]
[JsonSerializable(typeof(DictationStopParams))]
[JsonSerializable(typeof(DictationStatusResult))]
[JsonSerializable(typeof(DictationEnsureModelResult))]
[JsonSerializable(typeof(DictationModelReadyResult))]
[JsonSerializable(typeof(DictationBeginResult))]
[JsonSerializable(typeof(DictationTranscriptionResult))]
[JsonSerializable(typeof(DictationPartialResult))]
[JsonSerializable(typeof(DictationStateEvent))]
[JsonSerializable(typeof(DictationProgressEvent))]
[JsonSerializable(typeof(DictationModelEvent))]
[JsonSerializable(typeof(SubscribeParams))]
[JsonSerializable(typeof(SubscribeResult))]
[JsonSerializable(typeof(WriteParams))]
[JsonSerializable(typeof(ResizeParams))]
[JsonSerializable(typeof(NookInfo))]
[JsonSerializable(typeof(NookWriteParams))]
[JsonSerializable(typeof(KeybindDto))]
[JsonSerializable(typeof(KeybindListResult))]
[JsonSerializable(typeof(KeybindSetParams))]
[JsonSerializable(typeof(KeybindClearParams))]
[JsonSerializable(typeof(KeybindChordParams))]
[JsonSerializable(typeof(KeybindWarningDto))]
[JsonSerializable(typeof(KeybindSetResult))]
[JsonSerializable(typeof(KeybindConflictsResult))]
[JsonSerializable(typeof(KeybindReservedResult))]
[JsonSerializable(typeof(NookRefParams))]
[JsonSerializable(typeof(NookRenameParams))]
[JsonSerializable(typeof(NookReadParams))]
[JsonSerializable(typeof(NookCheckpointParams))]
[JsonSerializable(typeof(ThemeDto))]
[JsonSerializable(typeof(ThemeListResult))]
[JsonSerializable(typeof(ThemeRefParams))]
[JsonSerializable(typeof(ThemeSaveParams))]
[JsonSerializable(typeof(ThemeActiveResult))]
[JsonSerializable(typeof(ThemeBuiltinResult))]
[JsonSerializable(typeof(NookReadResult))]
[JsonSerializable(typeof(ConfigGetParams))]
[JsonSerializable(typeof(ConfigSchemaEntryDto))]
[JsonSerializable(typeof(ConfigSchemaResult))]
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
[JsonSerializable(typeof(LayoutGetParams))]
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
[JsonSerializable(typeof(LaunchProfileDetail))]
[JsonSerializable(typeof(LaunchProfileGetParams))]
[JsonSerializable(typeof(LaunchProfileCreateParams))]
[JsonSerializable(typeof(LaunchProfileUpdateParams))]
[JsonSerializable(typeof(AdapterEnvVar))]
[JsonSerializable(typeof(List<AdapterEnvVar>))]
[JsonSerializable(typeof(AdapterEnvVarItem))]
[JsonSerializable(typeof(AdapterEnvListResult))]
[JsonSerializable(typeof(ResolvedEnvVar))]
[JsonSerializable(typeof(AdapterEnvResolveResult))]
[JsonSerializable(typeof(HookStateResult))]
[JsonSerializable(typeof(NookStateItem))]
[JsonSerializable(typeof(NookStatesResult))]
[JsonSerializable(typeof(AgentMessageParams))]
[JsonSerializable(typeof(AgentListParams))]
[JsonSerializable(typeof(AgentListResult))]
[JsonSerializable(typeof(AgentListResult))]
[JsonSerializable(typeof(ActivityCardDto))]
[JsonSerializable(typeof(ActivityListResult))]
[JsonSerializable(typeof(ActivityAcknowledgeParams))]
[JsonSerializable(typeof(ActivityAcknowledgeResult))]
[JsonSerializable(typeof(SessionStateDto))]
[JsonSerializable(typeof(HandoffCheckpointDto))]
[JsonSerializable(typeof(HandoffNookRecord))]
[JsonSerializable(typeof(HandoffBrowserNookDto))]
[JsonSerializable(typeof(HandoffBeginResult))]
[JsonSerializable(typeof(SessionListResult))]
[JsonSerializable(typeof(ReplayInfoDto))]
[JsonSerializable(typeof(SpawnedNooksResult))]
[JsonSerializable(typeof(LaunchBuildParams))]
[JsonSerializable(typeof(LaunchResumeParams))]
[JsonSerializable(typeof(LaunchOverrideSaveParams))]
[JsonSerializable(typeof(LaunchOverrideGetParams))]
[JsonSerializable(typeof(ResumeCommandDto))]
[JsonSerializable(typeof(LauncherOverridesDto))]
[JsonSerializable(typeof(AdapterListResult))]
[JsonSerializable(typeof(HarnessUpdatesResult))]
[JsonSerializable(typeof(RegistryFetchResult))]
[JsonSerializable(typeof(ToolsListResult))]
[JsonSerializable(typeof(ToolsAdapterDto))]
[JsonSerializable(typeof(ToolsRetentionDto))]
[JsonSerializable(typeof(AdapterNameParams))]
[JsonSerializable(typeof(AdapterInstallLocalParams))]
[JsonSerializable(typeof(AdapterInstallLocalResult))]
[JsonSerializable(typeof(AdapterRemoveParams))]
[JsonSerializable(typeof(AdapterRemoveResult))]
[JsonSerializable(typeof(AdapterRetentionSetParams))]
[JsonSerializable(typeof(NeedsInputSignalDto))]
[JsonSerializable(typeof(NotificationDeliverDto))]
[JsonSerializable(typeof(LauncherOptionsParams))]
[JsonSerializable(typeof(LauncherOptionsResponse))]
[JsonSerializable(typeof(LauncherSuggestedFlagDto))]
[JsonSerializable(typeof(OmniChatAppendParams))]
[JsonSerializable(typeof(OmniChatHistoryParams))]
[JsonSerializable(typeof(OmniChatHistoryResult))]
[JsonSerializable(typeof(OmniChatClearParams))]
[JsonSerializable(typeof(NookScopeGetParams))]
[JsonSerializable(typeof(NookScopeSetParams))]
[JsonSerializable(typeof(NookScopeResult))]
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
[JsonSerializable(typeof(ScheduleSetRouteParams))]
[JsonSerializable(typeof(TaskBoardExportParams))]
[JsonSerializable(typeof(TaskBoardExportResultDto))]
[JsonSerializable(typeof(TaskBoardDiffParams))]
[JsonSerializable(typeof(TaskBoardDiffResultDto))]
[JsonSerializable(typeof(RunNowParams))]
[JsonSerializable(typeof(RunNowResult))]
[JsonSerializable(typeof(RepeatContinueParams))]
[JsonSerializable(typeof(RepeatFinishParams))]
[JsonSerializable(typeof(ScheduleGetParams))]
[JsonSerializable(typeof(ScheduleInfo))]
[JsonSerializable(typeof(ScheduleUpdateStateParams))]
[JsonSerializable(typeof(ScheduleValidationResultDto))]
[JsonSerializable(typeof(RunSetPendingPromptParams))]
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
[JsonSerializable(typeof(NoteSearchParams))]
[JsonSerializable(typeof(NoteSearchResult))]
[JsonSerializable(typeof(NoteReadParams))]
[JsonSerializable(typeof(NoteReadResult))]
[JsonSerializable(typeof(NoteWriteParams))]
[JsonSerializable(typeof(NoteHistoryParams))]
[JsonSerializable(typeof(NoteHistoryEntry))]
[JsonSerializable(typeof(NoteHistoryResult))]
[JsonSerializable(typeof(NoteMediaSaveParams))]
[JsonSerializable(typeof(NoteMediaSaveResult))]
[JsonSerializable(typeof(NoteGetStateParams))]
[JsonSerializable(typeof(NoteGetStateResult))]
[JsonSerializable(typeof(NoteSaveStateParams))]
[JsonSerializable(typeof(MemoryAddParams))]
[JsonSerializable(typeof(MemorySearchParams))]
[JsonSerializable(typeof(MemorySearchResult))]
[JsonSerializable(typeof(RankedFactDto))]
[JsonSerializable(typeof(MemoryRecallParams))]
[JsonSerializable(typeof(MemoryRecallResult))]
[JsonSerializable(typeof(RecallPreviewDto))]
[JsonSerializable(typeof(MemoryShowParams))]
[JsonSerializable(typeof(MemorySupersedeParams))]
[JsonSerializable(typeof(MemoryReindexParams))]
[JsonSerializable(typeof(MemoryConsolidateParams))]
[JsonSerializable(typeof(MemoryConsolidateResult))]
[JsonSerializable(typeof(MemoryProposeParams))]
[JsonSerializable(typeof(MemoryProposalTransitionParams))]
[JsonSerializable(typeof(CanvasActionParams))]
[JsonSerializable(typeof(CanvasActionResult))]
[JsonSerializable(typeof(Fact))]
[JsonSerializable(typeof(Proposal))]
[JsonSerializable(typeof(EditsFindParams))]
[JsonSerializable(typeof(EditsFindResult))]
[JsonSerializable(typeof(EditRecordDto))]
[JsonSerializable(typeof(VaultSearchParams))]
[JsonSerializable(typeof(VaultSearchResult))]
[JsonSerializable(typeof(SessionCorpusEntryDto))]
[JsonSerializable(typeof(VaultResumeParams))]
[JsonSerializable(typeof(VaultResumeResult))]
[JsonSerializable(typeof(VaultSetSettingParams))]
[JsonSerializable(typeof(VaultReindexParams))]
[JsonSerializable(typeof(LibraryListParams))]
[JsonSerializable(typeof(LibraryListResult))]
[JsonSerializable(typeof(LibraryEntryDto))]
[JsonSerializable(typeof(LibraryMaterializeParams))]
[JsonSerializable(typeof(TimelineEntry))]
[JsonSerializable(typeof(TimelineAppendParams))]
[JsonSerializable(typeof(KnowledgePingParams))]
[JsonSerializable(typeof(KnowledgePingResult))]
[JsonSerializable(typeof(TimelineListParams))]
[JsonSerializable(typeof(TimelineListResult))]
[JsonSerializable(typeof(BriefMeta))]
[JsonSerializable(typeof(RecapMeta))]
[JsonSerializable(typeof(UpdateMeta))]
[JsonSerializable(typeof(BlackboardPost))]
[JsonSerializable(typeof(BlackboardPostParams))]
[JsonSerializable(typeof(BlackboardShowParams))]
[JsonSerializable(typeof(BlackboardShowResult))]
[JsonSerializable(typeof(NookTypeDto))]
[JsonSerializable(typeof(NookTypeListResult))]
[JsonSerializable(typeof(BrowserOpenParams))]
[JsonSerializable(typeof(BrowserNavigateParams))]
[JsonSerializable(typeof(BrowserNookRefParams))]
[JsonSerializable(typeof(BrowserNookDto))]
[JsonSerializable(typeof(BrowserCreateParams))]
[JsonSerializable(typeof(BrowserAutomationExecEvent))]
[JsonSerializable(typeof(BrowserAutomationResultParams))]
[JsonSerializable(typeof(BrowserAutomationSnapshotParams))]
[JsonSerializable(typeof(BrowserAutomationClickParams))]
[JsonSerializable(typeof(BrowserAutomationFillParams))]
[JsonSerializable(typeof(BrowserAutomationEvalParams))]
[JsonSerializable(typeof(BrowserScreenshotParams))]
[JsonSerializable(typeof(BrowserSetUserAgentParams))]
[JsonSerializable(typeof(BrowserAutomationClearParams))]
[JsonSerializable(typeof(BrowserAutomationTypeParams))]
[JsonSerializable(typeof(BrowserAutomationPressParams))]
[JsonSerializable(typeof(BrowserAutomationSelectParams))]
[JsonSerializable(typeof(BrowserAutomationScrollParams))]
[JsonSerializable(typeof(BrowserAutomationWaitParams))]
[JsonSerializable(typeof(BrowserAutomationGetParams))]
[JsonSerializable(typeof(BrowserAutomationIsParams))]
[JsonSerializable(typeof(BrowserScrollValue))]
[JsonSerializable(typeof(BrowserWaitValue))]
[JsonSerializable(typeof(ReviewAddCommentParams))]
[JsonSerializable(typeof(ReviewListCommentsParams))]
[JsonSerializable(typeof(ReviewTransitionParams))]
[JsonSerializable(typeof(ReviewReAnchorParams))]
[JsonSerializable(typeof(ReviewTelemetryParams))]
[JsonSerializable(typeof(ReviewCommentDto))]
[JsonSerializable(typeof(ReviewListCommentsResult))]
[JsonSerializable(typeof(ReviewAuditDto))]
[JsonSerializable(typeof(ReviewAuditResult))]
[JsonSerializable(typeof(ReviewTelemetryDto))]
[JsonSerializable(typeof(ReviewTelemetryResult))]
[JsonSerializable(typeof(AttributionRecordParams))]
[JsonSerializable(typeof(AttributionFindByLineParams))]
[JsonSerializable(typeof(AttributionFindByRangeParams))]
[JsonSerializable(typeof(AttributionFindByToolUseParams))]
[JsonSerializable(typeof(AttributionEntryDto))]
[JsonSerializable(typeof(AttributionListResult))]
[JsonSerializable(typeof(ReviewDispatchParams))]
[JsonSerializable(typeof(ReviewDispatchResultDto))]
[JsonSerializable(typeof(DiagnosticsStatusResult))]
[JsonSerializable(typeof(DiagnosticsSnapshotTakeParams))]
[JsonSerializable(typeof(DiagnosticsExportParams))]
[JsonSerializable(typeof(DiagnosticsExportResult))]
[JsonSerializable(typeof(PerfBundleCreateParams))]
[JsonSerializable(typeof(PerfBundleDeleteParams))]
[JsonSerializable(typeof(PerfBundleDto))]
[JsonSerializable(typeof(PerfBundleListResult))]
[JsonSerializable(typeof(DirectoryListParams))]
[JsonSerializable(typeof(DirectoryEntryDto))]
[JsonSerializable(typeof(DirectoryListResult))]
[JsonSerializable(typeof(GitSummaryParams))]
[JsonSerializable(typeof(GitSummaryFileDto))]
[JsonSerializable(typeof(GitSummaryResult))]
[JsonSerializable(typeof(FeedbackSaveParams))]
[JsonSerializable(typeof(FeedbackSaveResult))]
[JsonSerializable(typeof(PerformanceResultSaveParams))]
[JsonSerializable(typeof(PerformanceResultSaveResult))]
[JsonSerializable(typeof(JsonElement))]
public sealed partial class CoveJsonContext : JsonSerializerContext;
