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

public sealed record PaneInfo(string PaneId, string Command, int Cols, int Rows, bool Alive);

public sealed record PaneListResult(PaneInfo[] Panes);

public sealed record SpawnParams(
    string Command,
    string[] Args,
    string? Cwd = null,
    Dictionary<string, string>? Env = null,
    int Cols = 80,
    int Rows = 24);

public sealed record SubscribeParams(string PaneId, ulong SinceOffset = 0);
public sealed record SubscribeResult(ulong StreamId, ulong BaseOffset, int Window);

public sealed record WriteParams(ulong StreamId, string DataBase64);
public sealed record ResizeParams(string PaneId, int Cols, int Rows);

public sealed record PaneWriteParams(string PaneId, string DataBase64);
public sealed record PaneRefParams(string PaneId);

public sealed record LayoutMutateParams(string Op, string? RoomId = null, string? TargetPaneId = null, string? NewPaneId = null, string? Orientation = null, string? Name = null, string? PaneId = null, int Dir = 1);
public sealed record LayoutMutateResult(string? RoomId = null);
public sealed record SessionStateResult(string PaneId, string Command, int Cols, int Rows, bool Alive);

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
[JsonSerializable(typeof(JsonElement))]
public sealed partial class CoveJsonContext : JsonSerializerContext;
