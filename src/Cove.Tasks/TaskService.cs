using Cove.Persistence;
using Cove.Tasks.Store;
using Microsoft.Extensions.Logging;

namespace Cove.Tasks;

public sealed class TaskService : IAsyncDisposable
{
    private readonly TasksWriteChannel _channel;
    private readonly CardRepository _cards;
    private readonly LabelRepository _labels;
    private readonly StatusRepository _statuses;
    private readonly Runs.RunRepository _runs;
    private readonly Runs.RunSegmentRepository _segments;
    private readonly CommentRepository _comments;
    private readonly Schedules.ScheduleRepository _schedules;
    private readonly TaskMutationRepository _mutations;
    private readonly Export.TaskBoardExportService _exports;

    public TaskService(string dataDir, ILogger logger)
    {
        var dbPath = System.IO.Path.Combine(dataDir, "tasks.db");
        var factory = new SqliteConnectionFactory(dbPath);
        var store = new TasksStore(factory, logger);
        store.EnsureSchema();
        _channel = new TasksWriteChannel(factory, logger);
        _mutations = new TaskMutationRepository(_channel);
        _cards = new CardRepository(factory, _channel);
        _statuses = new StatusRepository(factory, _channel);
        _comments = new CommentRepository(factory, _channel, _cards);
        _labels = new LabelRepository(factory, _channel);
        _runs = new Runs.RunRepository(factory, _channel);
        _segments = new Runs.RunSegmentRepository(factory, _channel);
        _schedules = new Schedules.ScheduleRepository(factory, _channel);
        _exports = new Export.TaskBoardExportService(
            new Export.SqliteTaskBoardExportRepository(factory),
            logger);
    }

    public System.Threading.Tasks.Task StartAsync() => _channel.StartAsync();

    public ValueTask DisposeAsync() => _channel.DisposeAsync();

    public System.Threading.Tasks.Task<CardRow> CreateCardAsync(string bayId, string title, string source, string? description, int priority, int size, string? assignee, string statusId = "todo")
        => _mutations.CreateCardAsync(bayId, title, source, description, priority, size, assignee, statusId);

    public CardRow? GetCard(string id) => _cards.GetById(id);
    public CardRow? GetCardByHumanId(string bayId, int number) => _cards.GetByBayAndNumber(bayId, number);
    public IReadOnlyList<CardRow> ListCards(string bayId) => _cards.ListByBay(bayId);
    public IReadOnlyList<CardRow> ListCardsByStatus(string bayId, string statusId) => _cards.ListByStatus(bayId, statusId);

    public async System.Threading.Tasks.Task<int> UpdateCardAsync(CardRow row)
    {
        row.UpdatedAt = System.DateTimeOffset.UtcNow.ToString("o");
        return await _cards.UpdateAsync(row);
    }

    public async System.Threading.Tasks.Task<int> DeleteCardAsync(string id) => await _cards.DeleteAsync(id);

    public System.Threading.Tasks.Task<Cove.Tasks.Store.StatusRow?> CreateStatusAsync(string bayId, string id, string name, string hexColor, double position)
        => _mutations.CreateStatusAsync(bayId, id, name, hexColor, position);

    public System.Collections.Generic.IReadOnlyList<Cove.Tasks.Store.StatusRow> ListStatuses(string bayId, bool includeHidden = false)
        => _statuses.ListByBay(bayId, includeHidden);

    public System.Threading.Tasks.Task DeleteStatusAsync(string bayId, string id, string? rehomeToStatusId)
        => _statuses.DeleteAsync(bayId, id, rehomeToStatusId);

    public System.Threading.Tasks.Task ReorderStatusesAsync(string bayId, string[] orderedIds)
        => _statuses.ReorderAsync(bayId, orderedIds);

    public System.Threading.Tasks.Task SetStatusHiddenAsync(string bayId, string id, bool hidden)
        => _statuses.SetHiddenAsync(bayId, id, hidden);
    public System.Threading.Tasks.Task<Cove.Tasks.Store.LabelRow?> CreateLabelAsync(string bayId, string id, string name, string hexColor, double position)
        => _labels.CreateAsync(bayId, id, name, hexColor, position);

