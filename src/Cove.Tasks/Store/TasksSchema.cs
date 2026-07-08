namespace Cove.Tasks.Store;

internal static class TasksSchema
{
    public const int CurrentVersion = 1;

    public const string V1Ddl = """
CREATE TABLE IF NOT EXISTS migrations (
    version INTEGER PRIMARY KEY,
    applied_at TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS statuses (
    workspace_id TEXT NOT NULL,
    id TEXT NOT NULL,
    name TEXT NOT NULL,
    hex_color TEXT NOT NULL DEFAULT '808080',
    position REAL NOT NULL,
    hidden INTEGER NOT NULL DEFAULT 0,
    is_looping INTEGER NOT NULL DEFAULT 0,
    is_in_progress INTEGER NOT NULL DEFAULT 0,
    is_review INTEGER NOT NULL DEFAULT 0,
    is_completion INTEGER NOT NULL DEFAULT 0,
    created_at TEXT NOT NULL,
    updated_at TEXT NOT NULL,
    PRIMARY KEY (workspace_id, id),
    UNIQUE (workspace_id, name)
);

CREATE TABLE IF NOT EXISTS cards (
    id TEXT PRIMARY KEY,
    workspace_id TEXT NOT NULL,
    task_number INTEGER NOT NULL,
    title TEXT NOT NULL,
    description TEXT NOT NULL DEFAULT '',
    status_id TEXT NOT NULL DEFAULT 'todo',
    priority INTEGER NOT NULL DEFAULT 1,
    size INTEGER NOT NULL DEFAULT 2,
    assignee TEXT,
    source TEXT NOT NULL,
    order_key REAL NOT NULL DEFAULT 0,
    current_primary_run_id TEXT,
    launch_config_json TEXT,
    agent_ref TEXT,
    skill_selection_json TEXT,
    profile_slug TEXT,
    due_at TEXT,
    attachments_json TEXT,
    comment_ids_json TEXT NOT NULL DEFAULT '[]',
    created_at TEXT NOT NULL,
    updated_at TEXT NOT NULL,
    UNIQUE (workspace_id, task_number),
    FOREIGN KEY (workspace_id, status_id) REFERENCES statuses (workspace_id, id) ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS comments (
    id TEXT PRIMARY KEY,
    card_id TEXT NOT NULL,
    kind TEXT NOT NULL DEFAULT 'discussion',
    body TEXT NOT NULL DEFAULT '',
    source TEXT NOT NULL,
    created_at TEXT NOT NULL,
    FOREIGN KEY (card_id) REFERENCES cards (id) ON DELETE CASCADE,
    CHECK (kind IN ('discussion', 'instruction', 'agent_update', 'system_event'))
);

CREATE TABLE IF NOT EXISTS labels (
    id TEXT PRIMARY KEY,
    workspace_id TEXT NOT NULL,
    name TEXT NOT NULL,
    hex_color TEXT NOT NULL DEFAULT '6e6e6e',
    position REAL NOT NULL,
    created_at TEXT NOT NULL,
    UNIQUE (workspace_id, name)
);

CREATE TABLE IF NOT EXISTS card_labels (
    card_id TEXT NOT NULL,
    label_id TEXT NOT NULL,
    PRIMARY KEY (card_id, label_id),
    FOREIGN KEY (card_id) REFERENCES cards (id) ON DELETE CASCADE,
    FOREIGN KEY (label_id) REFERENCES labels (id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS task_counter (
    workspace_id TEXT PRIMARY KEY,
    next_number INTEGER NOT NULL DEFAULT 1
);

CREATE TABLE IF NOT EXISTS task_runs (
    id TEXT PRIMARY KEY,
    card_id TEXT NOT NULL,
    workspace_id TEXT NOT NULL,
    run_family_id TEXT NOT NULL,
    state TEXT NOT NULL DEFAULT 'active',
    backgrounded INTEGER NOT NULL DEFAULT 0,
    launch_profile_json TEXT,
    review_status_id TEXT,
    completion_status_id TEXT,
    started_at TEXT NOT NULL,
    ended_at TEXT,
    created_at TEXT NOT NULL,
    updated_at TEXT NOT NULL,
    FOREIGN KEY (card_id) REFERENCES cards (id) ON DELETE CASCADE,
    CHECK (state IN ('active', 'interrupted', 'completed', 'cancelled', 'resuming', 'succeeded', 'failed'))
);

CREATE TABLE IF NOT EXISTS task_run_segments (
    id TEXT PRIMARY KEY,
    run_id TEXT NOT NULL,
    pane_id TEXT,
    adapter_session_id TEXT,
    started_at TEXT NOT NULL,
    ended_at TEXT,
    created_at TEXT NOT NULL,
    FOREIGN KEY (run_id) REFERENCES task_runs (id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS card_schedules (
    card_id TEXT PRIMARY KEY,
    trigger_kind TEXT NOT NULL,
    cron TEXT,
    tz TEXT,
    at TEXT,
    completion_rule TEXT NOT NULL DEFAULT 'loop',
    mark_done_by TEXT NOT NULL DEFAULT 'agent',
    block_overlap INTEGER NOT NULL DEFAULT 1,
    home_status_id TEXT,
    paused INTEGER NOT NULL DEFAULT 0,
    skip_next INTEGER NOT NULL DEFAULT 0,
    next_fire_at TEXT,
    last_fired_at TEXT,
    created_at TEXT NOT NULL,
    updated_at TEXT NOT NULL,
    FOREIGN KEY (card_id) REFERENCES cards (id) ON DELETE CASCADE,
    CHECK (trigger_kind IN ('immediate', 'datetime', 'cron')),
    CHECK (completion_rule IN ('terminal', 'loop', 'respawn')),
    CHECK (mark_done_by IN ('agent', 'review'))
);

CREATE INDEX IF NOT EXISTS idx_cards_workspace_status ON cards (workspace_id, status_id);
CREATE INDEX IF NOT EXISTS idx_cards_workspace_number ON cards (workspace_id, task_number);
CREATE INDEX IF NOT EXISTS idx_cards_status_order ON cards (status_id, order_key);
CREATE INDEX IF NOT EXISTS idx_comments_card ON comments (card_id, created_at);
CREATE INDEX IF NOT EXISTS idx_labels_workspace ON labels (workspace_id, position);
CREATE INDEX IF NOT EXISTS idx_runs_card ON task_runs (card_id);
CREATE INDEX IF NOT EXISTS idx_runs_family ON task_runs (run_family_id);
CREATE INDEX IF NOT EXISTS idx_runs_state ON task_runs (state);
CREATE INDEX IF NOT EXISTS idx_segments_run ON task_run_segments (run_id, started_at);
CREATE INDEX IF NOT EXISTS idx_schedules_next_fire ON card_schedules (next_fire_at) WHERE paused = 0 AND next_fire_at IS NOT NULL;
""";

    public static readonly Cove.Persistence.SqliteMigration[] Migrations =
    [
        new Cove.Persistence.SqliteMigration { Version = 1, Sql = V1Ddl },
    ];
}
