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
  type LauncherGeometry,
  type LauncherSession,
} from "./launcher-model";

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
  it("maps each pane type to its own accent and defaults to mauve", () => {
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
    { paneId: "p1", adapter: "claude", sessionId: "s1", lifecycle: "background", resumable: true },
    { paneId: "p2", adapter: "claude", sessionId: null, lifecycle: "dismissed", resumable: true },
    { paneId: "p3", adapter: "claude", sessionId: "s3", lifecycle: "dismissed", resumable: false },
    { paneId: "p4", adapter: "codex", sessionId: "s4", lifecycle: "background", resumable: true },
  ];
  it("keeps only resumable sessions with a session id for the adapter", () => {
    expect(resumableSessionsFor("claude", sessions).map((s) => s.paneId)).toEqual(["p1"]);
    expect(resumableSessionsFor("codex", sessions).map((s) => s.paneId)).toEqual(["p4"]);
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
