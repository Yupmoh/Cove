---
name: cove
description: Inspect and control the current Cove workspace, launch or resume agents, restart nooks, send messages, wait for agent state, and close nooks through the Cove daemon.
---

# Cove control

Use this skill when the user asks to inspect or change the Cove workspace or coordinate another agent.

## Command discovery

Use `"$COVE_CLI_PATH"` as the executable. Cove injects `COVE_CLI_PATH` and the current identity in `COVE_NOOK_ID` into every managed agent session. If either variable is empty, report that the session is not Cove-managed instead of guessing a path or nook ID.

Add `--json` when reading structured results. Preserve returned nook, bay, shore, session, placement, and scope values exactly.

## Inspect

Inspect the current placement and authorization scope:

```sh
"$COVE_CLI_PATH" workspace context --nook-id "$COVE_NOOK_ID" --json
```

Discover agents visible to this nook:

```sh
"$COVE_CLI_PATH" agent list --scope same-tab --json
"$COVE_CLI_PATH" agent list --scope same-shore --json
"$COVE_CLI_PATH" agent list --scope same-bay --json
```

Request only the narrowest scope needed. Use a returned nook ID for later operations.

## Launch

Launch an agent relative to the current nook:

```sh
"$COVE_CLI_PATH" agent launch <adapter> --relative-to "$COVE_NOOK_ID" --placement right --cwd <directory> --name <name> --json
```

Valid placements are `left`, `right`, `above`, `below`, and `new-shore`. Add `--profile <slug>`, `--yolo`, `--cols <count>`, `--rows <count>`, or `--access-scope same-tab|same-shore|same-bay|all` only when the user requests them or the task requires them.

## Resume

List recent sessions, then resume the exact adapter and session ID:

```sh
"$COVE_CLI_PATH" session recent --json
```

```sh
"$COVE_CLI_PATH" agent resume <adapter> <session-id> --relative-to "$COVE_NOOK_ID" --placement right --cwd <directory> --name <name> --json
```

Do not substitute a different adapter or session. A failed resume leaves the original workspace unchanged.

## Restart

Restart a nook in place under the same nook ID:

```sh
"$COVE_CLI_PATH" nook restart <nook-id> --mode fresh --json
"$COVE_CLI_PATH" nook restart <nook-id> --mode resume-current --resume-fallback fresh --json
```

Use `fresh` for a new process. Use `resume-current` only when the current adapter session should continue. Scrollback is preserved by default; add `--no-preserve-scrollback` only when requested.

## Message

Send a framed message to another visible agent:

```sh
"$COVE_CLI_PATH" agent message <nook-id> "<message>" --json
```

Keep framing enabled unless raw terminal input is explicitly required. Use `--no-frame` only for that exceptional case.

## Wait

Poll the narrowest useful agent list and inspect the target nook's `status`:

```sh
"$COVE_CLI_PATH" agent list --scope same-shore --json
```

Wait until the target reaches the state required by the task. `working` means it is active, `needs-input` means a human or agent response is required, `idle` means it can receive work, and `stopped` means its process ended. Re-run the command after a short delay rather than sending terminal probes.

## Close

Close a target nook through the daemon command:

```sh
"$COVE_CLI_PATH" exec nook.kill --params '{"nookId":"<nook-id>"}' --json
```

Never close the current nook unless the user explicitly asks. Re-list agents first when the target came from stale context.

## Authorization failures

`access_denied` means the current nook principal lacks the required scope. Do not retry with broader scope, raw layout operations, another claimed identity, or a forged adapter name. Explain the denied boundary and ask the user or a control client to perform the operation.

`not_found` means the target is stale or outside the visible scope; inspect context and list agents again. `invalid_state` means the requested lifecycle transition is not valid for the target's current state. `not_ready` means the required daemon subsystem is unavailable. Report the exact error code and message.

All mutations reconcile through daemon events. Do not issue a second layout snapshot request after a successful command, and do not instruct the user to manually reload the workspace.
