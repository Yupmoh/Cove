namespace Cove.Tasks.Store;

internal sealed record ExpectedColumn(string Name, string DeclaredType);

internal static class ExpectedColumns
{
    public static readonly System.Collections.Generic.IReadOnlyDictionary<string, System.Collections.Generic.IReadOnlyList<ExpectedColumn>> All = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.IReadOnlyList<ExpectedColumn>>(System.StringComparer.Ordinal)
    {
        ["migrations"] = [new ExpectedColumn("version", "INTEGER"), new ExpectedColumn("applied_at", "TEXT")],
        ["statuses"] =
        [
            new ExpectedColumn("id", "TEXT"), new ExpectedColumn("workspace_id", "TEXT"),
            new ExpectedColumn("name", "TEXT"), new ExpectedColumn("hex_color", "TEXT"),
            new ExpectedColumn("position", "REAL"), new ExpectedColumn("hidden", "INTEGER"),
            new ExpectedColumn("is_looping", "INTEGER"), new ExpectedColumn("is_in_progress", "INTEGER"),
            new ExpectedColumn("is_review", "INTEGER"), new ExpectedColumn("is_completion", "INTEGER"),
            new ExpectedColumn("created_at", "TEXT"), new ExpectedColumn("updated_at", "TEXT"),
        ],
        ["cards"] =
        [
            new ExpectedColumn("id", "TEXT"), new ExpectedColumn("workspace_id", "TEXT"),
            new ExpectedColumn("task_number", "INTEGER"), new ExpectedColumn("title", "TEXT"),
            new ExpectedColumn("description", "TEXT"), new ExpectedColumn("status_id", "TEXT"),
            new ExpectedColumn("priority", "INTEGER"), new ExpectedColumn("size", "INTEGER"),
            new ExpectedColumn("assignee", "TEXT"), new ExpectedColumn("source", "TEXT"),
            new ExpectedColumn("order_key", "REAL"), new ExpectedColumn("current_primary_run_id", "TEXT"),
            new ExpectedColumn("launch_config_json", "TEXT"), new ExpectedColumn("agent_ref", "TEXT"),
            new ExpectedColumn("skill_selection_json", "TEXT"), new ExpectedColumn("profile_slug", "TEXT"),
            new ExpectedColumn("due_at", "TEXT"), new ExpectedColumn("attachments_json", "TEXT"),
            new ExpectedColumn("comment_ids_json", "TEXT"), new ExpectedColumn("created_at", "TEXT"),
            new ExpectedColumn("updated_at", "TEXT"),
        ],
        ["comments"] =
        [
            new ExpectedColumn("id", "TEXT"), new ExpectedColumn("card_id", "TEXT"),
            new ExpectedColumn("kind", "TEXT"), new ExpectedColumn("body", "TEXT"),
            new ExpectedColumn("source", "TEXT"), new ExpectedColumn("created_at", "TEXT"),
        ],
        ["labels"] =
        [
            new ExpectedColumn("id", "TEXT"), new ExpectedColumn("workspace_id", "TEXT"),
            new ExpectedColumn("name", "TEXT"), new ExpectedColumn("hex_color", "TEXT"),
            new ExpectedColumn("position", "REAL"), new ExpectedColumn("created_at", "TEXT"),
        ],
        ["card_labels"] =
        [
            new ExpectedColumn("card_id", "TEXT"), new ExpectedColumn("label_id", "TEXT"),
        ],
        ["task_counter"] =
        [
            new ExpectedColumn("workspace_id", "TEXT"), new ExpectedColumn("next_number", "INTEGER"),
        ],
        ["task_runs"] =
        [
            new ExpectedColumn("id", "TEXT"), new ExpectedColumn("card_id", "TEXT"),
            new ExpectedColumn("workspace_id", "TEXT"), new ExpectedColumn("run_family_id", "TEXT"),
            new ExpectedColumn("state", "TEXT"), new ExpectedColumn("backgrounded", "INTEGER"),
            new ExpectedColumn("launch_profile_json", "TEXT"), new ExpectedColumn("review_status_id", "TEXT"),
            new ExpectedColumn("completion_status_id", "TEXT"), new ExpectedColumn("pending_prompt", "TEXT"), new ExpectedColumn("started_at", "TEXT"),
            new ExpectedColumn("ended_at", "TEXT"), new ExpectedColumn("created_at", "TEXT"),
            new ExpectedColumn("updated_at", "TEXT"),
        ],
        ["task_run_segments"] =
        [
            new ExpectedColumn("id", "TEXT"), new ExpectedColumn("run_id", "TEXT"),
            new ExpectedColumn("pane_id", "TEXT"), new ExpectedColumn("adapter_session_id", "TEXT"),
            new ExpectedColumn("started_at", "TEXT"), new ExpectedColumn("ended_at", "TEXT"),
            new ExpectedColumn("created_at", "TEXT"),
        ],
        ["card_schedules"] =
        [
            new ExpectedColumn("card_id", "TEXT"), new ExpectedColumn("trigger_kind", "TEXT"),
            new ExpectedColumn("cron", "TEXT"), new ExpectedColumn("tz", "TEXT"),
            new ExpectedColumn("at", "TEXT"), new ExpectedColumn("completion_rule", "TEXT"),
            new ExpectedColumn("mark_done_by", "TEXT"), new ExpectedColumn("block_overlap", "INTEGER"),
            new ExpectedColumn("home_status_id", "TEXT"), new ExpectedColumn("paused", "INTEGER"),
            new ExpectedColumn("skip_next", "INTEGER"), new ExpectedColumn("next_fire_at", "TEXT"),
            new ExpectedColumn("last_fired_at", "TEXT"), new ExpectedColumn("created_at", "TEXT"),
            new ExpectedColumn("updated_at", "TEXT"),
        ],
    };
}
