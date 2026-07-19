using System.Text.Json;
using Cove.Generated;
using Cove.Protocol;

namespace Cove.Engine.Tasks;

public static class ExportCommands
{
    [CoveCommand("cove://commands/task-board.export")]
    public static async Task<ControlResponse> Export(EngineDispatchContext ctx)
    {
        if (ctx.TaskService is not { } svc)
            return ctx.Fail("not_ready", "task store not available");
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.TaskBoardExportParams) is not { } p)
            return ctx.Fail("invalid_params", "export params required (exportPath, bayCount)");
        var result = await svc.ExportTaskBoardAsync(p.ExportPath, p.BayCount);
        if (!result.Success)
            return ctx.Fail("export_failed", result.Error ?? "export failed");
        return ctx.Ok(new TaskBoardExportResultDto(true, result.ExportPath, result.Manifest?.ExportedAt.ToString("o"), result.Manifest?.SchemaVersion ?? 0, result.Manifest?.BayCount ?? 0, null), CoveJsonContext.Default.TaskBoardExportResultDto);
    }

    [CoveCommand("cove://commands/task-board.diff")]
    public static async Task<ControlResponse> Diff(EngineDispatchContext ctx)
    {
        if (ctx.TaskService is not { } svc)
            return ctx.Fail("not_ready", "task store not available");
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.TaskBoardDiffParams) is not { } p)
            return ctx.Fail("invalid_params", "diff params required (importPath)");
        var result = await svc.DiffTaskBoardAsync(p.ImportPath);
        if (!result.Success)
            return ctx.Fail("diff_failed", result.Error ?? "diff failed");
        var diffs = result.Diffs.Select(d => $"{d.Table}:{d.Id}:{d.ChangeType}").ToArray();
        return ctx.Ok(new TaskBoardDiffResultDto(true, diffs, null), CoveJsonContext.Default.TaskBoardDiffResultDto);
    }
}
