using Cove.Tasks.Runs;
using Dapper;

namespace Cove.Tasks.Store;

internal sealed class TaskMutationRepository
{
    private readonly TasksWriteChannel _channel;
    private static readonly (string Id, string Name, string Color, int Position, int InProgress, int Looping, int Review, int Completion)[] DefaultStatuses =
    [
        ("todo", "Todo", "808080", 0, 0, 0, 0, 0),
        ("in-progress", "In Progress", "4a9eff", 1, 1, 0, 0, 0),
        ("in-review", "In Review", "f5a623", 2, 0, 0, 1, 0),
        ("done", "Done", "34c759", 3, 0, 0, 0, 1),
        ("looping", "Looping", "9b59b6", 4, 0, 1, 0, 0),
    ];

    public TaskMutationRepository(TasksWriteChannel channel)
    {
        _channel = channel;
    }

    public System.Threading.Tasks.Task<CardRow> CreateCardAsync(
        string bayId,
        string title,
        string source,
        string? description,
        int priority,
        int size,
        string? assignee,
        string statusId)
    {
        return _channel.ExecuteTransactionAsync((conn, transaction) =>
        {
            var now = System.DateTimeOffset.UtcNow.ToString("o");
            SeedDefaultStatuses(conn, transaction, bayId, now);
            var number = conn.ExecuteScalar<int>(
                """
                INSERT INTO task_counter (bay_id, next_number)
                VALUES (@BayId, 2)
                ON CONFLICT(bay_id) DO UPDATE SET next_number = next_number + 1
                RETURNING next_number - 1
                """,
                new { BayId = bayId },
                transaction);
            var minimumOrder = conn.ExecuteScalar<double?>(
                "SELECT MIN(order_key) FROM cards WHERE bay_id = @BayId AND status_id = @StatusId",
                new { BayId = bayId, StatusId = statusId },
                transaction);
            var row = new CardRow
            {
                Id = System.Guid.NewGuid().ToString("N"),
                BayId = bayId,
                TaskNumber = number,
                Title = title,
                Description = description ?? "",
                StatusId = statusId,
                Priority = priority,
                Size = size,
                Assignee = assignee,
                Source = source,
                OrderKey = minimumOrder.HasValue ? minimumOrder.Value - 1.0 : 0.0,
                CreatedAt = now,
                UpdatedAt = now,
            };
            conn.Execute(
                "INSERT INTO cards (id, bay_id, task_number, title, description, status_id, priority, size, assignee, source, order_key, current_primary_run_id, launch_config_json, agent_ref, skill_selection_json, profile_slug, due_at, attachments_json, comment_ids_json, created_at, updated_at) VALUES (@Id, @BayId, @TaskNumber, @Title, @Description, @StatusId, @Priority, @Size, @Assignee, @Source, @OrderKey, @CurrentPrimaryRunId, @LaunchConfigJson, @AgentRef, @SkillSelectionJson, @ProfileSlug, @DueAt, @AttachmentsJson, @CommentIdsJson, @CreatedAt, @UpdatedAt)",
                row,
                transaction);
            return System.Threading.Tasks.Task.FromResult(row);
        });
    }

    public System.Threading.Tasks.Task<StatusRow?> CreateStatusAsync(
        string bayId,
        string id,
        string name,
        string hexColor,
        double position)
    {
        return _channel.ExecuteTransactionAsync<StatusRow?>((conn, transaction) =>
        {
            var now = System.DateTimeOffset.UtcNow.ToString("o");
            SeedDefaultStatuses(conn, transaction, bayId, now);
            try
            {
                conn.Execute(
                    "INSERT INTO statuses (bay_id, id, name, hex_color, position, hidden, is_looping, is_in_progress, is_review, is_completion, created_at, updated_at) VALUES (@BayId, @Id, @Name, @HexColor, @Position, 0, 0, 0, 0, 0, @Now, @Now)",
                    new
                    {
                        BayId = bayId,
                        Id = id,
                        Name = name,
                        HexColor = hexColor,
                        Position = position,
                        Now = now,
                    },
                    transaction);
            }
            catch (Microsoft.Data.Sqlite.SqliteException)
            {
                return System.Threading.Tasks.Task.FromResult<StatusRow?>(null);
            }

            return System.Threading.Tasks.Task.FromResult<StatusRow?>(new StatusRow
            {
                BayId = bayId,
                Id = id,
                Name = name,
                HexColor = hexColor,
                Position = position,
                CreatedAt = now,
                UpdatedAt = now,
            });
        });
    }

