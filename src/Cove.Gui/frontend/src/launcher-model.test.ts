import { describe, it, expect } from "vitest";
import {
  detectedHarnessTiles,
  LAUNCHER_ACCENTS,
  hashAccent,
  adapterAccent,
  toolAccent,
  assignHotkeys,
  initialLauncherSelection,
  clampLauncherSelection,
  moveLauncherSelection,
  hotkeyTarget,
  resumableSessionsFor,
  mostRecentSession,
  tipAt,
  LAUNCHER_TIPS,
  cwdBasename,
  relativeTime,
  shapeRecentSessions,
  computeLauncherCols,
  resolveLauncherYolo,
  resolveLauncherProjectDir,
  filterSessionRows,
  SESSION_FILTER_MIN_ROWS,
  type LauncherGeometry,
  type LauncherSession,
  type RecentSessionRow,
} from "./launcher-model";

describe("resolveLauncherProjectDir", () => {
  it("uses the active layout project before the asynchronously loaded bay list", () => {
    expect(resolveLauncherProjectDir(
      { id: "in-spades", projectDir: "/work/InSpades" },
      [],
    )).toBe("/work/InSpades");
  });

  it("falls back to matching bay metadata for an older layout payload", () => {
    expect(resolveLauncherProjectDir(
      { id: "in-spades" },
      [{ id: "in-spades", projectDir: "/work/InSpades" }],
    )).toBe("/work/InSpades");
  });

  it("falls back to defaultDir when no layout or bay dir exists", () => {
    expect(resolveLauncherProjectDir(
      { id: "empty" },
      [{ id: "empty" }],
      "/home/user",
    )).toBe("/home/user");
  });

  it("prefers an explicit bay dir over defaultDir", () => {
    expect(resolveLauncherProjectDir(
      { id: "in-spades" },
      [{ id: "in-spades", projectDir: "/work/InSpades" }],
      "/home/user",
    )).toBe("/work/InSpades");
  });

  it("prefers the active layout project over defaultDir", () => {
    expect(resolveLauncherProjectDir(
      { id: "in-spades", projectDir: "/work/InSpades" },
      [],
      "/home/user",
    )).toBe("/work/InSpades");
  });

  it("returns empty string when no dir and no defaultDir", () => {
    expect(resolveLauncherProjectDir(null, [])).toBe("");
  });

  it("uses defaultDir when layout is null", () => {
    expect(resolveLauncherProjectDir(null, [], "/home/user")).toBe("/home/user");
  });
});

describe("resolveLauncherYolo", () => {
  it("defaults claude-code to bypass permissions", () => {
    expect(resolveLauncherYolo(null, "claude-code")).toBe(true);
  });

  it("defaults other adapters to prompting", () => {
    expect(resolveLauncherYolo(null, "codex")).toBe(false);
  });

  it("honors a stored true regardless of adapter", () => {
    expect(resolveLauncherYolo("true", "codex")).toBe(true);
  });

  it("honors a stored false for claude-code", () => {
    expect(resolveLauncherYolo("false", "claude-code")).toBe(false);
  });
});

describe("detectedHarnessTiles", () => {
  it("keeps only detected harnesses, dropping undetected ones", () => {
    const tiles = [
      { id: "a", disabled: false },
      { id: "b", disabled: true },
      { id: "c", disabled: false },
    ];
    expect(detectedHarnessTiles(tiles).map((t) => t.id)).toEqual(["a", "c"]);
  });
  it("returns an empty list when nothing is detected", () => {
    expect(detectedHarnessTiles([{ id: "a", disabled: true }])).toEqual([]);
  });
});

describe("adapterAccent", () => {
  it("prefers a non-empty manifest accent verbatim", () => {
    expect(adapterAccent("claude", "#ff0000")).toBe("#ff0000");
    expect(adapterAccent("claude", "  #00ff00  ")).toBe("#00ff00");
  });
  it("falls back to a deterministic palette hash when the manifest has none", () => {
    const a = adapterAccent("claude", "");
    const b = adapterAccent("claude", "   ");
    expect(a).toBe(b);
    expect(LAUNCHER_ACCENTS).toContain(a);
  });
  it("hashes different names to stable palette entries", () => {
    expect(hashAccent("codex")).toBe(hashAccent("codex"));
    expect(LAUNCHER_ACCENTS).toContain(hashAccent("gemini"));
  });
});

describe("toolAccent", () => {
  it("maps each nook type to its own accent and defaults to mauve", () => {
    expect(toolAccent("browser")).toBe("#89b4fa");
    expect(toolAccent("git")).toBe("#fab387");
    expect(toolAccent("unknown")).toBe("#cba6f7");
  });
});

