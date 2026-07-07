using System.Text.Json;
using System.Text.Json.Serialization;
using Cove.Protocol;

namespace Cove.Engine.Workspaces;

public static class RunCommandCommands
{
    [CoveCommand("cove://commands/workspace-command.create")]
    public static async Task<ControlResponse> Create(EngineDispatchContext ctx)
    {
        if (ctx.RunCommands is not { } svc)
            return ctx.Fail("no_run_commands", "run-command service unavailable");
        if (ctx.Request.Params is not JsonElement el
            || el.Deserialize(RunCommandVerbJsonContext.Default.RunCommandCreateParams) is not { } p
            || string.IsNullOrWhiteSpace(p.WorkspaceId) || string.IsNullOrWhiteSpace(p.Label) || string.IsNullOrWhiteSpace(p.Command))
            return ctx.Fail("bad_params", "workspaceId, label and command are required");

        var def = await svc.CreateAsync(p.WorkspaceId, p.Label, p.Command, p.Cwd).ConfigureAwait(false);
        return ctx.Ok(new RunCommandDefResult(def.Id, def.WorkspaceId, def.Label, def.Command, def.Cwd), RunCommandVerbJsonContext.Default.RunCommandDefResult);
    }

    [CoveCommand("cove://commands/workspace-command.edit")]
    public static async Task<ControlResponse> Edit(EngineDispatchContext ctx)
    {
        if (ctx.RunCommands is not { } svc)
            return ctx.Fail("no_run_commands", "run-command service unavailable");
        if (ctx.Request.Params is not JsonElement el
            || el.Deserialize(RunCommandVerbJsonContext.Default.RunCommandEditParams) is not { } p
            || string.IsNullOrWhiteSpace(p.Id) || string.IsNullOrWhiteSpace(p.Label) || string.IsNullOrWhiteSpace(p.Command))
            return ctx.Fail("bad_params", "id, label and command are required");

        var edited = await svc.EditAsync(p.Id, p.Label, p.Command, p.Cwd).ConfigureAwait(false);
        return edited is null
            ? ctx.Fail("not_found", "run-command not found")
            : ctx.Ok(new RunCommandDefResult(edited.Id, edited.WorkspaceId, edited.Label, edited.Command, edited.Cwd), RunCommandVerbJsonContext.Default.RunCommandDefResult);
    }

    [CoveCommand("cove://commands/workspace-command.delete")]
    public static async Task<ControlResponse> Delete(EngineDispatchContext ctx)
    {
        if (ctx.RunCommands is not { } svc)
            return ctx.Fail("no_run_commands", "run-command service unavailable");
        if (ctx.Request.Params is not JsonElement el
            || el.Deserialize(RunCommandVerbJsonContext.Default.RunCommandIdParams) is not { } p
            || string.IsNullOrWhiteSpace(p.Id))
            return ctx.Fail("bad_params", "id is required");

        return await svc.DeleteAsync(p.Id).ConfigureAwait(false)
            ? ctx.Ok()
            : ctx.Fail("not_found", "run-command not found");
    }

    [CoveCommand("cove://commands/workspace-command.list")]
    public static async Task<ControlResponse> List(EngineDispatchContext ctx)
    {
        if (ctx.RunCommands is not { } svc)
            return ctx.Fail("no_run_commands", "run-command service unavailable");
        if (ctx.Request.Params is not JsonElement el
            || el.Deserialize(RunCommandVerbJsonContext.Default.RunCommandListParams) is not { } p
            || string.IsNullOrWhiteSpace(p.WorkspaceId))
            return ctx.Fail("bad_params", "workspaceId is required");

        var items = await svc.ListEffectiveAsync(p.WorkspaceId, p.ParentWorkspaceId).ConfigureAwait(false);
        var result = items.Select(i => new RunCommandListItemDto(
            i.Definition.Id, i.Definition.WorkspaceId, i.Definition.Label, i.Definition.Command, i.Definition.Cwd,
            i.Lifecycle.ToString(), i.Inherited)).ToList();
        return ctx.Ok(new RunCommandListResult(result), RunCommandVerbJsonContext.Default.RunCommandListResult);
    }

    [CoveCommand("cove://commands/workspace-command.status")]
    public static async Task<ControlResponse> Status(EngineDispatchContext ctx)
    {
        if (ctx.RunCommands is not { } svc)
            return ctx.Fail("no_run_commands", "run-command service unavailable");
        if (ctx.Request.Params is not JsonElement el
            || el.Deserialize(RunCommandVerbJsonContext.Default.RunCommandIdParams) is not { } p
            || string.IsNullOrWhiteSpace(p.Id))
            return ctx.Fail("bad_params", "id is required");

        var status = await svc.StatusAsync(p.Id).ConfigureAwait(false);
        return status is null
            ? ctx.Fail("not_found", "run-command not found")
            : ctx.Ok(new RunCommandStatusResult(status.Id, status.Lifecycle.ToString(), status.SessionId, status.ExitCode, status.StartedAtUtc, status.StoppedAtUtc), RunCommandVerbJsonContext.Default.RunCommandStatusResult);
    }

    [CoveCommand("cove://commands/workspace-command.start")]
    public static async Task<ControlResponse> Start(EngineDispatchContext ctx)
    {
        if (ctx.RunCommands is not { } svc)
            return ctx.Fail("no_run_commands", "run-command service unavailable");
        if (ctx.Request.Params is not JsonElement el
            || el.Deserialize(RunCommandVerbJsonContext.Default.RunCommandIdParams) is not { } p
            || string.IsNullOrWhiteSpace(p.Id))
            return ctx.Fail("bad_params", "id is required");

        var status = await svc.StartAsync(p.Id).ConfigureAwait(false);
        return status is null
            ? ctx.Fail("not_found", "run-command not found")
            : ctx.Ok(new RunCommandStatusResult(status.Id, status.Lifecycle.ToString(), status.SessionId, status.ExitCode, status.StartedAtUtc, status.StoppedAtUtc), RunCommandVerbJsonContext.Default.RunCommandStatusResult);
    }

