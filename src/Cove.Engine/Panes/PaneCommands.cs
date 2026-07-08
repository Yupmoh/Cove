using System.Text.Json;
using System.Text.Json.Serialization;
using Cove.Protocol;

namespace Cove.Engine.Panes;

public static class PaneEditorCommands
{
    [CoveCommand("cove://commands/editor.open")]
    public static Task<ControlResponse> Open(EngineDispatchContext ctx)
    {
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(PaneEditorJsonContext.Default.EditorOpenParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "editor open params required"));
        var state = new EditorState(p.FilePath, Cursor: null, Scroll: null, Fold: null, Undo: null, ReadOnly: p.ReadOnly ?? false);
        return Task.FromResult(ctx.Ok(new EditorOpenResult(state.FilePath, state), PaneEditorJsonContext.Default.EditorOpenResult));
    }

    [CoveCommand("cove://commands/editor.save")]
    public static Task<ControlResponse> Save(EngineDispatchContext ctx)
    {
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(PaneEditorJsonContext.Default.EditorSaveParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "editor save params required"));
        var state = new EditorState(p.FilePath, p.Cursor, p.Scroll, p.Fold, p.Undo, p.ReadOnly ?? false);
        return Task.FromResult(ctx.Ok(state, PaneEditorJsonContext.Default.EditorState));
    }

    [CoveCommand("cove://commands/editor.get-state")]
    public static Task<ControlResponse> GetState(EngineDispatchContext ctx)
    {
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(PaneEditorJsonContext.Default.EditorStateParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "editor state params required"));
        var state = new EditorState(p.FilePath, Cursor: null, Scroll: null, Fold: null, Undo: null, ReadOnly: false);
        return Task.FromResult(ctx.Ok(state, PaneEditorJsonContext.Default.EditorState));
    }

    [CoveCommand("cove://commands/editor.set-state")]
    public static Task<ControlResponse> SetState(EngineDispatchContext ctx)
    {
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(PaneEditorJsonContext.Default.EditorState) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "editor state required"));
        return Task.FromResult(ctx.Ok(p, PaneEditorJsonContext.Default.EditorState));
    }
}

public static class PaneSearchCommands
{
    [CoveCommand("cove://commands/search.query")]
    public static Task<ControlResponse> Query(EngineDispatchContext ctx)
    {
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(PaneSearchJsonContext.Default.SearchQueryParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "search query params required"));
        var result = new SearchResult(p.Query, [], p.Regex ?? false, p.WholeWord ?? false, p.CaseInsensitive ?? true);
        return Task.FromResult(ctx.Ok(result, PaneSearchJsonContext.Default.SearchResult));
    }

    [CoveCommand("cove://commands/search.get-state")]
    public static Task<ControlResponse> GetState(EngineDispatchContext ctx)
    {
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(PaneSearchJsonContext.Default.SearchStateParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "search state params required"));
        var state = new SearchState(p.Query ?? "", Regex: false, WholeWord: false, CaseInsensitive: true, IncludeGlobs: [], ExcludeGlobs: [], Scroll: null);
        return Task.FromResult(ctx.Ok(state, PaneSearchJsonContext.Default.SearchState));
    }

    [CoveCommand("cove://commands/search.set-state")]
    public static Task<ControlResponse> SetState(EngineDispatchContext ctx)
    {
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(PaneSearchJsonContext.Default.SearchState) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "search state required"));
        return Task.FromResult(ctx.Ok(p, PaneSearchJsonContext.Default.SearchState));
    }
}

public static class PaneScmCommands
{
    [CoveCommand("cove://commands/scm.status")]
    public static Task<ControlResponse> Status(EngineDispatchContext ctx)
    {
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(PaneScmJsonContext.Default.ScmStatusParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "scm status params required"));
        var result = new ScmStatusResult(p.RepoRoot, [], []);
        return Task.FromResult(ctx.Ok(result, PaneScmJsonContext.Default.ScmStatusResult));
    }

    [CoveCommand("cove://commands/scm.diff")]
    public static Task<ControlResponse> Diff(EngineDispatchContext ctx)
    {
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(PaneScmJsonContext.Default.ScmDiffParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "scm diff params required"));
        var result = new ScmDiffResult(p.FilePath, OldContent: null, NewContent: null, OldRef: p.Ref);
        return Task.FromResult(ctx.Ok(result, PaneScmJsonContext.Default.ScmDiffResult));
    }

    [CoveCommand("cove://commands/scm.stage")]
    public static Task<ControlResponse> Stage(EngineDispatchContext ctx)
    {
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(PaneScmJsonContext.Default.ScmStageParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "scm stage params required"));
        return Task.FromResult(ctx.Ok());
    }

    [CoveCommand("cove://commands/scm.commit")]
    public static Task<ControlResponse> Commit(EngineDispatchContext ctx)
    {
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(PaneScmJsonContext.Default.ScmCommitParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "scm commit params required"));
        return Task.FromResult(ctx.Ok(new ScmCommitResult(p.Message, Success: true), PaneScmJsonContext.Default.ScmCommitResult));
    }

    [CoveCommand("cove://commands/scm.blame")]
    public static Task<ControlResponse> Blame(EngineDispatchContext ctx)
    {
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(PaneScmJsonContext.Default.ScmBlameParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "scm blame params required"));
        var result = new ScmBlameResult(p.FilePath, []);
        return Task.FromResult(ctx.Ok(result, PaneScmJsonContext.Default.ScmBlameResult));
    }
}