    public System.Collections.Generic.IReadOnlyList<Cove.Tasks.Store.LabelRow> ListLabels(string bayId)
        => _labels.ListByBay(bayId);

    public System.Threading.Tasks.Task DeleteLabelAsync(string bayId, string id)
        => _labels.DeleteAsync(bayId, id);

    public System.Threading.Tasks.Task AssignLabelAsync(string cardId, string labelId)
        => _labels.AssignToCardAsync(cardId, labelId);

    public System.Threading.Tasks.Task UnassignLabelAsync(string cardId, string labelId)
        => _labels.UnassignFromCardAsync(cardId, labelId);

    public System.Collections.Generic.IReadOnlyList<Cove.Tasks.Store.LabelRow> GetLabelsForCard(string cardId)
        => _labels.GetLabelsForCard(cardId);

    public System.Collections.Generic.IReadOnlyList<string> FilterCardsByLabel(string bayId, string labelId)
        => _labels.FilterCardsByLabel(bayId, labelId);

    public System.Threading.Tasks.Task ReorderLabelsAsync(string bayId, string[] orderedIds)
        => _labels.ReorderAsync(bayId, orderedIds);

    public System.Threading.Tasks.Task<Cove.Tasks.Store.CommentRow?> AddCommentAsync(string cardId, string kind, string body, string source)
        => _comments.AddAsync(cardId, kind, body, source);

    public System.Collections.Generic.IReadOnlyList<Cove.Tasks.Store.CommentRow> ListComments(string cardId)
        => _comments.ListByCard(cardId);

    public int CountComments(string cardId)
        => _comments.CountByCard(cardId);

    public System.Threading.Tasks.Task DeleteCommentAsync(string id)
        => _comments.DeleteAsync(id);

    public LaunchConfig.LaunchConfigModel? GetLaunchConfig(string cardId)
    {
        var card = _cards.GetById(cardId);
        return card is null ? null : LaunchConfig.LaunchConfigSerializer.Deserialize(card.LaunchConfigJson);
    }

    public async System.Threading.Tasks.Task<LaunchConfig.LaunchConfigValidationResult> SetLaunchConfigAsync(string cardId, LaunchConfig.LaunchConfigModel config, LaunchConfig.LaunchConfigValidationContext context)
    {
        var result = LaunchConfig.LaunchConfigValidator.Validate(config, context);
        if (!result.IsValid)
            return result;
        var card = _cards.GetById(cardId);
        if (card is null)
            return new LaunchConfig.LaunchConfigValidationResult(false, ["card not found"]);
        card.LaunchConfigJson = LaunchConfig.LaunchConfigSerializer.Serialize(config);
        if (config.ProfileSlug is not null) card.ProfileSlug = config.ProfileSlug;
        if (config.Adapter is not null) card.AgentRef = config.Adapter;
        await _cards.UpdateAsync(card);
        return result;
    }

    public async System.Threading.Tasks.Task SetBindingAsync(string cardId, string? agentRef, System.Collections.Generic.IReadOnlyList<SkillSelection> skills, string? profileSlug)
        => await SkillsBinder.BindAsync(_cards, cardId, agentRef, skills, profileSlug);

    public TaskBinding GetBinding(string cardId)
        => SkillsBinder.GetBinding(_cards, cardId);

    public TaskProfilePayload ResolveTaskProfile(string cardId)
    {
        var card = _cards.GetById(cardId);
        return card is null ? new TaskProfilePayload(null, null, []) : SkillsBinder.ResolveTaskProfile(card);
    }

