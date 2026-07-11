using System.Text.Json;
using System.Text.Json.Serialization;
using Cove.Protocol;

namespace Cove.Engine.Nooks;

public static class NookEditorCommands
{
    [CoveCommand("cove://commands/editor.open")]
    public static Task<ControlResponse> Open(EngineDispatchContext ctx)
    {
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(NookEditorJsonContext.Default.EditorOpenParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "editor open params required"));
        var state = new EditorState(p.FilePath, Cursor: null, Scroll: null, Fold: null, Undo: null, ReadOnly: p.ReadOnly ?? false);
        return Task.FromResult(ctx.Ok(new EditorOpenResult(state.FilePath, state), NookEditorJsonContext.Default.EditorOpenResult));
    }

    [CoveCommand("cove://commands/editor.save")]
    public static Task<ControlResponse> Save(EngineDispatchContext ctx)
    {
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(NookEditorJsonContext.Default.EditorSaveParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "editor save params required"));
        var state = new EditorState(p.FilePath, p.Cursor, p.Scroll, p.Fold, p.Undo, p.ReadOnly ?? false);
        return Task.FromResult(ctx.Ok(state, NookEditorJsonContext.Default.EditorState));
    }

    [CoveCommand("cove://commands/editor.get-state")]
    public static Task<ControlResponse> GetState(EngineDispatchContext ctx)
    {
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(NookEditorJsonContext.Default.EditorStateParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "editor state params required"));
        var state = new EditorState(p.FilePath, Cursor: null, Scroll: null, Fold: null, Undo: null, ReadOnly: false);
        return Task.FromResult(ctx.Ok(state, NookEditorJsonContext.Default.EditorState));
    }

    [CoveCommand("cove://commands/editor.set-state")]
    public static Task<ControlResponse> SetState(EngineDispatchContext ctx)
    {
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(NookEditorJsonContext.Default.EditorState) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "editor state required"));
        return Task.FromResult(ctx.Ok(p, NookEditorJsonContext.Default.EditorState));
    }
}

public static class NookSearchCommands
{
    [CoveCommand("cove://commands/search.query")]
    public static async Task<ControlResponse> Query(EngineDispatchContext ctx)
    {
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(NookSearchJsonContext.Default.SearchQueryParams) is not { } p)
            return ctx.Fail("invalid_params", "search query params required");
        if (ctx.SearchService is not { } search)
            return ctx.Fail("not_ready", "search service not available");

        var searchParams = new Cove.Engine.Search.SearchParams(p.Query, p.Path, p.Regex, p.WholeWord, p.CaseInsensitive, p.IncludeGlob, p.ExcludeGlob);
        var searchResult = await search.SearchAsync(searchParams);
        var matches = searchResult.Matches.Select(m => new SearchMatch(m.FilePath, m.Line, m.Column, m.Text, m.ContextBefore)).ToList();
        var result = new SearchResult(searchResult.Query, matches, searchResult.UseRegex, searchResult.WholeWord, searchResult.CaseInsensitive);
        return ctx.Ok(result, NookSearchJsonContext.Default.SearchResult);
    }
    [CoveCommand("cove://commands/search.replace")]
    public static async Task<ControlResponse> Replace(EngineDispatchContext ctx)
    {
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(NookSearchJsonContext.Default.SearchReplaceParams) is not { } p)
            return ctx.Fail("invalid_params", "search replace params required");
        if (ctx.SearchService is not { } search)
            return ctx.Fail("not_ready", "search service not available");

        var replaceService = new Cove.Engine.Search.ReplaceService();
        var results = replaceService.ReplaceInFiles(p.Search, p.Replacement, p.Files, p.Regex ?? false, p.CaseInsensitive ?? true, p.WholeWord ?? false);
        var dtos = results.Select(r => new SearchReplaceResult(r.FilePath, r.Replacements, r.Saved)).ToList();
        return ctx.Ok(new SearchReplaceResponse(dtos), NookSearchJsonContext.Default.SearchReplaceResponse);
    }

    [CoveCommand("cove://commands/search.get-state")]
    public static Task<ControlResponse> GetState(EngineDispatchContext ctx)
    {
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(NookSearchJsonContext.Default.SearchStateParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "search state params required"));
        var state = new SearchState(p.Query ?? "", Regex: false, WholeWord: false, CaseInsensitive: true, IncludeGlobs: [], ExcludeGlobs: [], Scroll: null);
        return Task.FromResult(ctx.Ok(state, NookSearchJsonContext.Default.SearchState));
    }

    [CoveCommand("cove://commands/search.set-state")]
    public static Task<ControlResponse> SetState(EngineDispatchContext ctx)
    {
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(NookSearchJsonContext.Default.SearchState) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "search state required"));
        return Task.FromResult(ctx.Ok(p, NookSearchJsonContext.Default.SearchState));
    }
}