public static class PaneViewerCommands
{
    [CoveCommand("cove://commands/viewer.open")]
    public static Task<ControlResponse> Open(EngineDispatchContext ctx)
    {
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(PaneViewerJsonContext.Default.ViewerOpenParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "viewer open params required"));
        var state = new ViewerState(p.FilePath, p.Ref, p.ContextLines ?? 3);
        return Task.FromResult(ctx.Ok(state, PaneViewerJsonContext.Default.ViewerState));
    }

    [CoveCommand("cove://commands/viewer.get-state")]
    public static Task<ControlResponse> GetState(EngineDispatchContext ctx)
    {
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(PaneViewerJsonContext.Default.ViewerStateParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "viewer state params required"));
        var state = new ViewerState(p.FilePath, Ref: null, ContextLines: 3);
        return Task.FromResult(ctx.Ok(state, PaneViewerJsonContext.Default.ViewerState));
    }
}

public sealed record EditorOpenParams(string FilePath, bool? ReadOnly);
public sealed record EditorOpenResult(string FilePath, EditorState State);
public sealed record EditorSaveParams(string FilePath, string? Cursor, string? Scroll, string? Fold, string? Undo, bool? ReadOnly);
public sealed record EditorStateParams(string FilePath);
public sealed record EditorState(string FilePath, string? Cursor, string? Scroll, string? Fold, string? Undo, bool ReadOnly);

public sealed record SearchQueryParams(string Query, string? Path, bool? Regex, bool? WholeWord, bool? CaseInsensitive, string? IncludeGlob, string? ExcludeGlob);
public sealed record SearchResult(string Query, IReadOnlyList<SearchMatch> Matches, bool Regex, bool WholeWord, bool CaseInsensitive);
public sealed record SearchMatch(string FilePath, int Line, int Column, string Text, string? Context);
public sealed record SearchStateParams(string? Query);
public sealed record SearchState(string Query, bool Regex, bool WholeWord, bool CaseInsensitive, IReadOnlyList<string> IncludeGlobs, IReadOnlyList<string> ExcludeGlobs, string? Scroll);

public sealed record ScmStatusParams(string RepoRoot);
public sealed record ScmStatusResult(string RepoRoot, IReadOnlyList<ScmFileStatus> Staged, IReadOnlyList<ScmFileStatus> Unstaged);
public sealed record ScmFileStatus(string FilePath, string Status, string OldPath);
public sealed record ScmDiffParams(string FilePath, string Ref);
public sealed record ScmDiffResult(string FilePath, string? OldContent, string? NewContent, string? OldRef);
public sealed record ScmStageParams(string RepoRoot, string FilePath, bool Unstage);
public sealed record ScmCommitParams(string RepoRoot, string Message, bool? Amend, bool? Sign);
public sealed record ScmCommitResult(string Message, bool Success);
public sealed record ScmBlameParams(string FilePath, string? Ref);
public sealed record ScmBlameResult(string FilePath, IReadOnlyList<ScmBlameLine> Lines);
public sealed record ScmBlameLine(int Line, string Commit, string Author, string RelativeTime);

public sealed record ViewerOpenParams(string FilePath, string? Ref, int? ContextLines);
public sealed record ViewerStateParams(string FilePath);
public sealed record ViewerState(string FilePath, string? Ref, int ContextLines);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(EditorOpenParams))]
[JsonSerializable(typeof(EditorOpenResult))]
[JsonSerializable(typeof(EditorSaveParams))]
[JsonSerializable(typeof(EditorStateParams))]
[JsonSerializable(typeof(EditorState))]
public sealed partial class PaneEditorJsonContext : JsonSerializerContext { }

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(SearchQueryParams))]
[JsonSerializable(typeof(SearchResult))]
[JsonSerializable(typeof(SearchMatch))]
[JsonSerializable(typeof(SearchStateParams))]
[JsonSerializable(typeof(SearchState))]
public sealed partial class PaneSearchJsonContext : JsonSerializerContext { }

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ScmStatusParams))]
[JsonSerializable(typeof(ScmStatusResult))]
[JsonSerializable(typeof(ScmFileStatus))]
[JsonSerializable(typeof(ScmDiffParams))]
[JsonSerializable(typeof(ScmDiffResult))]
[JsonSerializable(typeof(ScmStageParams))]
[JsonSerializable(typeof(ScmCommitParams))]
[JsonSerializable(typeof(ScmCommitResult))]
[JsonSerializable(typeof(ScmBlameParams))]
[JsonSerializable(typeof(ScmBlameResult))]
[JsonSerializable(typeof(ScmBlameLine))]
public sealed partial class PaneScmJsonContext : JsonSerializerContext { }

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ViewerOpenParams))]
[JsonSerializable(typeof(ViewerStateParams))]
[JsonSerializable(typeof(ViewerState))]
public sealed partial class PaneViewerJsonContext : JsonSerializerContext { }
