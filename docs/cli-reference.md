# Cove CLI Reference

Generated from the command registry. Do not edit by hand.

| Command | Source | Description |
|---------|--------|-------------|
| `--version` | cli |  |
| `-V` | cli |  |
| `activity acknowledge` | cli |  |
| `activity list` | cli |  |
| `adapter-env list` | cli |  |
| `adapter-env resolve` | cli |  |
| `adapter-env save` | cli |  |
| `agent definition delete` | cli |  |
| `agent definition list` | cli |  |
| `agent definition show` | cli |  |
| `attach` | cli |  |
| `attribution find-by-line` | cli |  |
| `attribution find-by-range` | cli |  |
| `attribution find-by-tool-use` | cli |  |
| `attribution record` | cli |  |
| `blackboard post` | cli |  |
| `blackboard show` | cli |  |
| `browser back` | cli |  |
| `browser close` | cli |  |
| `browser forward` | cli |  |
| `browser navigate` | cli |  |
| `browser open` | cli |  |
| `browser reload` | cli |  |
| `canvas action` | cli |  |
| `capture attach` | cli |  |
| `capture delete` | cli |  |
| `capture flag` | cli |  |
| `capture list` | cli |  |
| `capture show` | cli |  |
| `capture start` | cli |  |
| `capture stop` | cli |  |
| `commands` | cli |  |
| `config get` | cli |  |
| `config set` | cli |  |
| `context` | cli |  |
| `cove://commands/activity.acknowledge` | core |  |
| `cove://commands/activity.list` | core |  |
| `cove://commands/activity.needs-input` | core |  |
| `cove://commands/adapter-env.list` | core |  |
| `cove://commands/adapter-env.resolve` | core |  |
| `cove://commands/adapter-env.save` | core |  |
| `cove://commands/adapter.install-local` | core |  |
| `cove://commands/adapter.list` | core |  |
| `cove://commands/adapter.remove` | core |  |
| `cove://commands/adapter.rescan` | core |  |
| `cove://commands/adapter.retention-get` | core |  |
| `cove://commands/adapter.retention-set` | core |  |
| `cove://commands/adapter.tools-list` | core |  |
| `cove://commands/adapter.updates-check` | core |  |
| `cove://commands/agent.close` | core |  |
| `cove://commands/agent.definition.delete` | core |  |
| `cove://commands/agent.definition.list` | core |  |
| `cove://commands/agent.definition.show` | core |  |
| `cove://commands/agent.list` | core |  |
| `cove://commands/agent.message` | core |  |
| `cove://commands/agent.replay` | core |  |
| `cove://commands/agent.spawned-nooks` | core |  |
| `cove://commands/agent.stop` | core |  |
| `cove://commands/attach.raw` | core | raw external-terminal attach (TM-79, runtime-owned) |
| `cove://commands/attribution.find-by-line` | core |  |
| `cove://commands/attribution.find-by-range` | core |  |
| `cove://commands/attribution.find-by-tool-use` | core |  |
| `cove://commands/attribution.record` | core |  |
| `cove://commands/bay-command.clear` | core |  |
| `cove://commands/bay-command.create` | core |  |
| `cove://commands/bay-command.delete` | core |  |
| `cove://commands/bay-command.edit` | core |  |
| `cove://commands/bay-command.list` | core |  |
| `cove://commands/bay-command.logs` | core |  |
| `cove://commands/bay-command.restart` | core |  |
| `cove://commands/bay-command.start` | core |  |
| `cove://commands/bay-command.status` | core |  |
| `cove://commands/bay-command.stop` | core |  |
| `cove://commands/bay.create` | core |  |
| `cove://commands/bay.delete` | core |  |
| `cove://commands/bay.hide` | core |  |
| `cove://commands/bay.list` | core |  |
| `cove://commands/bay.move-shore` | core |  |
| `cove://commands/bay.rename` | core |  |
| `cove://commands/bay.reorder` | core |  |
| `cove://commands/bay.set-accent` | core |  |
| `cove://commands/bay.set-icon` | core |  |
| `cove://commands/bay.switch` | core |  |
| `cove://commands/blackboard.post` | core |  |
| `cove://commands/blackboard.show` | core |  |
| `cove://commands/browser.automation.result` | core |  |
| `cove://commands/browser.back` | core |  |
| `cove://commands/browser.clear` | core | clear a ref input/textarea/contenteditable and dispatch input+change |
| `cove://commands/browser.click` | core |  |
| `cove://commands/browser.close` | core |  |
| `cove://commands/browser.create` | core |  |
| `cove://commands/browser.eval` | core |  |
| `cove://commands/browser.fill` | core |  |
| `cove://commands/browser.forward` | core |  |
| `cove://commands/browser.get` | core | read a whitelisted property (text,value,href,title,checked,disabled,visible) from a ref |
| `cove://commands/browser.is` | core | test a whitelisted state (visible,enabled,checked,editable) of a ref |
| `cove://commands/browser.navigate` | core |  |
| `cove://commands/browser.open` | core |  |
| `cove://commands/browser.press` | core | dispatch synthetic keydown/keypress/keyup for a named key (isTrusted=false, cannot fire browser default actions) |
| `cove://commands/browser.reload` | core |  |
| `cove://commands/browser.screenshot` | core |  |
| `cove://commands/browser.scroll` | core | scroll a ref element (or the window when ref is omitted) to absolute coordinates x,y — a missing axis defaults to 0 |
| `cove://commands/browser.select` | core | select an option in a <select> ref by its value |
| `cove://commands/browser.setUserAgent` | core |  |
| `cove://commands/browser.snapshot` | core |  |
| `cove://commands/browser.type` | core | append text char-by-char into a ref with synthetic input events |
| `cove://commands/browser.wait` | core | poll the page until a ref exists or text becomes visible, up to timeoutMs (default 2000, capped 8000ms under the bridge timeout) |
| `cove://commands/canvas.action` | core |  |
| `cove://commands/capture.attach` | core |  |
| `cove://commands/capture.delete` | core |  |
| `cove://commands/capture.flag` | core |  |
| `cove://commands/capture.list` | core |  |
| `cove://commands/capture.show` | core |  |
| `cove://commands/capture.start` | core |  |
| `cove://commands/capture.stop` | core |  |
| `cove://commands/capture_native_screenshot` | core | mcp-bridge capture_native_screenshot (PL-30, render-bound) |
| `cove://commands/collection.create` | core |  |
| `cove://commands/collection.list` | core |  |
| `cove://commands/collection.move-bay` | core |  |
| `cove://commands/collection.remove` | core |  |
| `cove://commands/collection.rename` | core |  |
| `cove://commands/collection.switch` | core |  |
| `cove://commands/config.get` | core |  |
| `cove://commands/config.schema` | core |  |
| `cove://commands/config.set` | core |  |
| `cove://commands/diagnostics.export` | core |  |
| `cove://commands/diagnostics.snapshot.list` | core |  |
| `cove://commands/diagnostics.snapshot.take` | core |  |
| `cove://commands/diagnostics.status` | core |  |
| `cove://commands/editor.get-state` | core |  |
| `cove://commands/editor.open` | core |  |
| `cove://commands/editor.save` | core |  |
| `cove://commands/editor.set-state` | core |  |
| `cove://commands/edits.find` | core |  |
| `cove://commands/emit_event` | core | mcp-bridge emit_event (PL-30, headless-safe) |
| `cove://commands/execute_command` | core | mcp-bridge execute_command (PL-30, headless-safe) |
| `cove://commands/execute_js` | core | mcp-bridge execute_js (PL-30, render-bound) |
| `cove://commands/extension.list` | core |  |
| `cove://commands/extension.run` | core |  |
| `cove://commands/get_backend_state` | core | mcp-bridge get_backend_state (PL-30, headless-safe) |
| `cove://commands/get_ipc_events` | core | mcp-bridge get_ipc_events (PL-30, headless-safe) |
| `cove://commands/get_window_info` | core | mcp-bridge get_window_info (PL-30, render-bound) |
| `cove://commands/keybind.clear` | core |  |
| `cove://commands/keybind.conflicts` | core |  |
| `cove://commands/keybind.is-reserved` | core |  |
| `cove://commands/keybind.list` | core |  |
| `cove://commands/keybind.set` | core |  |
| `cove://commands/knowledge.ping` | core |  |
| `cove://commands/launch-profile.create` | core |  |
| `cove://commands/launch-profile.delete` | core |  |
| `cove://commands/launch-profile.get` | core |  |
| `cove://commands/launch-profile.list` | core |  |
| `cove://commands/launch-profile.set-default` | core |  |
| `cove://commands/launch-profile.update` | core |  |
| `cove://commands/launch.build` | core |  |
| `cove://commands/launch.overrides.clear` | core |  |
| `cove://commands/launch.overrides.get` | core |  |
| `cove://commands/launch.overrides.save` | core |  |
| `cove://commands/launcher.options` | core |  |
| `cove://commands/layout.get` | core |  |
| `cove://commands/layout.mutate` | core |  |
| `cove://commands/layout.snapshot` | core |  |
| `cove://commands/library.list` | core |  |
| `cove://commands/library.materialize` | core |  |
| `cove://commands/list_windows` | core | mcp-bridge list_windows (PL-30, render-bound) |
| `cove://commands/lsp.definition` | core |  |
| `cove://commands/lsp.diagnostics` | core |  |
| `cove://commands/lsp.hover` | core |  |
| `cove://commands/lsp.status` | core |  |
| `cove://commands/memory.add` | core |  |
| `cove://commands/memory.consolidate` | core |  |
| `cove://commands/memory.proposal.transition` | core |  |
| `cove://commands/memory.propose` | core |  |
| `cove://commands/memory.recall` | core |  |
| `cove://commands/memory.reindex` | core |  |
| `cove://commands/memory.search` | core |  |
| `cove://commands/memory.show` | core |  |
| `cove://commands/memory.supersede` | core |  |
| `cove://commands/nook-types.list` | core |  |
| `cove://commands/nook.checkpoint` | core |  |
| `cove://commands/nook.kill` | core |  |
| `cove://commands/nook.list` | core |  |
| `cove://commands/nook.read` | core |  |
| `cove://commands/nook.rename` | core |  |
| `cove://commands/nook.resize` | core |  |
| `cove://commands/nook.scope.get` | core |  |
| `cove://commands/nook.scope.set` | core |  |
| `cove://commands/nook.search` | core |  |
| `cove://commands/nook.spawn` | core |  |
| `cove://commands/nook.write` | core |  |
| `cove://commands/note.create` | core |  |
| `cove://commands/note.delete` | core |  |
| `cove://commands/note.get` | core |  |
| `cove://commands/note.get-state` | core |  |
| `cove://commands/note.history` | core |  |
| `cove://commands/note.list` | core |  |
| `cove://commands/note.media.save` | core |  |
| `cove://commands/note.read` | core |  |
| `cove://commands/note.save-state` | core |  |
| `cove://commands/note.search` | core |  |
| `cove://commands/note.update` | core |  |
| `cove://commands/note.write` | core |  |
| `cove://commands/omni-chat.append` | core |  |
| `cove://commands/omni-chat.clear` | core |  |
| `cove://commands/omni-chat.history` | core |  |
| `cove://commands/perf.bundle.create` | core |  |
| `cove://commands/perf.bundle.delete` | core |  |
| `cove://commands/perf.bundle.list` | core |  |
| `cove://commands/protocol.resolve` | core |  |
| `cove://commands/registry.fetch` | core |  |
| `cove://commands/resident.dock` | core |  |
| `cove://commands/resident.list` | core |  |
| `cove://commands/resident.set-collapsed` | core |  |
| `cove://commands/resident.set-height` | core |  |
| `cove://commands/resident.undock` | core |  |
| `cove://commands/restore.chooser` | core |  |
| `cove://commands/restore.confirm` | core |  |
| `cove://commands/restore.undo` | core |  |
| `cove://commands/review.add-comment` | core |  |
| `cove://commands/review.audit` | core |  |
| `cove://commands/review.close` | core |  |
| `cove://commands/review.dispatch` | core |  |
| `cove://commands/review.list-comments` | core |  |
| `cove://commands/review.re-anchor` | core |  |
| `cove://commands/review.reopen` | core |  |
| `cove://commands/review.resolve` | core |  |
| `cove://commands/review.telemetry` | core |  |
| `cove://commands/run.cancel` | core |  |
| `cove://commands/run.complete` | core |  |
| `cove://commands/run.list` | core |  |
| `cove://commands/run.resume` | core |  |
| `cove://commands/run.segments` | core |  |
| `cove://commands/run.set-pending-prompt` | core |  |
| `cove://commands/run.show` | core |  |
| `cove://commands/scm.blame` | core |  |
| `cove://commands/scm.commit` | core |  |
| `cove://commands/scm.diff` | core |  |
| `cove://commands/scm.log` | core |  |
| `cove://commands/scm.stage` | core |  |
| `cove://commands/scm.status` | core |  |
| `cove://commands/search.get-state` | core |  |
| `cove://commands/search.query` | core |  |
| `cove://commands/search.replace` | core |  |
| `cove://commands/search.set-state` | core |  |
| `cove://commands/session.background` | core |  |
| `cove://commands/session.dismiss` | core |  |
| `cove://commands/session.foreground` | core |  |
| `cove://commands/session.list` | core |  |
| `cove://commands/session.recent` | core |  |
| `cove://commands/session.state` | core |  |
| `cove://commands/session.stop` | core |  |
| `cove://commands/shore.close` | core |  |
| `cove://commands/shore.create` | core |  |
| `cove://commands/shore.list` | core |  |
| `cove://commands/shore.move-to-wing` | core |  |
| `cove://commands/shore.pin` | core |  |
| `cove://commands/shore.rename` | core |  |
| `cove://commands/shore.switch` | core |  |
| `cove://commands/skills.index` | core |  |
| `cove://commands/skills.resolve-manifest` | core |  |
| `cove://commands/skills.resolve-prompt-sigils` | core |  |
| `cove://commands/snapshot.inspect` | core |  |
| `cove://commands/snapshot.list` | core |  |
| `cove://commands/snapshot.pin` | core |  |
| `cove://commands/snapshot.prune` | core |  |
| `cove://commands/snapshot.restore` | core |  |
| `cove://commands/snapshot.take` | core |  |
| `cove://commands/start_ipc_monitor` | core | mcp-bridge start_ipc_monitor (PL-30, headless-safe) |
| `cove://commands/state.read` | core |  |
| `cove://commands/state.write` | core |  |
| `cove://commands/stop_ipc_monitor` | core | mcp-bridge stop_ipc_monitor (PL-30, headless-safe) |
| `cove://commands/task-board.diff` | core |  |
| `cove://commands/task-board.export` | core |  |
| `cove://commands/task.binding.get` | core |  |
| `cove://commands/task.binding.resolve-profile` | core |  |
| `cove://commands/task.binding.set` | core |  |
| `cove://commands/task.claim` | core |  |
| `cove://commands/task.comment.add` | core |  |
| `cove://commands/task.comment.count` | core |  |
| `cove://commands/task.comment.delete` | core |  |
| `cove://commands/task.comment.list` | core |  |
| `cove://commands/task.create` | core |  |
| `cove://commands/task.delete` | core |  |
| `cove://commands/task.get` | core |  |
| `cove://commands/task.label.assign` | core |  |
| `cove://commands/task.label.create` | core |  |
| `cove://commands/task.label.delete` | core |  |
| `cove://commands/task.label.filter` | core |  |
| `cove://commands/task.label.list` | core |  |
| `cove://commands/task.label.reorder` | core |  |
| `cove://commands/task.label.unassign` | core |  |
| `cove://commands/task.launch` | core |  |
| `cove://commands/task.launch-config.get` | core |  |
| `cove://commands/task.launch-config.set` | core |  |
| `cove://commands/task.list` | core |  |
| `cove://commands/task.ping` | core | echo back params as pong (smoke) |
| `cove://commands/task.repeat.continue` | core |  |
| `cove://commands/task.repeat.finish` | core |  |
| `cove://commands/task.repeat.get` | core |  |
| `cove://commands/task.repeat.pause` | core |  |
| `cove://commands/task.repeat.resume` | core |  |
| `cove://commands/task.repeat.set` | core |  |
| `cove://commands/task.repeat.skip-next` | core |  |
| `cove://commands/task.repeat.stop` | core |  |
| `cove://commands/task.run-now` | core |  |
| `cove://commands/task.set-done` | core |  |
| `cove://commands/task.set-in-review` | core |  |
| `cove://commands/task.status.create` | core |  |
| `cove://commands/task.status.delete` | core |  |
| `cove://commands/task.status.list` | core |  |
| `cove://commands/task.status.reorder` | core |  |
| `cove://commands/task.status.set-hidden` | core |  |
| `cove://commands/task.update` | core |  |
| `cove://commands/theme.delete-custom` | core |  |
| `cove://commands/theme.get-active` | core |  |
| `cove://commands/theme.is-builtin` | core |  |
| `cove://commands/theme.list` | core |  |
| `cove://commands/theme.save-custom` | core |  |
| `cove://commands/theme.set-active` | core |  |
| `cove://commands/timeline.append` | core |  |
| `cove://commands/timeline.list` | core |  |
| `cove://commands/vault.reindex` | core |  |
| `cove://commands/vault.resume` | core |  |
| `cove://commands/vault.search` | core |  |
| `cove://commands/vault.set-setting` | core |  |
| `cove://commands/viewer.get-state` | core |  |
| `cove://commands/viewer.open` | core |  |
| `cove://commands/wing.create` | core |  |
| `cove://commands/wing.list` | core |  |
| `cove://commands/wing.remove` | core |  |
| `cove://commands/wing.rename` | core |  |
| `cove://commands/wing.reorder` | core |  |
| `cove://commands/wing.set-icon` | core |  |
| `cove://commands/wing.switch` | core |  |
| `cove://commands/worktree.adopt` | core |  |
| `cove://commands/worktree.create` | core |  |
| `cove://commands/worktree.list` | core |  |
| `cove://commands/worktree.orphans` | core |  |
| `cove://commands/worktree.prune` | core |  |
| `cove://commands/worktree.remove` | core |  |
| `cove://hooks/_state` | core |  |
| `cove://hooks/nook-states` | core |  |
| `diagnostics export` | cli |  |
| `diagnostics snapshot` | cli |  |
| `diagnostics snapshots` | cli |  |
| `diagnostics status` | cli |  |
| `docs generate` | cli |  |
| `editor get-state` | cli |  |
| `editor open` | cli |  |
| `editor save` | cli |  |
| `editor set-state` | cli |  |
| `edits find` | cli |  |
| `exec` | cli |  |
| `extension list` | cli |  |
| `extension run` | cli |  |
| `hook emit` | cli |  |
| `knowledge ping` | cli |  |
| `launch-profile create` | cli |  |
| `launch-profile delete` | cli |  |
| `launch-profile get` | cli |  |
| `launch-profile list` | cli |  |
| `launch-profile set-default` | cli |  |
| `launch-profile update` | cli |  |
| `library list` | cli |  |
| `library materialize` | cli |  |
| `memory add` | cli |  |
| `memory consolidate` | cli |  |
| `memory proposal transition` | cli |  |
| `memory propose` | cli |  |
| `memory recall` | cli |  |
| `memory reindex` | cli |  |
| `memory search` | cli |  |
| `memory show` | cli |  |
| `memory supersede` | cli |  |
| `migrate` | cli |  |
| `nook list` | cli |  |
| `nook-types list` | cli |  |
| `note create` | cli |  |
| `note delete` | cli |  |
| `note get` | cli |  |
| `note get-state` | cli |  |
| `note history` | cli |  |
| `note list` | cli |  |
| `note media save` | cli |  |
| `note read` | cli |  |
| `note save-state` | cli |  |
| `note search` | cli |  |
| `note update` | cli |  |
| `note write` | cli |  |
| `perf bundle create` | cli |  |
| `perf bundle delete` | cli |  |
| `perf bundle list` | cli |  |
| `protocol resolve` | cli |  |
| `review add-comment` | cli |  |
| `review audit` | cli |  |
| `review close` | cli |  |
| `review dispatch` | cli |  |
| `review list-comments` | cli |  |
| `review re-anchor` | cli |  |
| `review reopen` | cli |  |
| `review resolve` | cli |  |
| `review telemetry` | cli |  |
| `run cancel` | cli |  |
| `run complete` | cli |  |
| `run list` | cli |  |
| `run resume` | cli |  |
| `run segments` | cli |  |
| `run set-pending-prompt` | cli |  |
| `run show` | cli |  |
| `scm blame` | cli |  |
| `scm commit` | cli |  |
| `scm diff` | cli |  |
| `scm stage` | cli |  |
| `scm status` | cli |  |
| `search get-state` | cli |  |
| `search query` | cli |  |
| `search set-state` | cli |  |
| `session recent` | cli |  |
| `skills list` | cli |  |
| `skills resolve-prompt-sigils` | cli |  |
| `task binding get` | cli |  |
| `task binding resolve-profile` | cli |  |
| `task binding set` | cli |  |
| `task claim` | cli |  |
| `task comment add` | cli |  |
| `task comment count` | cli |  |
| `task comment delete` | cli |  |
| `task comment list` | cli |  |
| `task create` | cli |  |
| `task delete` | cli |  |
| `task get` | cli |  |
| `task label assign` | cli |  |
| `task label create` | cli |  |
| `task label delete` | cli |  |
| `task label filter` | cli |  |
| `task label list` | cli |  |
| `task label reorder` | cli |  |
| `task label unassign` | cli |  |
| `task launch` | cli |  |
| `task launch-config get` | cli |  |
| `task launch-config set` | cli |  |
| `task list` | cli |  |
| `task repeat continue` | cli |  |
| `task repeat finish` | cli |  |
| `task repeat get` | cli |  |
| `task repeat pause` | cli |  |
| `task repeat resume` | cli |  |
| `task repeat set` | cli |  |
| `task repeat skip-next` | cli |  |
| `task repeat stop` | cli |  |
| `task run-now` | cli |  |
| `task set-done` | cli |  |
| `task set-in-review` | cli |  |
| `task status create` | cli |  |
| `task status delete` | cli |  |
| `task status hide` | cli |  |
| `task status list` | cli |  |
| `task status reorder` | cli |  |
| `task update` | cli |  |
| `task-board diff` | cli |  |
| `task-board export` | cli |  |
| `timeline append` | cli |  |
| `timeline list` | cli |  |
| `vault reindex` | cli |  |
| `vault resume` | cli |  |
| `vault search` | cli |  |
| `vault set-setting` | cli |  |
| `version` | cli |  |
| `viewer get-state` | cli |  |
| `viewer open` | cli |  |

Total: 470 commands