    [CoveCommand("cove://commands/workspace-command.stop")]
    public static async Task<ControlResponse> Stop(EngineDispatchContext ctx)
    {
        if (ctx.RunCommands is not { } svc)
            return ctx.Fail("no_run_commands", "run-command service unavailable");
        if (ctx.Request.Params is not JsonElement el
            || el.Deserialize(RunCommandVerbJsonContext.Default.RunCommandIdParams) is not { } p
            || string.IsNullOrWhiteSpace(p.Id))
            return ctx.Fail("bad_params", "id is required");

        var status = await svc.StopAsync(p.Id).ConfigureAwait(false);
        return status is null
            ? ctx.Fail("not_found", "run-command not found")
            : ctx.Ok(new RunCommandStatusResult(status.Id, status.Lifecycle.ToString(), status.SessionId, status.ExitCode, status.StartedAtUtc, status.StoppedAtUtc), RunCommandVerbJsonContext.Default.RunCommandStatusResult);
    }

    [CoveCommand("cove://commands/workspace-command.restart")]
    public static async Task<ControlResponse> Restart(EngineDispatchContext ctx)
    {
        if (ctx.RunCommands is not { } svc)
            return ctx.Fail("no_run_commands", "run-command service unavailable");
        if (ctx.Request.Params is not JsonElement el
            || el.Deserialize(RunCommandVerbJsonContext.Default.RunCommandIdParams) is not { } p
            || string.IsNullOrWhiteSpace(p.Id))
            return ctx.Fail("bad_params", "id is required");

        var status = await svc.RestartAsync(p.Id).ConfigureAwait(false);
        return status is null
            ? ctx.Fail("not_found", "run-command not found")
            : ctx.Ok(new RunCommandStatusResult(status.Id, status.Lifecycle.ToString(), status.SessionId, status.ExitCode, status.StartedAtUtc, status.StoppedAtUtc), RunCommandVerbJsonContext.Default.RunCommandStatusResult);
    }

    [CoveCommand("cove://commands/workspace-command.logs")]
    public static async Task<ControlResponse> Logs(EngineDispatchContext ctx)
    {
        if (ctx.RunCommands is not { } svc)
            return ctx.Fail("no_run_commands", "run-command service unavailable");
        if (ctx.Request.Params is not JsonElement el
            || el.Deserialize(RunCommandVerbJsonContext.Default.RunCommandLogsParams) is not { } p
            || string.IsNullOrWhiteSpace(p.Id))
            return ctx.Fail("bad_params", "id is required");

        var logs = await svc.LogsAsync(p.Id).ConfigureAwait(false);
        var tail = p.Tail is > 0 ? string.Join('\n', logs.Split('\n').TakeLast(p.Tail.Value)) : logs;
        return ctx.Ok(new RunCommandLogsResult(tail), RunCommandVerbJsonContext.Default.RunCommandLogsResult);
    }

    [CoveCommand("cove://commands/workspace-command.clear")]
    public static async Task<ControlResponse> Clear(EngineDispatchContext ctx)
    {
        if (ctx.RunCommands is not { } svc)
            return ctx.Fail("no_run_commands", "run-command service unavailable");
        if (ctx.Request.Params is not JsonElement el
            || el.Deserialize(RunCommandVerbJsonContext.Default.RunCommandIdParams) is not { } p
            || string.IsNullOrWhiteSpace(p.Id))
            return ctx.Fail("bad_params", "id is required");

        return await svc.ClearAsync(p.Id).ConfigureAwait(false)
            ? ctx.Ok()
            : ctx.Fail("not_found", "run-command not found");
    }
}

public sealed record RunCommandCreateParams(string WorkspaceId, string Label, string Command, string? Cwd = null);
public sealed record RunCommandEditParams(string Id, string Label, string Command, string? Cwd = null);
public sealed record RunCommandListParams(string WorkspaceId, string? ParentWorkspaceId = null);
public sealed record RunCommandLogsParams(string Id, int? Tail = null);
public sealed record RunCommandIdParams(string Id);
public sealed record RunCommandDefResult(string Id, string WorkspaceId, string Label, string Command, string Cwd);
public sealed record RunCommandListItemDto(string Id, string WorkspaceId, string Label, string Command, string Cwd, string Lifecycle, bool Inherited);
public sealed record RunCommandListResult(IReadOnlyList<RunCommandListItemDto> Commands);
public sealed record RunCommandStatusResult(string Id, string Lifecycle, string SessionId, int? ExitCode, DateTimeOffset? StartedAtUtc, DateTimeOffset? StoppedAtUtc);
public sealed record RunCommandLogsResult(string Logs);

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(RunCommandCreateParams))]
[JsonSerializable(typeof(RunCommandEditParams))]
[JsonSerializable(typeof(RunCommandListParams))]
[JsonSerializable(typeof(RunCommandLogsParams))]
[JsonSerializable(typeof(RunCommandIdParams))]
[JsonSerializable(typeof(RunCommandDefResult))]
[JsonSerializable(typeof(RunCommandListItemDto))]
[JsonSerializable(typeof(RunCommandListResult))]
[JsonSerializable(typeof(RunCommandStatusResult))]
[JsonSerializable(typeof(RunCommandLogsResult))]
public sealed partial class RunCommandVerbJsonContext : JsonSerializerContext { }
