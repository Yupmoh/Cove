---
name: cove
description: Inspect and control the current Cove workspace, open terminal and browser nooks, launch or resume agents, balance stacks, send messages, wait for agent state, restart nooks, and close every nook type through the Cove daemon.
---

# Cove control

Use this skill when the user asks to inspect or change the Cove workspace or coordinate another agent.

## Command discovery

Use `"$COVE_CLI_PATH"` as the executable. Cove injects `COVE_CLI_PATH` and the current identity in `COVE_NOOK_ID` into every managed agent session. If either variable is empty, report that the session is not Cove-managed instead of guessing a path or nook ID.

Add `--json` when reading structured results. Preserve every returned nook, bay, shore, session, placement, and scope value exactly. Use returned nook IDs rather than names for later operations.

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

Request only the narrowest scope needed.

## Open a terminal

Open the platform default shell:

```sh
"$COVE_CLI_PATH" nook open terminal --relative-to "$COVE_NOOK_ID" --placement right --cwd <directory> --json
```

Open a specific executable:

```sh
"$COVE_CLI_PATH" nook open terminal --command <executable> --relative-to "$COVE_NOOK_ID" --placement right --cwd <directory> --json
```

Pass ordered arguments by repeating `--arg`:

```sh
"$COVE_CLI_PATH" nook open terminal --command <executable> --arg <argument-1> --arg <argument-2> --relative-to "$COVE_NOOK_ID" --placement right --cwd <directory> --json
```

Omit `--command` only when the platform default shell is intended. Valid placements are `left`, `right`, `above`, `below`, and `new-shore`.

## Open a browser

Open DuckDuckGo:

```sh
"$COVE_CLI_PATH" nook open browser --relative-to "$COVE_NOOK_ID" --placement right --json
```

Open an explicit URL:

```sh
"$COVE_CLI_PATH" nook open browser --url <url> --relative-to "$COVE_NOOK_ID" --placement right --json
```

Browser creation and placement are one operation. Do not create browser state separately or place a fabricated browser leaf.

## Launch an agent

Launch an agent relative to the current nook:

```sh
"$COVE_CLI_PATH" agent launch <adapter> --relative-to "$COVE_NOOK_ID" --placement right --cwd <directory> --name <name> --json
```

Select durable configuration with a profile:

```sh
"$COVE_CLI_PATH" agent launch <adapter> --profile <slug> --relative-to "$COVE_NOOK_ID" --placement right --cwd <directory> --name <name> --json
```

Override model or effort for only this launch when requested:

```sh
"$COVE_CLI_PATH" agent launch <adapter> --profile <slug> --model <model-id> --effort <level> --relative-to "$COVE_NOOK_ID" --placement right --cwd <directory> --name <name> --json
```

A profile is durable configuration. `--model` and `--effort` override the selected profile only for the launched nook and survive that nook's restart or resume. Omit them to preserve the selected profile unchanged. Do not create a temporary profile for one launch.

Add `--yolo`, `--cols <count>`, `--rows <count>`, or `--access-scope same-tab|same-shore|same-bay|all` only when the user requests them or the task requires them.

## Open and balance multiple adjacent nooks

Place the first new nook relative to the current nook. For each later open or launch, set `--relative-to` to the previously returned nook ID and use the same placement axis. This produces the requested visual order.

Repeated binary `below` or `right` opens are intentionally local 50/50 splits until balancing is requested. After the final open, balance once with its returned nook ID:

```sh
"$COVE_CLI_PATH" nook stack <final-nook-id> --placement below --json
```

Use `left` or `right` to equalize the horizontal component. Use `above` or `below` to equalize the vertical component. Balancing preserves lane order and running process state.

For a vertical stack on the right: place the first nook `right` of `"$COVE_NOOK_ID"`, place every later nook `below` the previous returned ID, then run `nook stack <final-nook-id> --placement below --json`.

Each open is atomic, but a sequence of opens is not one transaction. If a later open fails, close only the nooks created for that user request with `nook close`; do not repair the tree through raw layout operations.

## Resume

List recent sessions, then resume the exact adapter and session ID:

```sh
"$COVE_CLI_PATH" session recent --json
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

Close a terminal, agent, or browser nook through the same command:

```sh
"$COVE_CLI_PATH" nook close <nook-id> --json
```

The command removes the runtime and layout state together. Never close the current nook unless the user explicitly asks. Re-list agents first when the target came from stale context.

## Authorization failures

`access_denied` means the current nook principal lacks the required scope. Do not retry with broader scope, raw layout operations, another claimed identity, or a forged adapter name. Explain the denied boundary and ask the user or a control client to perform the operation.

`not_found` means the target is stale or outside the visible scope; inspect context and list agents again. `invalid_state` means the requested operation is not valid for the target's current state. `not_ready` means the required daemon subsystem is unavailable. Report the exact error code and message.

All mutations reconcile through daemon events. Do not issue a second layout snapshot request after a successful command, and do not instruct the user to reload the workspace.