public static class NookScmCommands
{
    [CoveCommand("cove://commands/scm.status")]
    public static async Task<ControlResponse> Status(EngineDispatchContext ctx)
    {
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(NookScmJsonContext.Default.ScmStatusParams) is not { } p)
            return ctx.Fail("invalid_params", "scm status params required");
        if (ctx.GitReadModel is not { } git)
            return ctx.Fail("not_ready", "git read model not available");

        var status = await git.GetStatusAsync(p.RepoRoot, default);
        var staged = status.Entries.Where(e => e.IsStaged).Select(e => new ScmFileStatus(e.FilePath, e.StatusCode, e.OldFilePath ?? "")).ToList();
        var unstaged = status.Entries.Where(e => !e.IsStaged).Select(e => new ScmFileStatus(e.FilePath, e.StatusCode, e.OldFilePath ?? "")).ToList();
        var result = new ScmStatusResult(p.RepoRoot, staged, unstaged);
        return ctx.Ok(result, NookScmJsonContext.Default.ScmStatusResult);
    }

    [CoveCommand("cove://commands/scm.diff")]
    public static async Task<ControlResponse> Diff(EngineDispatchContext ctx)
    {
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(NookScmJsonContext.Default.ScmDiffParams) is not { } p)
            return ctx.Fail("invalid_params", "scm diff params required");
        if (ctx.GitReadModel is not { } git)
            return ctx.Fail("not_ready", "git read model not available");

        var fileDiff = await git.GetFileDiffAsync(p.RepoRoot, p.FilePath, p.Ref, default);
        var result = new ScmDiffResult(p.FilePath, OldContent: null, NewContent: fileDiff.Patch, OldRef: p.Ref);
        return ctx.Ok(result, NookScmJsonContext.Default.ScmDiffResult);
    }
    [CoveCommand("cove://commands/scm.log")]
    public static async Task<ControlResponse> Log(EngineDispatchContext ctx)
    {
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(NookScmJsonContext.Default.ScmLogParams) is not { } p)
            return ctx.Fail("invalid_params", "scm log params required");
        if (ctx.GitReadModel is not { } git)
            return ctx.Fail("not_ready", "git read model not available");

        var unpushed = await git.GetUnpushedAsync(p.RepoRoot, default);
        var unpulled = await git.GetUnpulledAsync(p.RepoRoot, default);
        var result = new ScmLogResult(p.RepoRoot, ToDtos(unpushed), ToDtos(unpulled));
        return ctx.Ok(result, NookScmJsonContext.Default.ScmLogResult);
    }

    private static IReadOnlyList<ScmCommit> ToDtos(Cove.Engine.Bays.GitLog log)
        => log.Commits.Select(c => new ScmCommit(c.Sha, c.Author, c.Message, c.Date.ToString("o"))).ToList();

    [CoveCommand("cove://commands/scm.stage")]
    public static async Task<ControlResponse> Stage(EngineDispatchContext ctx)
    {
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(NookScmJsonContext.Default.ScmStageParams) is not { } p)
            return ctx.Fail("invalid_params", "scm stage params required");
        if (ctx.GitReadModel is not { } git)
            return ctx.Fail("not_ready", "git read model not available");

        if (p.Unstage)
            await git.UnstageAsync(p.RepoRoot, p.FilePath, default);
        else
            await git.StageAsync(p.RepoRoot, p.FilePath, default);
        return ctx.Ok();
    }

    [CoveCommand("cove://commands/scm.commit")]
    public static async Task<ControlResponse> Commit(EngineDispatchContext ctx)
    {
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(NookScmJsonContext.Default.ScmCommitParams) is not { } p)
            return ctx.Fail("invalid_params", "scm commit params required");
        if (ctx.GitReadModel is not { } git)
            return ctx.Fail("not_ready", "git read model not available");

        var success = await git.CommitAsync(p.RepoRoot, p.Message, p.Amend ?? false, p.Sign ?? false, default);
        return ctx.Ok(new ScmCommitResult(p.Message, success), NookScmJsonContext.Default.ScmCommitResult);
    }

    [CoveCommand("cove://commands/scm.blame")]
    public static async Task<ControlResponse> Blame(EngineDispatchContext ctx)
    {
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(NookScmJsonContext.Default.ScmBlameParams) is not { } p)
            return ctx.Fail("invalid_params", "scm blame params required");
        if (ctx.GitReadModel is not { } git)
            return ctx.Fail("not_ready", "git read model not available");

        var blame = await git.GetBlameAsync(p.RepoRoot, p.FilePath, default);
        var lines = blame.Lines.Select(l => new ScmBlameLine(l.LineNumber, l.Commit, l.Author, "")).ToList();
        var result = new ScmBlameResult(p.FilePath, lines);
        return ctx.Ok(result, NookScmJsonContext.Default.ScmBlameResult);
    }
}

