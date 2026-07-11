import { describe, it, expect } from "vitest";
import {
  resumeSpawnPlan,
  recentsForProjectDir,
  groupRecentsByAdapter,
  sessionLabel,
  type VaultResumeResult,
} from "./session-resume";
import type { RecentSessionRow } from "./launcher-model";

function row(over: Partial<RecentSessionRow>): RecentSessionRow {
  return {
    adapter: "claude-code",
    sessionId: "s1",
    workspaceId: "ws",
    cwd: "/home/me/proj",
    startedAt: new Date().toISOString(),
    ...over,
  };
}

describe("resumeSpawnPlan", () => {
  const projectDir = "/home/me/proj";

  it("maps a successful resume to a spawn with cwd = projectDir and no toast", () => {
    const result: VaultResumeResult = {
      ok: true,
      adapter: "claude-code",
      command: ["claude", "resume", "abc"],
      cwd: "/some/other/dir",
      fallback: "none",
      error: null,
    };
    const action = resumeSpawnPlan(result, projectDir, "Claude Code");
    expect(action.kind).toBe("spawn");
    if (action.kind !== "spawn") return;
    expect(action.command).toBe("claude");
    expect(action.args).toEqual(["resume", "abc"]);
    expect(action.cwd).toBe(projectDir);
    expect(action.roomName).toBe("Claude Code");
    expect(action.toast).toBeNull();
  });

  it("maps a fresh fallback to a spawn with a toast", () => {
    const result: VaultResumeResult = {
      ok: true,
      adapter: "claude-code",
      command: ["claude"],
      cwd: "",
      fallback: "fresh",
      error: "session reaped",
    };
    const action = resumeSpawnPlan(result, projectDir, "Claude Code");
    expect(action.kind).toBe("spawn");
    if (action.kind !== "spawn") return;
    expect(action.toast).not.toBeNull();
    expect(action.toast?.body).toContain("fresh Claude Code session");
  });

  it("maps a failed result to an error action", () => {
    const result: VaultResumeResult = {
      ok: false,
      adapter: "claude-code",
      command: [],
      cwd: "",
      fallback: "none",
      error: "boom",
    };
    const action = resumeSpawnPlan(result, projectDir, "Claude Code");
    expect(action.kind).toBe("error");
    if (action.kind !== "error") return;
    expect(action.toast.body).toContain("boom");
  });
});

describe("recentsForProjectDir", () => {
  it("keeps only rows whose cwd matches the project dir, ignoring trailing slashes", () => {
    const rows = [
      row({ sessionId: "a", cwd: "/home/me/proj" }),
      row({ sessionId: "b", cwd: "/home/me/proj/" }),
      row({ sessionId: "c", cwd: "/home/me/other" }),
    ];
    const kept = recentsForProjectDir(rows, "/home/me/proj");
    expect(kept.map((r) => r.sessionId)).toEqual(["a", "b"]);
  });

  it("returns nothing when the project dir is empty", () => {
    expect(recentsForProjectDir([row({})], "")).toEqual([]);
  });
});

describe("groupRecentsByAdapter", () => {
  it("groups scoped rows by adapter and resolves display names", () => {
    const rows = [
      row({ adapter: "claude-code", sessionId: "a", cwd: "/p" }),
      row({ adapter: "codex", sessionId: "b", cwd: "/p" }),
      row({ adapter: "claude-code", sessionId: "c", cwd: "/p" }),
      row({ adapter: "claude-code", sessionId: "d", cwd: "/elsewhere" }),
    ];
    const groups = groupRecentsByAdapter(rows, "/p", [
      { name: "claude-code", displayName: "Claude Code" },
      { name: "codex", displayName: "Codex" },
    ], Date.now());
    expect(groups.map((g) => g.adapter)).toEqual(["claude-code", "codex"]);
    expect(groups[0].displayName).toBe("Claude Code");
    expect(groups[0].sessions.map((s) => s.sessionId)).toEqual(["a", "c"]);
    expect(groups[1].sessions.map((s) => s.sessionId)).toEqual(["b"]);
  });

  it("falls back to the raw adapter name when no display name is known", () => {
    const groups = groupRecentsByAdapter([row({ adapter: "mystery", cwd: "/p" })], "/p", [], Date.now());
    expect(groups[0].displayName).toBe("mystery");
  });

  it("carries the adapter-provided label and a relative time on each entry", () => {
    const nowMs = Date.parse("2026-07-11T12:00:00Z");
    const rows = [
      { ...row({ adapter: "claude-code", sessionId: "a", cwd: "/p", startedAt: "2026-07-11T09:00:00Z" }), label: "Refactor the router" } as RecentSessionRow,
    ];
    const groups = groupRecentsByAdapter(rows, "/p", [{ name: "claude-code", displayName: "Claude Code" }], nowMs);
    expect(groups[0].sessions[0].label).toBe("Refactor the router");
    expect(groups[0].sessions[0].relative).toBe("3h ago");
  });
});

describe("sessionLabel", () => {
  it("uses the descriptive label when present", () => {
    const withLabel = { ...row({}), label: "  Refactor the router  " } as RecentSessionRow;
    expect(sessionLabel(withLabel, Date.now())).toBe("Refactor the router");
  });

  it("falls back to a relative-time session label", () => {
    const nowMs = Date.parse("2026-07-11T12:00:00Z");
    const r = row({ startedAt: "2026-07-11T09:00:00Z" });
    expect(sessionLabel(r, nowMs)).toBe("3h ago session");
  });
});