describe("assignHotkeys", () => {
  it("assigns the first free letter of each label, avoiding collisions", () => {
    expect(assignHotkeys(["Claude Code", "Codex", "Gemini"])).toEqual(["C", "O", "G"]);
  });
  it("honors reserved letters from the tool row", () => {
    expect(assignHotkeys(["Terminal"], ["T"])).toEqual(["E"]);
  });
  it("returns an empty letter when nothing is available", () => {
    expect(assignHotkeys(["123"], [])).toEqual([""]);
    expect(assignHotkeys(["AA"], ["A"])).toEqual([""]);
  });
});

describe("launcher selection geometry", () => {
  const geo: LauncherGeometry = { harnessCount: 5, harnessCols: 3, toolCount: 6 };
  it("starts on the first harness card, or the tool row when no harness", () => {
    expect(initialLauncherSelection(geo)).toEqual({ section: "harness", index: 0 });
    expect(initialLauncherSelection({ harnessCount: 0, harnessCols: 3, toolCount: 6 })).toEqual({ section: "tool", index: 0 });
  });
  it("moves left/right within the harness grid and clamps at the edges", () => {
    expect(moveLauncherSelection({ section: "harness", index: 1 }, "ArrowRight", geo)).toEqual({ section: "harness", index: 2 });
    expect(moveLauncherSelection({ section: "harness", index: 0 }, "ArrowLeft", geo)).toEqual({ section: "harness", index: 0 });
    expect(moveLauncherSelection({ section: "harness", index: 4 }, "ArrowRight", geo)).toEqual({ section: "harness", index: 4 });
  });
  it("moves up and down a row inside the harness grid", () => {
    expect(moveLauncherSelection({ section: "harness", index: 4 }, "ArrowUp", geo)).toEqual({ section: "harness", index: 1 });
    expect(moveLauncherSelection({ section: "harness", index: 1 }, "ArrowUp", geo)).toEqual({ section: "harness", index: 1 });
    expect(moveLauncherSelection({ section: "harness", index: 0 }, "ArrowDown", geo)).toEqual({ section: "harness", index: 3 });
  });
  it("drops from the last harness row down into the tool row, mapping the column", () => {
    expect(moveLauncherSelection({ section: "harness", index: 4 }, "ArrowDown", geo)).toEqual({ section: "tool", index: 1 });
    expect(moveLauncherSelection({ section: "harness", index: 3 }, "ArrowDown", geo)).toEqual({ section: "tool", index: 0 });
  });
  it("rises from the tool row back onto the bottom harness row at the same column", () => {
    expect(moveLauncherSelection({ section: "tool", index: 1 }, "ArrowUp", geo)).toEqual({ section: "harness", index: 4 });
    expect(moveLauncherSelection({ section: "tool", index: 0 }, "ArrowUp", geo)).toEqual({ section: "harness", index: 3 });
    expect(moveLauncherSelection({ section: "tool", index: 5 }, "ArrowUp", geo)).toEqual({ section: "harness", index: 4 });
  });
  it("moves within the tool row and stays put at the bottom", () => {
    expect(moveLauncherSelection({ section: "tool", index: 2 }, "ArrowRight", geo)).toEqual({ section: "tool", index: 3 });
    expect(moveLauncherSelection({ section: "tool", index: 0 }, "ArrowDown", geo)).toEqual({ section: "tool", index: 0 });
  });
  it("clamps a stale selection when the geometry shrinks", () => {
    expect(clampLauncherSelection({ section: "harness", index: 9 }, geo)).toEqual({ section: "harness", index: 4 });
    expect(clampLauncherSelection({ section: "harness", index: 0 }, { harnessCount: 0, harnessCols: 3, toolCount: 6 })).toEqual({ section: "tool", index: 0 });
  });
});

describe("computeLauncherCols", () => {
  it("fits three cards in a wide container", () => {
    expect(computeLauncherCols(680, 3, 3)).toBe(3);
  });

  it("drops to fewer columns as the container narrows", () => {
    expect(computeLauncherCols(460, 3, 3)).toBe(2);
    expect(computeLauncherCols(300, 3, 3)).toBe(1);
  });

  it("never exceeds the card count and never returns zero", () => {
    expect(computeLauncherCols(9999, 2, 3)).toBe(2);
    expect(computeLauncherCols(0, 3, 3)).toBe(1);
    expect(computeLauncherCols(680, 0, 3)).toBe(1);
  });
});

describe("hotkeyTarget", () => {
  it("resolves a harness letter first, then the tool row", () => {
    expect(hotkeyTarget("o", ["C", "O"], ["T", "B"])).toEqual({ section: "harness", index: 1 });
    expect(hotkeyTarget("b", ["C", "O"], ["T", "B"])).toEqual({ section: "tool", index: 1 });
  });
  it("returns null for unassigned or non-letter keys", () => {
    expect(hotkeyTarget("z", ["C"], ["T"])).toBeNull();
    expect(hotkeyTarget("1", ["C"], ["T"])).toBeNull();
    expect(hotkeyTarget("", ["C"], ["T"])).toBeNull();
  });
});