public static class NookViewerCommands
{
    [CoveCommand("cove://commands/viewer.open")]
    public static Task<ControlResponse> Open(EngineDispatchContext ctx)
    {
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(NookViewerJsonContext.Default.ViewerOpenParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "viewer open params required"));
        var state = new ViewerState(p.FilePath, p.Ref, p.ContextLines ?? 3);
        return Task.FromResult(ctx.Ok(state, NookViewerJsonContext.Default.ViewerState));
    }

    [CoveCommand("cove://commands/viewer.get-state")]
    public static Task<ControlResponse> GetState(EngineDispatchContext ctx)
    {
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(NookViewerJsonContext.Default.ViewerStateParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "viewer state params required"));
        var state = new ViewerState(p.FilePath, Ref: null, ContextLines: 3);
        return Task.FromResult(ctx.Ok(state, NookViewerJsonContext.Default.ViewerState));
    }
}
public sealed record SearchReplaceParams(string Search, string Replacement, IReadOnlyList<string> Files, bool? Regex, bool? WholeWord, bool? CaseInsensitive);
public sealed record SearchReplaceResponse(IReadOnlyList<SearchReplaceResult> Results);
public sealed record SearchReplaceResult(string FilePath, int Replacements, bool Saved);

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
public sealed record ScmDiffParams(string RepoRoot, string FilePath, string? Ref);
public sealed record ScmDiffResult(string FilePath, string? OldContent, string? NewContent, string? OldRef);
public sealed record ScmStageParams(string RepoRoot, string FilePath, bool Unstage);
public sealed record ScmCommitParams(string RepoRoot, string Message, bool? Amend, bool? Sign);
public sealed record ScmCommitResult(string Message, bool Success);
public sealed record ScmBlameParams(string RepoRoot, string FilePath, string? Ref);
public sealed record ScmBlameResult(string FilePath, IReadOnlyList<ScmBlameLine> Lines);
public sealed record ScmBlameLine(int Line, string Commit, string Author, string RelativeTime);
public sealed record ScmLogParams(string RepoRoot);
public sealed record ScmLogResult(string RepoRoot, IReadOnlyList<ScmCommit> Unpushed, IReadOnlyList<ScmCommit> Unpulled);
public sealed record ScmCommit(string Sha, string Author, string Message, string Date);

public sealed record ViewerOpenParams(string FilePath, string? Ref, int? ContextLines);
public sealed record ViewerStateParams(string FilePath);
public sealed record ViewerState(string FilePath, string? Ref, int ContextLines);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(EditorOpenParams))]
[JsonSerializable(typeof(EditorOpenResult))]
[JsonSerializable(typeof(EditorSaveParams))]
[JsonSerializable(typeof(EditorStateParams))]
[JsonSerializable(typeof(EditorState))]
public sealed partial class NookEditorJsonContext : JsonSerializerContext { }

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(SearchQueryParams))]
[JsonSerializable(typeof(SearchReplaceParams))]
[JsonSerializable(typeof(SearchReplaceResponse))]
[JsonSerializable(typeof(SearchReplaceResult))]
[JsonSerializable(typeof(SearchResult))]
[JsonSerializable(typeof(SearchMatch))]
[JsonSerializable(typeof(SearchStateParams))]
[JsonSerializable(typeof(SearchState))]
public sealed partial class NookSearchJsonContext : JsonSerializerContext { }

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
[JsonSerializable(typeof(ScmLogParams))]
[JsonSerializable(typeof(ScmLogResult))]
[JsonSerializable(typeof(ScmCommit))]
public sealed partial class NookScmJsonContext : JsonSerializerContext { }

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ViewerOpenParams))]
[JsonSerializable(typeof(ViewerStateParams))]
[JsonSerializable(typeof(ViewerState))]
public sealed partial class NookViewerJsonContext : JsonSerializerContext { }
