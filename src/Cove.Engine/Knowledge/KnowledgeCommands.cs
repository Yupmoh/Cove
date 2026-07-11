using System.Linq;
using System.Text.Json;
using Cove.Generated;
using Cove.Protocol;

namespace Cove.Engine.Knowledge;

public static class KnowledgeCommands
{
    [CoveCommand("cove://commands/knowledge.ping")]
    public static Task<ControlResponse> Ping(EngineDispatchContext ctx)
    {
        string? echo = null;
        if (ctx.Request.Params is JsonElement el && el.Deserialize(CoveJsonContext.Default.KnowledgePingParams) is { } p)
            echo = p.Echo;
        return Task.FromResult(ctx.Ok(new KnowledgePingResult("pong", echo), CoveJsonContext.Default.KnowledgePingResult));
    }

    [CoveCommand("cove://commands/note.create")]
    public static Task<ControlResponse> NoteCreate(EngineDispatchContext ctx)
    {
        if (ctx.NoteFiles is not { } store)
            return Task.FromResult(ctx.Fail("not_ready", "note store not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.NoteCreateParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "note create params required"));

        var note = store.Create(new Note { Title = p.Title, Content = p.Content ?? "", WorkspaceId = p.WorkspaceId, Source = p.Source, Kind = p.Kind ?? "markdown" });
        return Task.FromResult(ctx.Ok(note, CoveJsonContext.Default.Note));
    }

    [CoveCommand("cove://commands/note.get")]
    public static Task<ControlResponse> NoteGet(EngineDispatchContext ctx)
    {
        if (ctx.NoteFiles is not { } store)
            return Task.FromResult(ctx.Fail("not_ready", "note store not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.NoteReadParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "note read params required"));

        var note = store.Get(p.WorkspaceId, p.Id);
        if (note is null)
            return Task.FromResult(ctx.Fail("not_found", "note not found"));
        return Task.FromResult(ctx.Ok(note, CoveJsonContext.Default.Note));
    }

    [CoveCommand("cove://commands/note.list")]
    public static Task<ControlResponse> NoteList(EngineDispatchContext ctx)
    {
        if (ctx.NoteFiles is not { } store)
            return Task.FromResult(ctx.Fail("not_ready", "note store not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.NoteListParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "note list params required"));

        var metas = store.ListByWorkspace(p.WorkspaceId);
        var notes = new System.Collections.Generic.List<Note>(metas.Count);
        foreach (var m in metas)
        {
            var full = store.Get(m.WorkspaceId, m.Id);
            if (full is not null)
                notes.Add(full);
        }
        return Task.FromResult(ctx.Ok(new NoteListResult(notes), CoveJsonContext.Default.NoteListResult));
    }

    [CoveCommand("cove://commands/note.update")]
    public static Task<ControlResponse> NoteUpdate(EngineDispatchContext ctx)
    {
        if (ctx.NoteFiles is not { } store)
            return Task.FromResult(ctx.Fail("not_ready", "note store not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.NoteWriteParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "note write params required"));

        store.Update(p.WorkspaceId, p.Id, n => n with { Title = p.Title ?? n.Title, Content = p.Content ?? n.Content, Kind = p.Kind ?? n.Kind });
        return Task.FromResult(ctx.Ok());
    }

    [CoveCommand("cove://commands/note.delete")]
    public static Task<ControlResponse> NoteDelete(EngineDispatchContext ctx)
    {
        if (ctx.NoteFiles is not { } store)
            return Task.FromResult(ctx.Fail("not_ready", "note store not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.NoteReadParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "note read params required"));

        store.Delete(p.WorkspaceId, p.Id);
        return Task.FromResult(ctx.Ok());
    }

    [CoveCommand("cove://commands/note.search")]
    public static Task<ControlResponse> NoteSearch(EngineDispatchContext ctx)
    {
        if (ctx.NoteFiles is not { } store)
            return Task.FromResult(ctx.Fail("not_ready", "note store not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.NoteSearchParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "note search params required"));

        var results = store.Search(p.WorkspaceId, p.Query, p.Limit ?? 20);
        return Task.FromResult(ctx.Ok(new NoteSearchResult(results), CoveJsonContext.Default.NoteSearchResult));
    }

    [CoveCommand("cove://commands/note.read")]
    public static Task<ControlResponse> NoteRead(EngineDispatchContext ctx)
    {
        if (ctx.NoteFiles is not { } store)
            return Task.FromResult(ctx.Fail("not_ready", "note store not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.NoteReadParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "note read params required"));

        var note = store.Get(p.WorkspaceId, p.Id);
        if (note is null)
            return Task.FromResult(ctx.Fail("not_found", "note not found"));

        var format = p.Format;
        if (format == "svg")
        {
            if (note.Kind != "sketch")
                return Task.FromResult(ctx.Fail("invalid_format", "--svg is only valid for sketch notes"));
            var svgSerializer = new SketchSvgSerializer(Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);
            var svg = svgSerializer.Serialize(note.Content);
            return Task.FromResult(ctx.Ok(new NoteReadResult(note.Id, note.Title, svg, note.Kind, "svg"), CoveJsonContext.Default.NoteReadResult));
        }
        if (format == "png")
        {
            if (note.Kind != "sketch")
                return Task.FromResult(ctx.Fail("invalid_format", "--png is only valid for sketch notes"));
            var rasterizer = new SketchPngRasterizer(Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);
            var png = rasterizer.Rasterize(note.Content);
            return Task.FromResult(ctx.Ok(new NoteReadResult(note.Id, note.Title, System.Convert.ToBase64String(png), note.Kind, "png"), CoveJsonContext.Default.NoteReadResult));
        }

        return Task.FromResult(ctx.Ok(new NoteReadResult(note.Id, note.Title, note.Content, note.Kind, null), CoveJsonContext.Default.NoteReadResult));
    }

    [CoveCommand("cove://commands/note.write")]
    public static Task<ControlResponse> NoteWrite(EngineDispatchContext ctx)
    {
        if (ctx.NoteFiles is not { } store)
            return Task.FromResult(ctx.Fail("not_ready", "note store not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.NoteWriteParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "note write params required"));

        store.Update(p.WorkspaceId, p.Id, n => n with { Title = p.Title ?? n.Title, Content = p.Content ?? n.Content, Kind = p.Kind ?? n.Kind });
        return Task.FromResult(ctx.Ok());
    }

    [CoveCommand("cove://commands/note.history")]
    public static Task<ControlResponse> NoteHistory(EngineDispatchContext ctx)
    {
        if (ctx.NoteFiles is not { } store)
            return Task.FromResult(ctx.Fail("not_ready", "note store not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.NoteHistoryParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "note history params required"));

        var history = store.GetHistory(p.WorkspaceId, p.Id);
        return Task.FromResult(ctx.Ok(new NoteHistoryResult(history), CoveJsonContext.Default.NoteHistoryResult));
    }

    [CoveCommand("cove://commands/note.media.save")]
    public static Task<ControlResponse> NoteMediaSave(EngineDispatchContext ctx)
    {
        if (ctx.NoteFiles is not { } store)
            return Task.FromResult(ctx.Fail("not_ready", "note store not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.NoteMediaSaveParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "note media save params required"));

        byte[] bytes;
        try
        {
            bytes = System.Convert.FromBase64String(p.Base64Data);
        }
        catch (System.FormatException)
        {
            return Task.FromResult(ctx.Fail("invalid_data", "base64 data is malformed or contains a data-URL prefix"));
        }
        var mediaPath = store.SaveMedia(p.WorkspaceId, p.Id, p.FileName, bytes);
        return Task.FromResult(ctx.Ok(new NoteMediaSaveResult(mediaPath), CoveJsonContext.Default.NoteMediaSaveResult));
    }

    [CoveCommand("cove://commands/note.get-state")]
    public static Task<ControlResponse> NoteGetState(EngineDispatchContext ctx)
    {
        if (ctx.NoteFiles is not { } store)
            return Task.FromResult(ctx.Fail("not_ready", "note store not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.NoteGetStateParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "note get-state params required"));

        var state = store.LoadState(p.WorkspaceId, p.Id);
        return Task.FromResult(ctx.Ok(new NoteGetStateResult(state), CoveJsonContext.Default.NoteGetStateResult));
    }

    [CoveCommand("cove://commands/note.save-state")]
    public static Task<ControlResponse> NoteSaveState(EngineDispatchContext ctx)
    {
        if (ctx.NoteFiles is not { } store)
            return Task.FromResult(ctx.Fail("not_ready", "note store not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.NoteSaveStateParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "note save-state params required"));

        store.SaveState(p.WorkspaceId, p.Id, p.StateJson);
        return Task.FromResult(ctx.Ok());
    }

    [CoveCommand("cove://commands/canvas.action")]
    public static async Task<ControlResponse> CanvasAction(EngineDispatchContext ctx)
    {
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.CanvasActionParams) is not { } p)
            return ctx.Fail("invalid_params", "canvas action params required");

        if (p.Action == "cove_command")
        {
            if (string.IsNullOrEmpty(p.Uri))
                return ctx.Fail("invalid_params", "uri required for cove_command action");
            if (ctx.Redrive is null)
                return ctx.Fail("not_ready", "redrive unavailable");
            var subReq = new ControlRequest(ctx.Request.Id + "-action", p.Uri, p.State);
            var subResp = await ctx.Redrive(subReq);
            if (subResp is null)
                return ctx.Fail("not_found", $"command '{p.Uri}' not found");
            return ctx.Ok(new CanvasActionResult(true, null, p.Uri), CoveJsonContext.Default.CanvasActionResult);
        }

        if (p.Action == "send_to_agent")
        {
            if (string.IsNullOrEmpty(p.TargetPane))
                return ctx.Fail("invalid_params", "targetPane required for send_to_agent action");
            if (ctx.Panes is not { } panes)
                return ctx.Fail("not_ready", "pane registry not available");
            var message = p.Payload ?? "";
            var bytes = System.Text.Encoding.UTF8.GetBytes(message + "\r");
            if (!panes.Write(p.TargetPane, bytes))
                return ctx.Fail("not_found", $"target pane {p.TargetPane} not found or write failed");
            return ctx.Ok(new CanvasActionResult(true, p.TargetPane, null), CoveJsonContext.Default.CanvasActionResult);
        }

        return ctx.Fail("invalid_action", $"unknown action: {p.Action}");
    }

    [CoveCommand("cove://commands/timeline.append")]
    public static Task<ControlResponse> TimelineAppend(EngineDispatchContext ctx)
    {
        if (ctx.Timeline is not { } store)
            return Task.FromResult(ctx.Fail("not_ready", "timeline store not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.TimelineAppendParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "timeline append params required"));

        try
        {
            var entry = store.Append(new TimelineEntry { WorkspaceId = p.WorkspaceId, Kind = p.Kind, Source = p.Source, Scope = p.Scope, Summary = p.Summary, Tags = p.Tags });
            return Task.FromResult(ctx.Ok(entry, CoveJsonContext.Default.TimelineEntry));
        }
        catch (TimelineValidationException ex)
        {
            return Task.FromResult(ctx.Fail(ex.Code, ex.Message));
        }
    }

    [CoveCommand("cove://commands/timeline.list")]
    public static Task<ControlResponse> TimelineList(EngineDispatchContext ctx)
    {
        if (ctx.Timeline is not { } store)
            return Task.FromResult(ctx.Fail("not_ready", "timeline store not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.TimelineListParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "timeline list params required"));

        var entries = store.ListByWorkspace(p.WorkspaceId);
        return Task.FromResult(ctx.Ok(new TimelineListResult(entries), CoveJsonContext.Default.TimelineListResult));
    }

    [CoveCommand("cove://commands/blackboard.post")]
    public static Task<ControlResponse> BlackboardPost(EngineDispatchContext ctx)
    {
        if (ctx.Blackboard is not { } store)
            return Task.FromResult(ctx.Fail("not_ready", "blackboard store not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.BlackboardPostParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "blackboard post params required"));

        System.TimeSpan? ttl = p.TtlSeconds.HasValue ? System.TimeSpan.FromSeconds(p.TtlSeconds.Value) : null;
        var post = store.Post(p.WorkspaceId, p.Kind, p.Audience, p.Content, p.RefId, ttl);
        return Task.FromResult(ctx.Ok(post, CoveJsonContext.Default.BlackboardPost));
    }

    [CoveCommand("cove://commands/blackboard.show")]
    public static Task<ControlResponse> BlackboardShow(EngineDispatchContext ctx)
    {
        if (ctx.Blackboard is not { } store)
            return Task.FromResult(ctx.Fail("not_ready", "blackboard store not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.BlackboardShowParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "blackboard show params required"));

        var posts = store.Show(p.WorkspaceId, p.Audience);
        return Task.FromResult(ctx.Ok(new BlackboardShowResult(posts), CoveJsonContext.Default.BlackboardShowResult));
    }
    [CoveCommand("cove://commands/memory.add")]
    public static Task<ControlResponse> MemoryAdd(EngineDispatchContext ctx)
    {
        if (ctx.Memory is not { } store)
            return Task.FromResult(ctx.Fail("not_ready", "memory store not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.MemoryAddParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "memory add params required"));

        var fact = store.AddFact(new Fact { WorkspaceId = p.WorkspaceId, Kind = p.Kind, Content = p.Content, Confidence = p.Confidence ?? 0.5, Audience = p.Audience });
        return Task.FromResult(ctx.Ok(fact, CoveJsonContext.Default.Fact));
    }

    [CoveCommand("cove://commands/memory.search")]
    public static Task<ControlResponse> MemorySearch(EngineDispatchContext ctx)
    {
        if (ctx.MemoryRanker is not { } ranker)
            return Task.FromResult(ctx.Fail("not_ready", "memory ranker not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.MemorySearchParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "memory search params required"));

        var results = ranker.SearchRanked(p.WorkspaceId, p.Query, p.Limit ?? 20);
        var dtos = results.Select(r => new RankedFactDto(r.Fact.Id, r.Fact.Kind, r.Fact.Content, r.Score, r.Snippet)).ToList();
        return Task.FromResult(ctx.Ok(new MemorySearchResult(dtos), CoveJsonContext.Default.MemorySearchResult));
    }

    [CoveCommand("cove://commands/memory.recall")]
    public static Task<ControlResponse> MemoryRecall(EngineDispatchContext ctx)
    {
        if (ctx.MemoryRanker is not { } ranker)
            return Task.FromResult(ctx.Fail("not_ready", "memory ranker not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.MemoryRecallParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "memory recall params required"));

        var previews = ranker.Recall(p.WorkspaceId, p.Query, p.Limit ?? 10);
        var dtos = previews.Select(p => new RecallPreviewDto(p.Id, p.Kind, p.Preview, p.Score, p.HowLongAgo)).ToList();
        return Task.FromResult(ctx.Ok(new MemoryRecallResult(dtos), CoveJsonContext.Default.MemoryRecallResult));
    }

    [CoveCommand("cove://commands/memory.show")]
    public static Task<ControlResponse> MemoryShow(EngineDispatchContext ctx)
    {
        if (ctx.Memory is not { } store)
            return Task.FromResult(ctx.Fail("not_ready", "memory store not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.MemoryShowParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "memory show params required"));

        var fact = store.GetFact(p.WorkspaceId, p.Id);
        if (fact is null)
            return Task.FromResult(ctx.Fail("not_found", "fact not found"));
        return Task.FromResult(ctx.Ok(fact, CoveJsonContext.Default.Fact));
    }

    [CoveCommand("cove://commands/memory.supersede")]
    public static Task<ControlResponse> MemorySupersede(EngineDispatchContext ctx)
    {
        if (ctx.Memory is not { } store)
            return Task.FromResult(ctx.Fail("not_ready", "memory store not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.MemorySupersedeParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "memory supersede params required"));

        var newFact = store.Supersede(p.WorkspaceId, p.OldFactId, new Fact { WorkspaceId = p.WorkspaceId, Kind = p.Kind, Content = p.Content, Confidence = p.Confidence ?? 0.5 });
        if (newFact is null)
            return Task.FromResult(ctx.Fail("not_found", "old fact not found"));
        return Task.FromResult(ctx.Ok(newFact, CoveJsonContext.Default.Fact));
    }

    [CoveCommand("cove://commands/memory.reindex")]
    public static Task<ControlResponse> MemoryReindex(EngineDispatchContext ctx)
    {
        if (ctx.Memory is not { } store)
            return Task.FromResult(ctx.Fail("not_ready", "memory store not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.MemoryReindexParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "memory reindex params required"));

        store.ReindexFromDisk(p.WorkspaceId);
        return Task.FromResult(ctx.Ok());
    }

    [CoveCommand("cove://commands/memory.consolidate")]
    public static async Task<ControlResponse> MemoryConsolidate(EngineDispatchContext ctx)
    {
        if (ctx.Consolidator is not { } consolidator)
            return ctx.Fail("not_ready", "consolidator not available");
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.MemoryConsolidateParams) is not { } p)
            return ctx.Fail("invalid_params", "memory consolidate params required");

        var count = await consolidator.ConsolidateAsync(p.WorkspaceId, p.DryRun);
        return ctx.Ok(new MemoryConsolidateResult(count), CoveJsonContext.Default.MemoryConsolidateResult);
    }

    [CoveCommand("cove://commands/memory.propose")]
    public static Task<ControlResponse> MemoryPropose(EngineDispatchContext ctx)
    {
        if (ctx.Proposals is not { } proposals)
            return Task.FromResult(ctx.Fail("not_ready", "proposal store not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.MemoryProposeParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "memory propose params required"));

        var proposal = proposals.Create(p.WorkspaceId, p.Kind, p.Content);
        return Task.FromResult(ctx.Ok(proposal, CoveJsonContext.Default.Proposal));
    }

    [CoveCommand("cove://commands/memory.proposal.transition")]
    public static Task<ControlResponse> MemoryProposalTransition(EngineDispatchContext ctx)
    {
        if (ctx.Proposals is not { } proposals)
            return Task.FromResult(ctx.Fail("not_ready", "proposal store not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.MemoryProposalTransitionParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "memory proposal transition params required"));

        var ok = proposals.Transition(p.ProposalId, p.State);
        return Task.FromResult(ok ? ctx.Ok() : ctx.Fail("not_found", "proposal not found or already in target state"));
    }

    [CoveCommand("cove://commands/edits.find")]
    public static Task<ControlResponse> EditsFind(EngineDispatchContext ctx)
    {
        if (ctx.Edits is not { } index)
            return Task.FromResult(ctx.Fail("not_ready", "edits index not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.EditsFindParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "edits find params required"));

        var records = index.FindByFile(p.FilePath, p.Limit ?? 20);
        var dtos = records.Select(r => new EditRecordDto(r.SessionId, r.FilePath, r.Tool, r.Op, r.OccurredAt.ToString("o"), r.EditSummary)).ToList();
        return Task.FromResult(ctx.Ok(new EditsFindResult(dtos), CoveJsonContext.Default.EditsFindResult));
    }

    [CoveCommand("cove://commands/vault.search")]
    public static Task<ControlResponse> VaultSearch(EngineDispatchContext ctx)
    {
        if (ctx.Corpus is not { } corpus)
            return Task.FromResult(ctx.Fail("not_ready", "session corpus not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.VaultSearchParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "vault search params required"));
        var entries = corpus.SearchSessions(p.WorkspaceId, p.Query, p.Limit ?? 20);
        var dtos = entries.Select(e => new SessionCorpusEntryDto(e.Id, e.WorkspaceId, e.Adapter, e.StartedAt, e.EndedAt, e.ExtractorVersion)).ToList();
        return Task.FromResult(ctx.Ok(new VaultSearchResult(dtos), CoveJsonContext.Default.VaultSearchResult));
    }

    [CoveCommand("cove://commands/vault.resume")]
    public static async Task<ControlResponse> VaultResume(EngineDispatchContext ctx)
    {
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.VaultResumeParams) is not { } p)
            return ctx.Fail("invalid_params", "vault resume params required");
        if (string.IsNullOrWhiteSpace(p.Adapter) || string.IsNullOrWhiteSpace(p.SessionId))
            return ctx.Fail("invalid_params", "adapter and sessionId are required");
        if (ctx.ManifestStore is not { } manifests)
            return ctx.Fail("not_ready", "adapter manifest store not available");
        if (manifests.Load(p.Adapter) is null)
            return ctx.Fail("not_found", $"unknown adapter: {p.Adapter}");

        var overrides = new Cove.Engine.Restart.LauncherOverrides { WorkingDir = p.Cwd, Yolo = p.Yolo };
        var protocol = new Cove.Engine.Launch.AdapterResumeProtocol(manifests, new Cove.Adapters.MethodRunner());
        try
        {
            var cmd = await protocol.BuildResumeCommandAsync(p.Adapter, p.SessionId, overrides).ConfigureAwait(false);
            return ctx.Ok(new VaultResumeResult(true, p.Adapter, ToArgv(cmd), cmd.Cwd, "none", null), CoveJsonContext.Default.VaultResumeResult);
        }
        catch (Cove.Engine.Restart.ResumeFailedException ex)
        {
            var fresh = await BuildFreshLaunchAsync(ctx, p.Adapter, overrides).ConfigureAwait(false);
            if (fresh is null)
                return ctx.Fail("resume_failed", ex.Message);
            return ctx.Ok(new VaultResumeResult(true, p.Adapter, ToArgv(fresh), fresh.Cwd, "fresh", ex.Message), CoveJsonContext.Default.VaultResumeResult);
        }
    }

    private static string[] ToArgv(Cove.Engine.Restart.ResumeCommand cmd)
    {
        var argv = new System.Collections.Generic.List<string>(1 + cmd.Args.Count) { cmd.Command };
        argv.AddRange(cmd.Args);
        return argv.ToArray();
    }

    private static async Task<Cove.Engine.Restart.ResumeCommand?> BuildFreshLaunchAsync(EngineDispatchContext ctx, string adapter, Cove.Engine.Restart.LauncherOverrides overrides)
    {
        if (ctx.Launcher is not { } orch || ctx.LaunchProfiles is not { } profiles)
            return null;
        var profile = profiles.Load(adapter, "default")
            ?? new Cove.Adapters.LaunchProfile("Default", "default", adapter, true, null, null,
                System.Array.Empty<string>(), new System.Collections.Generic.Dictionary<string, string>(),
                new System.Collections.Generic.Dictionary<string, bool>(), System.Array.Empty<string>(), null, 1);
        try
        {
            return await orch.BuildLaunchCommandAsync(profile, overrides).ConfigureAwait(false);
        }
        catch (Cove.Engine.Restart.ResumeFailedException)
        {
            return null;
        }
    }

    [CoveCommand("cove://commands/vault.set-setting")]
    public static Task<ControlResponse> VaultSetSetting(EngineDispatchContext ctx)
    {
        if (ctx.VaultSettings is not { } settings)
            return Task.FromResult(ctx.Fail("not_ready", "vault settings store not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.VaultSetSettingParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "vault set-setting params required"));

        settings.Set(p.Key, p.Value);
        return Task.FromResult(ctx.Ok());
    }

    [CoveCommand("cove://commands/vault.reindex")]
    public static Task<ControlResponse> VaultReindex(EngineDispatchContext ctx)
    {
        if (ctx.Corpus is not { } corpus)
            return Task.FromResult(ctx.Fail("not_ready", "session corpus not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.VaultReindexParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "vault reindex params required"));

        var version = ctx.VaultSettings?.Get()?.ExtractorVersion ?? "latest";
        corpus.ReindexIfVersionChanged(p.WorkspaceId, version);
        return Task.FromResult(ctx.Ok());
    }

    [CoveCommand("cove://commands/library.list")]
    public static Task<ControlResponse> LibraryList(EngineDispatchContext ctx)
    {
        if (ctx.Library is not { } store)
            return Task.FromResult(ctx.Fail("not_ready", "library store not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.LibraryListParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "library list params required"));
        var entries = store.ListByWorkspace(p.WorkspaceId, p.Kind);
        var dtos = entries.Select(e => new LibraryEntryDto(e.Id, e.WorkspaceId, e.PaneId, e.PaneType, e.Title, e.StateJson, e.Scrollback, e.Kind, e.CapturedAt.ToString("o"))).ToList();
        return Task.FromResult(ctx.Ok(new LibraryListResult(dtos), CoveJsonContext.Default.LibraryListResult));
    }

    [CoveCommand("cove://commands/library.materialize")]
    public static Task<ControlResponse> LibraryMaterialize(EngineDispatchContext ctx)
    {
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.LibraryMaterializeParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "library materialize params required"));
        return Task.FromResult(ctx.Ok());
    }
    [CoveCommand("cove://commands/review.add-comment")]
    public static Task<ControlResponse> ReviewAddComment(EngineDispatchContext ctx)
    {
        if (ctx.Reviews is not { } store)
            return Task.FromResult(ctx.Fail("not_ready", "review store not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.ReviewAddCommentParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "review add-comment params required"));
        var comment = store.AddComment(p.CommitSha, p.FilePath, p.Line, p.Author, p.Body, p.ParentId);
        var dto = new ReviewCommentDto(comment.Id, comment.RootId, comment.ParentId, comment.CommitSha, comment.FilePath, comment.Line, comment.Author, comment.Body, comment.State, comment.CreatedAt.ToString("o"), comment.OrphanedAt?.ToString("o"), comment.HunkId, comment.ContextHash);
        return Task.FromResult(ctx.Ok(dto, CoveJsonContext.Default.ReviewCommentDto));
    }
    [CoveCommand("cove://commands/review.list-comments")]
    public static Task<ControlResponse> ReviewListComments(EngineDispatchContext ctx)
    {
        if (ctx.Reviews is not { } store)
            return Task.FromResult(ctx.Fail("not_ready", "review store not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.ReviewListCommentsParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "review list-comments params required"));
        var comments = store.ListComments(p.CommitSha, p.FilePath, p.State);
        var dtos = comments.Select(c => new ReviewCommentDto(c.Id, c.RootId, c.ParentId, c.CommitSha, c.FilePath, c.Line, c.Author, c.Body, c.State, c.CreatedAt.ToString("o"), c.OrphanedAt?.ToString("o"), c.HunkId, c.ContextHash)).ToList();
        return Task.FromResult(ctx.Ok(new ReviewListCommentsResult(dtos), CoveJsonContext.Default.ReviewListCommentsResult));
    }
    [CoveCommand("cove://commands/review.resolve")]
    public static Task<ControlResponse> ReviewResolve(EngineDispatchContext ctx)
    {
        if (ctx.Reviews is not { } store)
            return Task.FromResult(ctx.Fail("not_ready", "review store not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.ReviewTransitionParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "review transition params required"));
        store.ResolveComment(p.CommentId, p.Actor);
        return Task.FromResult(ctx.Ok());
    }
    [CoveCommand("cove://commands/review.reopen")]
    public static Task<ControlResponse> ReviewReopen(EngineDispatchContext ctx)
    {
        if (ctx.Reviews is not { } store)
            return Task.FromResult(ctx.Fail("not_ready", "review store not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.ReviewTransitionParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "review transition params required"));
        store.ReopenComment(p.CommentId, p.Actor);
        return Task.FromResult(ctx.Ok());
    }
    [CoveCommand("cove://commands/review.close")]
    public static Task<ControlResponse> ReviewClose(EngineDispatchContext ctx)
    {
        if (ctx.Reviews is not { } store)
            return Task.FromResult(ctx.Fail("not_ready", "review store not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.ReviewTransitionParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "review transition params required"));
        store.CloseComment(p.CommentId, p.Actor);
        return Task.FromResult(ctx.Ok());
    }
    [CoveCommand("cove://commands/review.re-anchor")]
    public static Task<ControlResponse> ReviewReAnchor(EngineDispatchContext ctx)
    {
        if (ctx.Reviews is not { } store)
            return Task.FromResult(ctx.Fail("not_ready", "review store not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.ReviewReAnchorParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "review re-anchor params required"));
        store.ReAnchorComment(p.CommentId, p.NewLine);
        return Task.FromResult(ctx.Ok());
    }
    [CoveCommand("cove://commands/review.audit")]
    public static Task<ControlResponse> ReviewAudit(EngineDispatchContext ctx)
    {
        if (ctx.Reviews is not { } store)
            return Task.FromResult(ctx.Fail("not_ready", "review store not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.ReviewTransitionParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "review transition params required"));
        var audit = store.GetAuditTrail(p.CommentId);
        var dtos = audit.Select(a => new ReviewAuditDto(a.Id, a.CommentId, a.FromState, a.ToState, a.Actor, a.At.ToString("o"), a.Note)).ToList();
        return Task.FromResult(ctx.Ok(new ReviewAuditResult(dtos), CoveJsonContext.Default.ReviewAuditResult));
    }
    [CoveCommand("cove://commands/review.telemetry")]
    public static Task<ControlResponse> ReviewTelemetry(EngineDispatchContext ctx)
    {
        if (ctx.Reviews is not { } store)
            return Task.FromResult(ctx.Fail("not_ready", "review store not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.ReviewTelemetryParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "review telemetry params required"));
        store.AddTelemetry(p.CommitSha, p.SessionId, p.Adapter, p.FilesTouched);
        var telemetry = store.GetTelemetry(p.CommitSha);
        var dtos = telemetry.Select(t => new ReviewTelemetryDto(t.SessionId, t.Adapter, t.FilesTouched)).ToList();
        return Task.FromResult(ctx.Ok(new ReviewTelemetryResult(dtos), CoveJsonContext.Default.ReviewTelemetryResult));
    }
    [CoveCommand("cove://commands/attribution.record")]
    public static Task<ControlResponse> AttributionRecord(EngineDispatchContext ctx)
    {
        if (ctx.Attribution is not { } idx)
            return Task.FromResult(ctx.Fail("not_ready", "attribution index not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.AttributionRecordParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "attribution record params required"));
        var entry = idx.Record(p.SessionId, p.ToolUseId, p.FilePath, p.StartLine, p.EndLine);
        var dto = new AttributionEntryDto(entry.Id, entry.SessionId, entry.ToolUseId, entry.FilePath, entry.StartLine, entry.EndLine, entry.At.ToString("o"));
        return Task.FromResult(ctx.Ok(dto, CoveJsonContext.Default.AttributionEntryDto));
    }

    [CoveCommand("cove://commands/attribution.find-by-line")]
    public static Task<ControlResponse> AttributionFindByLine(EngineDispatchContext ctx)
    {
        if (ctx.Attribution is not { } idx)
            return Task.FromResult(ctx.Fail("not_ready", "attribution index not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.AttributionFindByLineParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "attribution find-by-line params required"));
        var entry = idx.FindByLine(p.FilePath, p.Line);
        if (entry is null)
            return Task.FromResult(ctx.Fail("not_found", "no attribution for this line"));
        var dto = new AttributionEntryDto(entry.Id, entry.SessionId, entry.ToolUseId, entry.FilePath, entry.StartLine, entry.EndLine, entry.At.ToString("o"));
        return Task.FromResult(ctx.Ok(dto, CoveJsonContext.Default.AttributionEntryDto));
    }

    [CoveCommand("cove://commands/attribution.find-by-range")]
    public static Task<ControlResponse> AttributionFindByRange(EngineDispatchContext ctx)
    {
        if (ctx.Attribution is not { } idx)
            return Task.FromResult(ctx.Fail("not_ready", "attribution index not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.AttributionFindByRangeParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "attribution find-by-range params required"));
        var entries = idx.FindByRange(p.FilePath, p.StartLine, p.EndLine);
        var dtos = entries.Select(e => new AttributionEntryDto(e.Id, e.SessionId, e.ToolUseId, e.FilePath, e.StartLine, e.EndLine, e.At.ToString("o"))).ToList();
        return Task.FromResult(ctx.Ok(new AttributionListResult(dtos), CoveJsonContext.Default.AttributionListResult));
    }

    [CoveCommand("cove://commands/attribution.find-by-tool-use")]
    public static Task<ControlResponse> AttributionFindByToolUse(EngineDispatchContext ctx)
    {
        if (ctx.Attribution is not { } idx)
            return Task.FromResult(ctx.Fail("not_ready", "attribution index not available"));
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.AttributionFindByToolUseParams) is not { } p)
            return Task.FromResult(ctx.Fail("invalid_params", "attribution find-by-tool-use params required"));
        var entries = idx.FindByToolUse(p.ToolUseId);
        var dtos = entries.Select(e => new AttributionEntryDto(e.Id, e.SessionId, e.ToolUseId, e.FilePath, e.StartLine, e.EndLine, e.At.ToString("o"))).ToList();
        return Task.FromResult(ctx.Ok(new AttributionListResult(dtos), CoveJsonContext.Default.AttributionListResult));
    }

    [CoveCommand("cove://commands/review.dispatch")]
    public static async Task<ControlResponse> ReviewDispatch(EngineDispatchContext ctx)
    {
        if (ctx.ReviewDispatcher is not { } dispatcher)
            return ctx.Fail("not_ready", "review dispatcher not available");
        if (ctx.Panes is not { } panes)
            return ctx.Fail("not_ready", "pane registry not available");
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(CoveJsonContext.Default.ReviewDispatchParams) is not { } p)
            return ctx.Fail("invalid_params", "review dispatch params required");

        var request = new ReviewDispatchRequest(p.TargetPaneId, p.WorkspaceId, p.SessionId, p.TaskRunId, p.Message, p.CommitSha);
        var result = await dispatcher.DispatchAsync(request, (paneId, bytes) =>
        {
            panes.Write(paneId, bytes);
            return Task.CompletedTask;
        });
        var dto = new ReviewDispatchResultDto(result.DispatchId, result.TargetPaneId, result.SessionId, result.TaskRunId, result.DispatchedAt.ToString("o"));
        return ctx.Ok(dto, CoveJsonContext.Default.ReviewDispatchResultDto);
    }
}
