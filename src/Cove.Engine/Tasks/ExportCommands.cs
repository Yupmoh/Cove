using System.Text.Json;
using Cove.Generated;
using Cove.Protocol;

namespace Cove.Engine.Tasks;

public static class ExportCommands
{
    [CoveCommand("cove://commands/task-board.export")]
    public static Task<ControlResponse> Export(EngineDispatchContext ctx)
    {
        if (ctx.TaskService is not { } svc)
            return Task.FromResult(ctx.Fail("not_ready", "task store not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.TaskBoardExportParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "export params required (exportPath, workspaceCount)"));
        var factory = svc.GetConnectionFactory();
        var exportService = new Cove.Tasks.Export.TaskBoardExportService(factory, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);
        var result = exportService.Export(p.ExportPath, p.WorkspaceCount);
        if (!result.Success)
            return Task.FromResult(ctx.Fail("export_failed", result.Error ?? "export failed"));
        return Task.FromResult(ctx.Ok(new TaskBoardExportResultDto(true, result.ExportPath, result.Manifest?.ExportedAt.ToString("o"), result.Manifest?.SchemaVersion ?? 0, result.Manifest?.WorkspaceCount ?? 0, null), CoveJsonContext.Default.TaskBoardExportResultDto));
    }

    [CoveCommand("cove://commands/task-board.diff")]
    public static Task<ControlResponse> Diff(EngineDispatchContext ctx)
    {
        if (ctx.TaskService is not { } svc)
            return Task.FromResult(ctx.Fail("not_ready", "task store not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.TaskBoardDiffParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "diff params required (importPath)"));
        var factory = svc.GetConnectionFactory();
        var exportService = new Cove.Tasks.Export.TaskBoardExportService(factory, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);
        var result = exportService.DiffAgainst(p.ImportPath);
        if (!result.Success)
            return Task.FromResult(ctx.Fail("diff_failed", result.Error ?? "diff failed"));
        var diffs = result.Diffs.Select(d => $"{d.Table}:{d.Id}:{d.ChangeType}").ToArray();
        return Task.FromResult(ctx.Ok(new TaskBoardDiffResultDto(true, diffs, null), CoveJsonContext.Default.TaskBoardDiffResultDto));
    }
}
