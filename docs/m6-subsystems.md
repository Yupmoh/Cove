# M6 Knowledge Layer — Subsystem Docs

## Persistence architecture

Multi-DB SQLite split (one writer per corpus, FTS rebuilds independently):

- `timeline.db` — append-only timeline entries with FTS5 mirror (`timeline_fts`, external-content + INSERT/DELETE/UPDATE triggers)
- `memory.db` — facts (`facts_fts` porter unicode61), episodes, supersede chains, blackboard (TTL), feedback, proposals
- `fts/index.db` — session corpus (`sessions_fts` word-level + `sessions_fts_trigram` substring), agent edits
- `notes/index.db` — note FTS cache (`notes_fts` porter unicode61, external-content + triggers)
- `attribution/index.db` — line-range authorship attribution
- `library/catalog.db` — saved/history pane entries with secret redaction
- `reviews/<commit-sha>/review.db` — one DB per reviewed commit (comments, audit_trail, attribution, session_telemetry)
- `vault/settings.json` — vault depth/horizon/extractor version

All SQLite: WAL mode, busy_timeout=5000. All FTS5 external-content tables use INSERT/DELETE/UPDATE triggers (manual DELETE corrupts the index). Startup self-check (`VerifyTokenizers`) fails loud if porter/trigram/unicode61 unavailable.

## Timeline (KN-30-43)

`TimelineStore` — append-only entries with backfill dedup (`ON CONFLICT(backfill_key) WHERE NOT NULL DO NOTHING`). Tag validation (regex `^[a-z][a-z0-9-]*:[a-zA-Z0-9_-]+$`), scope validation (workspace/room/pane/task/session). FTS5 search via MATCH + rank. `GitCommitIngester` appends git.commit entries. `TimelineSynthesizer` produces typed brief/recap/update entries with backing markdown notes (no app-side LLM).

GUI: `timeline-feed` pane (day-grouped feed, scope filter, search, kind icons).

## Notes (KN-01-29)

`NoteFileStore` — file-backed notes (`notes/<ws>/<id>/meta.json` + `note.<ext>` + `state.json` + `viewport.json`). FTS cache in `notes/index.db`. `RebuildIndexFromDisk` rescans all note dirs. `NoteSnapshotService` git-commits on create/update (history). Four types: markdown (inline comment anchors), sketch (SVG export, pan/zoom), canvas (43-component directive engine), html (sandboxed iframe + postMessage state). Mermaid as fifth type.

`CanvasPatchEngine` — RFC 6902 JSON Patch over JsonNode DOM with forgiving semantics (forward refs, partial-path create, test-mismatch skip). Buffered flush via Channel + 50ms timer.

## Memory (KN-44-55)

`MemoryStore` — facts with FTS5, supersede chains, reindex from disk (facts on disk are truth). `MemoryRanker` — fused score (bm25 0.6 + hotness 0.4), recall previews with how-long-ago, feedback logging. `ProposalStore` + `MemoryConsolidator` — cancellable task, dry-run mode, proposal lifecycle (proposed→applied→reverted).

BLOCKED: `SemanticEmbedder` (KN-49) is hash-BoW, not Model2Vec. Requires model asset or authorized package. Semantic recall feature-flagged OFF; lexical+hotness ships as working path.

## Blackboard (KN-56-58)

`BlackboardStore` — TTL'd posts in `memory.db`. Corrections cite original (refId), show alongside. Sweeps expired on read. No board UI (KN-58 SKIP).

## Vault / Library (KN-59-65, 66-73)

`SessionCorpusIndexer` — sessions FTS (word + trigram), reindex on version change. `EditsIndex` — agent_edits table (FK to sessions), find by file (exact + basename retry). Session picker GUI with search + vault settings (depth/horizon/excludes).

`LibraryStore` — `catalog.db` + entries. Secret redaction: keyword-based (password/token/api_key/bearer/authorization/credential), value-after-redacted-key, known secret formats (sk- OpenAI, AKIA AWS, ghp_ GitHub, xox Slack, eyJ JWT, BEGIN PRIVATE KEY). Library popover with fuzzy search + active-workspace boost.

`SnapshotService` — git-based snapshot vault. `TakeAsync` (content dict → git commit), `RestoreAsync` (PreRestore undo commit + git checkout), `InspectAsync` (diff snapshot vs current via `git ls-tree` + `git show`, no working-tree mutation). Three change types: changed/added/removed.

## Reviews (KN-86-95)

`ReviewStore` — per-commit `review.db`. Threaded comments (root_id/parent_id, cmt_ ULIDs, states open/resolved/orphaned/closed). Audit trail (from_state/to_state/actor/at/note appended on every transition). `session_telemetry` accrues per-commit.

`CommentAnchorEngine` — maps comment line anchors across diffs. DiffHunk model. Handles: insert-above (shift), delete-above (shift), delete-commented-line (orphan), rename (follow), context-hash match (keep), mismatch (orphan conservatively). Core invariant proven by fuzz tests (300 multi-hunk + 200 move + 100 rename iterations): never wrong-anchored — conservatively orphans on uncertainty.

`ReviewScopeResolver` — workspace scope returns all, session scope returns comments only if session has telemetry.

`AttributionIndex` — `attribution/index.db`. Record (session_id, tool_use_id, file_path, start_line, end_line). FindByLine (range overlap, most recent), FindByRange (overlapping), FindByToolUse. Changed line-range resolves to exact authoring tool call.

`ReviewDispatcher` — dispatches rendered review messages to target pane PTY. Links to task run. Diff-review pane with live-update (3s poll, scroll preservation, isConnected self-clear).

## Control plane integration

All knowledge services are headless-first daemon services on the `cove://` control plane. Handlers in `KnowledgeCommands` + `SnapshotCommands`. DTOs in `Cove.Protocol` (dependency direction: Engine references Protocol, not vice versa). All JSON via source-generated `JsonSerializerContext` (camelCase, AOT-safe). CLI verbs via `[CoveCommand]` source generator + `RouteCoreAsync`.

## Contract test

`HandlerOutputContractTests` (8 tests) — asserts CLI/GUI/TUI decode identical handler output: DTO camelCase round-trips, vault settings persistence, context hash determinism, secret redaction, review lifecycle audit, snapshot round-trip with undo.

## Limitations / blockers

- **M6-P22** (SemanticEmbedder): hash-BoW not Model2Vec. Needs model asset or authorized ONNX package.
- **PNG export** (P13): `note.read --png` returns not_supported. Needs SkiaSharp/System.Drawing/ImageSharp (unauthorized dep). SVG works.
- **Frontend per-component tests** (P14): vitest lacks jsdom. Canvas component tests in C# instead.
