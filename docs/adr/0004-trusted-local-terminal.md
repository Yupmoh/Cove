# 0004 — Trusted local terminal security posture

Status: Accepted

## Context

A general terminal must allow-list the user's own shell, which the framework's own documentation flags as defeating the shell-argument sandbox. Pretending the argv-scope sandbox protects anything once `/bin/zsh` is allowed would be dishonest.

## Decision

Adopt the trusted-local-terminal posture explicitly: the engine runs the user's real shell and agents with their real environment, and the argv-scope sandbox is not treated as a security boundary. Security effort goes into the control-channel trust boundary instead — the Unix socket is mode 0600, the Windows named pipe grants the current user SID only, and channels (stable/beta/dev) are isolated so instances never collide. The threat model is owner-only local access, not defense against a hostile process already running as the user (which already owns the socket).

## Consequences

- No false sense of shell sandboxing.
- Trust is enforced at the socket/pipe, not the shell argv.
- Any future cross-origin loopback path adds a per-launch token; the M0 socket does not need one.

## Inventory IDs

- None directly — this is the security posture underpinning PL-29 (control channel).

## Related

Realized by the endpoint permissions in 0005.