    public Runs.RunRow? GetRun(string id) => _runs.GetById(id);
    public Runs.RunRow? FindRunByPrefix(string prefix) => _runs.FindByPrefix(prefix);
    public Runs.RunRow? GetRunByNook(string nookId)
    {
        var segment = _segments.GetByNookId(nookId);
        return segment is not null ? _runs.GetById(segment.RunId) : null;
    }
    public Runs.RunRow? GetActiveRunForCard(string cardId)
    {
        var active = _runs.ListByCardAndState(cardId, "active");
        if (active.Count > 0) return active[0];
        var interrupted = _runs.ListByCardAndState(cardId, "interrupted");
        return interrupted.Count > 0 ? interrupted[0] : null;
    }
    public System.Threading.Tasks.Task TransitionRunAsync(string runId, Runs.RunState newState) => _runs.TransitionAsync(runId, newState);
    public System.Threading.Tasks.Task SetPendingPromptAsync(string runId, string? prompt) => _runs.SetPendingPromptAsync(runId, prompt);
    public System.Collections.Generic.IReadOnlyList<Runs.RunRow> ListRuns(string? taskId, string? bayId, string? state)
    {
        if (taskId is not null && state is not null) return _runs.ListByCardAndState(taskId, state);
        if (taskId is not null) return _runs.ListByCard(taskId);
        if (bayId is not null && state is not null) return _runs.ListByBayAndState(bayId, state);
        if (bayId is not null) return _runs.ListByBay(bayId);
        if (state is not null) return _runs.ListByState(state);
        return [];
    }
    public System.Collections.Generic.IReadOnlyList<Runs.RunSegmentRow> ListRunSegments(string runId) => _segments.ListByRun(runId);
    public bool HasActiveRun(string cardId) => _runs.HasActiveRun(cardId);
    public System.Threading.Tasks.Task<Runs.RunRow?> CreateRunAsync(string cardId, string bayId, string? launchProfileJson, string? runFamilyId = null, bool backgrounded = false, string? reviewStatusId = null, string? completionStatusId = null) => _runs.CreateAsync(cardId, bayId, launchProfileJson, runFamilyId, backgrounded, reviewStatusId, completionStatusId);
    public System.Threading.Tasks.Task<Runs.RunSegmentRow?> AddRunSegmentAsync(string runId, string? nookId, string? adapterSessionId) => _segments.AddAsync(runId, nookId, adapterSessionId);
    public System.Threading.Tasks.Task EndRunSegmentAsync(string segmentId) => _segments.EndAsync(segmentId);
    public System.Threading.Tasks.Task<Runs.RunSegmentRow> CompleteDispatchAsync(string runId, string nookId, string? adapterSessionId, string cardId, string statusId)
        => _mutations.CompleteDispatchAsync(runId, nookId, adapterSessionId, cardId, statusId);
    public System.Threading.Tasks.Task<Runs.RunRow> CreateScheduledRunAndAdvanceAsync(CardRow card, string? nextFireAt, string lastFiredAt)
        => _mutations.CreateScheduledRunAndAdvanceAsync(card, nextFireAt, lastFiredAt);

    public Schedules.ScheduleRow? GetSchedule(string cardId) => _schedules.GetByCard(cardId);
    public System.Threading.Tasks.Task UpsertScheduleAsync(Schedules.ScheduleRow row) => _schedules.UpsertAsync(row);
    public System.Threading.Tasks.Task UpdateScheduleAsync(string cardId, bool? paused, bool? skipNext, string? nextFireAt, string? lastFiredAt, string? pendingIntent = null) => _schedules.UpdateAsync(cardId, paused, skipNext, nextFireAt, lastFiredAt, pendingIntent);
    public System.Threading.Tasks.Task DeleteScheduleAsync(string cardId) => _schedules.DeleteAsync(cardId);
    public System.Collections.Generic.IReadOnlyList<Schedules.ScheduleRow> ListActiveSchedules() => _schedules.ListActive();
    public System.Collections.Generic.IReadOnlyList<Schedules.ScheduleRow> ListPendingSchedules() => _schedules.ListPending();
    public System.Threading.Tasks.Task CompleteScheduleIntentAsync(string cardId, string? nextFireAt)
        => _schedules.CompleteIntentAsync(cardId, nextFireAt);
    public System.Threading.Tasks.Task<Export.ExportResult> ExportTaskBoardAsync(string exportPath, int bayCount)
        => _channel.ExecuteAsync(
            _ => System.Threading.Tasks.Task.FromResult(_exports.Export(exportPath, bayCount)));
    public System.Threading.Tasks.Task<Export.RestoreDiffResult> DiffTaskBoardAsync(string importPath)
        => _channel.ExecuteAsync(
            _ => System.Threading.Tasks.Task.FromResult(_exports.DiffAgainst(importPath)));
}