describe("session resolution", () => {
  const sessions: LauncherSession[] = [
    { nookId: "p1", adapter: "claude", sessionId: "s1", lifecycle: "background", resumable: true },
    { nookId: "p2", adapter: "claude", sessionId: null, lifecycle: "dismissed", resumable: true },
    { nookId: "p3", adapter: "claude", sessionId: "s3", lifecycle: "dismissed", resumable: false },
    { nookId: "p4", adapter: "codex", sessionId: "s4", lifecycle: "background", resumable: true },
  ];
  it("keeps only resumable sessions with a session id for the adapter", () => {
    expect(resumableSessionsFor("claude", sessions).map((s) => s.nookId)).toEqual(["p1"]);
    expect(resumableSessionsFor("codex", sessions).map((s) => s.nookId)).toEqual(["p4"]);
  });
  it("returns the first session as most-recent, or null when empty", () => {
    expect(mostRecentSession(resumableSessionsFor("claude", sessions))?.sessionId).toBe("s1");
    expect(mostRecentSession([])).toBeNull();
  });
});

describe("tip rotation", () => {
  it("rotates deterministically by index with wraparound", () => {
    expect(tipAt(0)).toBe(LAUNCHER_TIPS[0]);
    expect(tipAt(LAUNCHER_TIPS.length)).toBe(LAUNCHER_TIPS[0]);
    expect(tipAt(-1)).toBe(LAUNCHER_TIPS[LAUNCHER_TIPS.length - 1]);
  });
});

describe("recent session shaping", () => {
  it("extracts the trailing path segment as the basename", () => {
    expect(cwdBasename("/home/moh/proj")).toBe("proj");
    expect(cwdBasename("/home/moh/proj/")).toBe("proj");
    expect(cwdBasename("C:\\work\\cove")).toBe("cove");
    expect(cwdBasename("")).toBe("~");
    expect(cwdBasename("/")).toBe("~");
  });

  it("renders coarse relative times", () => {
    const now = Date.parse("2026-07-10T12:00:00Z");
    expect(relativeTime("2026-07-10T11:59:40Z", now)).toBe("just now");
    expect(relativeTime("2026-07-10T11:55:00Z", now)).toBe("5m ago");
    expect(relativeTime("2026-07-10T10:00:00Z", now)).toBe("2h ago");
    expect(relativeTime("2026-07-07T12:00:00Z", now)).toBe("3d ago");
    expect(relativeTime("not-a-date", now)).toBe("");
  });

  it("shapes and caps recent rows preserving order", () => {
    const now = Date.parse("2026-07-10T12:00:00Z");
    const rows: RecentSessionRow[] = [
      { adapter: "claude", sessionId: "s1", bayId: "w", cwd: "/home/moh/alpha", startedAt: "2026-07-10T11:55:00Z" },
      { adapter: "claude", sessionId: "s2", bayId: "w", cwd: "/home/moh/beta", startedAt: "2026-07-10T11:00:00Z" },
      { adapter: "claude", sessionId: "s3", bayId: "w", cwd: "/home/moh/gamma", startedAt: "2026-07-09T12:00:00Z" },
      { adapter: "claude", sessionId: "s4", bayId: "w", cwd: "/home/moh/delta", startedAt: "2026-07-08T12:00:00Z" },
    ];
    const shaped = shapeRecentSessions(rows, now, 3);
    expect(shaped.map((s) => s.sessionId)).toEqual(["s1", "s2", "s3"]);
    expect(shaped[0].cwdBase).toBe("alpha");
    expect(shaped[0].relative).toBe("5m ago");
  });
});

describe("filterSessionRows", () => {
  const rows = [
    { label: "fix inventory desync", cwd: "/Users/moh/Work/Raptor" },
    { label: "voice dictation", cwd: "/Users/moh/Work/Cove" },
    { label: "session", cwd: "/tmp/CLIProxyAPI-src" },
  ];

  it("returns all rows for an empty or whitespace query", () => {
    expect(filterSessionRows(rows, "")).toEqual(rows);
    expect(filterSessionRows(rows, "   ")).toEqual(rows);
  });

  it("matches label substrings case-insensitively", () => {
    expect(filterSessionRows(rows, "DICT")).toEqual([rows[1]]);
  });

  it("matches against the cwd too", () => {
    expect(filterSessionRows(rows, "cliproxy")).toEqual([rows[2]]);
  });

  it("requires every token to match somewhere", () => {
    expect(filterSessionRows(rows, "cove voice")).toEqual([rows[1]]);
    expect(filterSessionRows(rows, "cove desync")).toEqual([]);
  });
});

describe("SESSION_FILTER_MIN_ROWS", () => {
  it("keeps the filter hidden for short lists", () => {
    expect(5 >= SESSION_FILTER_MIN_ROWS).toBe(false);
    expect(6 >= SESSION_FILTER_MIN_ROWS).toBe(true);
  });
});