    public System.Threading.Tasks.Task<Runs.RunSegmentRow> CompleteDispatchAsync(
        string runId,
        string nookId,
        string? adapterSessionId,
        string cardId,
        string statusId)
    {
        return _channel.ExecuteTransactionAsync((conn, transaction) =>
        {
            var now = System.DateTimeOffset.UtcNow.ToString("o");
            var segment = new Runs.RunSegmentRow
            {
                Id = System.Guid.NewGuid().ToString("N"),
                RunId = runId,
                NookId = nookId,
                AdapterSessionId = adapterSessionId,
                StartedAt = now,
                CreatedAt = now,
            };
            conn.Execute(
                "INSERT INTO task_run_segments (id, run_id, nook_id, adapter_session_id, started_at, ended_at, created_at) VALUES (@Id, @RunId, @NookId, @AdapterSessionId, @StartedAt, @EndedAt, @CreatedAt)",
                segment,
                transaction);
            var affected = conn.Execute(
                "UPDATE cards SET status_id = @StatusId, current_primary_run_id = @RunId, updated_at = @Now WHERE id = @CardId",
                new { StatusId = statusId, RunId = runId, Now = now, CardId = cardId },
                transaction);
            if (affected != 1)
                throw new System.InvalidOperationException($"card {cardId} was not updated");
            return System.Threading.Tasks.Task.FromResult(segment);
        });
    }

    public System.Threading.Tasks.Task<RunRow> CreateScheduledRunAndAdvanceAsync(
        CardRow card,
        string? nextFireAt,
        string lastFiredAt)
    {
        return _channel.ExecuteTransactionAsync((conn, transaction) =>
        {
            var now = System.DateTimeOffset.UtcNow.ToString("o");
            var runId = System.Guid.NewGuid().ToString("N");
            var run = new RunRow
            {
                Id = runId,
                CardId = card.Id,
                BayId = card.BayId,
                RunFamilyId = runId,
                State = "active",
                Backgrounded = true,
                LaunchProfileJson = card.LaunchConfigJson,
                StartedAt = now,
                CreatedAt = now,
                UpdatedAt = now,
            };
            conn.Execute(
                "INSERT INTO task_runs (id, card_id, bay_id, run_family_id, state, backgrounded, launch_profile_json, review_status_id, completion_status_id, pending_prompt, started_at, ended_at, created_at, updated_at) VALUES (@Id, @CardId, @BayId, @RunFamilyId, @State, @Backgrounded, @LaunchProfileJson, @ReviewStatusId, @CompletionStatusId, @PendingPrompt, @StartedAt, @EndedAt, @CreatedAt, @UpdatedAt)",
                run,
                transaction);
            var affected = conn.Execute(
                "UPDATE card_schedules SET next_fire_at = @NextFireAt, last_fired_at = @LastFiredAt, updated_at = @Now WHERE card_id = @CardId",
                new { NextFireAt = nextFireAt, LastFiredAt = lastFiredAt, Now = now, CardId = card.Id },
                transaction);
            if (affected != 1)
                throw new System.InvalidOperationException($"schedule for card {card.Id} was not updated");
            return System.Threading.Tasks.Task.FromResult(run);
        });
    }

    private static void SeedDefaultStatuses(
        Microsoft.Data.Sqlite.SqliteConnection conn,
        Microsoft.Data.Sqlite.SqliteTransaction transaction,
        string bayId,
        string now)
    {
        foreach (var (id, name, color, position, inProgress, looping, review, completion) in DefaultStatuses)
        {
            conn.Execute(
                "INSERT OR IGNORE INTO statuses (bay_id, id, name, hex_color, position, is_in_progress, is_looping, is_review, is_completion, created_at, updated_at) VALUES (@BayId, @Id, @Name, @Color, @Position, @InProgress, @Looping, @Review, @Completion, @Now, @Now)",
                new
                {
                    BayId = bayId,
                    Id = id,
                    Name = name,
                    Color = color,
                    Position = position,
                    InProgress = inProgress,
                    Looping = looping,
                    Review = review,
                    Completion = completion,
                    Now = now,
                },
                transaction);
        }
    }
}
