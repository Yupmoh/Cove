using Cove.Persistence;
using Cove.Tasks.Store;
using Dapper;
using Microsoft.Data.Sqlite;

namespace Cove.Tasks.Schedules;

public sealed class ScheduleRow
{
    public string CardId { get; set; } = "";
    public string TriggerKind { get; set; } = "immediate";
    public string? Cron { get; set; }
    public string? Tz { get; set; }
    public string? At { get; set; }
    public string CompletionRule { get; set; } = "loop";
    public string MarkDoneBy { get; set; } = "agent";
    public bool BlockOverlap { get; set; } = true;
    public string? HomeStatusId { get; set; }
    public bool Paused { get; set; }
    public bool SkipNext { get; set; }
    public string? NextFireAt { get; set; }
    public string? PendingIntent { get; set; }
    public string? LastFiredAt { get; set; }
    public string CreatedAt { get; set; } = "";
    public string UpdatedAt { get; set; } = "";
}

public sealed class ScheduleRepository
{
    private readonly SqliteConnectionFactory _factory;
    private readonly TasksWriteChannel? _channel;

    private const string SelectColumns = "card_id AS CardId, trigger_kind AS TriggerKind, cron AS Cron, tz AS Tz, at AS At, completion_rule AS CompletionRule, mark_done_by AS MarkDoneBy, block_overlap AS BlockOverlap, home_status_id AS HomeStatusId, paused AS Paused, skip_next AS SkipNext, next_fire_at AS NextFireAt, pending_intent AS PendingIntent, last_fired_at AS LastFiredAt, created_at AS CreatedAt, updated_at AS UpdatedAt";

    private const string UpsertSql = "INSERT INTO card_schedules (card_id, trigger_kind, cron, tz, at, completion_rule, mark_done_by, block_overlap, home_status_id, paused, skip_next, next_fire_at, pending_intent, last_fired_at, created_at, updated_at) VALUES (@CardId, @TriggerKind, @Cron, @Tz, @At, @CompletionRule, @MarkDoneBy, @BlockOverlap, @HomeStatusId, @Paused, @SkipNext, @NextFireAt, @PendingIntent, @LastFiredAt, @CreatedAt, @UpdatedAt) ON CONFLICT(card_id) DO UPDATE SET trigger_kind=@TriggerKind, cron=@Cron, tz=@Tz, at=@At, completion_rule=@CompletionRule, mark_done_by=@MarkDoneBy, block_overlap=@BlockOverlap, home_status_id=@HomeStatusId, paused=@Paused, skip_next=@SkipNext, next_fire_at=@NextFireAt, pending_intent=@PendingIntent, last_fired_at=@LastFiredAt, updated_at=@UpdatedAt";

    public ScheduleRepository(SqliteConnectionFactory factory, TasksWriteChannel? channel = null)
    {
        _factory = factory;
        _channel = channel;
    }

    public System.Threading.Tasks.Task UpsertAsync(ScheduleRow row)
    {
        row.CreatedAt = System.DateTimeOffset.UtcNow.ToString("o");
        row.UpdatedAt = System.DateTimeOffset.UtcNow.ToString("o");
        if (_channel is null)
        {
            using var conn = _factory.Open();
            UpsertInternal(conn, row);
            return System.Threading.Tasks.Task.CompletedTask;
        }
        return _channel.ExecuteAsync(conn => { UpsertInternal(conn, row); return System.Threading.Tasks.Task.CompletedTask; });
    }

    private static void UpsertInternal(SqliteConnection conn, ScheduleRow row)
    {
        conn.Execute(UpsertSql, row);
    }

    public ScheduleRow? GetByCard(string cardId)
    {
        using var conn = _factory.Open();
        return conn.QueryFirstOrDefault<ScheduleRow>(
            $"SELECT {SelectColumns} FROM card_schedules WHERE card_id = @CardId",
            new { CardId = cardId });
    }

    public System.Collections.Generic.IReadOnlyList<ScheduleRow> ListDue(System.DateTimeOffset now)
    {
        using var conn = _factory.Open();
        var nowStr = now.ToString("o");
        return conn.Query<ScheduleRow>(
            $"SELECT {SelectColumns} FROM card_schedules WHERE paused = 0 AND next_fire_at IS NOT NULL AND next_fire_at <= @Now ORDER BY next_fire_at",
            new { Now = nowStr }).AsList();
    }

    public System.Threading.Tasks.Task UpdateAsync(string cardId, bool? paused, bool? skipNext, string? nextFireAt, string? lastFiredAt, string? pendingIntent = null)
    {
        if (_channel is null)
        {
            using var conn = _factory.Open();
            UpdateInternal(conn, cardId, paused, skipNext, nextFireAt, lastFiredAt, pendingIntent);
            return System.Threading.Tasks.Task.CompletedTask;
        }
        return _channel.ExecuteAsync(conn => { UpdateInternal(conn, cardId, paused, skipNext, nextFireAt, lastFiredAt, pendingIntent); return System.Threading.Tasks.Task.CompletedTask; });
    }

    private const string UpdateSql =
        "UPDATE card_schedules SET " +
        "paused = CASE WHEN @SetPaused = 1 THEN @Paused ELSE paused END, " +
        "skip_next = CASE WHEN @SetSkipNext = 1 THEN @SkipNext ELSE skip_next END, " +
        "next_fire_at = CASE WHEN @SetNextFireAt = 1 THEN @NextFireAt ELSE next_fire_at END, " +
        "last_fired_at = CASE WHEN @SetLastFiredAt = 1 THEN @LastFiredAt ELSE last_fired_at END, " +
        "pending_intent = CASE WHEN @SetPendingIntent = 1 THEN @PendingIntent ELSE pending_intent END, " +
        "updated_at = @Now WHERE card_id = @CardId";

    private static void UpdateInternal(SqliteConnection conn, string cardId, bool? paused, bool? skipNext, string? nextFireAt, string? lastFiredAt, string? pendingIntent)
    {
        conn.Execute(UpdateSql, new
        {
            CardId = cardId,
            Now = System.DateTimeOffset.UtcNow.ToString("o"),
            SetPaused = paused is not null ? 1 : 0,
            Paused = paused == true ? 1 : 0,
            SetSkipNext = skipNext is not null ? 1 : 0,
            SkipNext = skipNext == true ? 1 : 0,
            SetNextFireAt = nextFireAt is not null ? 1 : 0,
            NextFireAt = nextFireAt,
            SetLastFiredAt = lastFiredAt is not null ? 1 : 0,
            LastFiredAt = lastFiredAt,
            SetPendingIntent = pendingIntent is not null ? 1 : 0,
            PendingIntent = pendingIntent,
        });
    }

    public System.Threading.Tasks.Task DeleteAsync(string cardId)
    {
        if (_channel is null)
        {
            using var conn = _factory.Open();
            conn.Execute("DELETE FROM card_schedules WHERE card_id = @CardId", new { CardId = cardId });
            return System.Threading.Tasks.Task.CompletedTask;
        }
        return _channel.ExecuteAsync(conn => { conn.Execute("DELETE FROM card_schedules WHERE card_id = @CardId", new { CardId = cardId }); return System.Threading.Tasks.Task.CompletedTask; });
    }
}
