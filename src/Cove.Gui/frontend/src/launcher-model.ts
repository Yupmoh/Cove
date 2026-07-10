export function detectedHarnessTiles<T extends { disabled: boolean }>(tiles: T[]): T[] {
  return tiles.filter((t) => !t.disabled);
}

export const LAUNCHER_ACCENTS = ["#fab387", "#94e2d5", "#a6e3a1", "#89b4fa", "#f5c2e7"];

export function hashAccent(name: string): string {
  let h = 0;
  for (let i = 0; i < name.length; i++) {
    h = (h * 31 + name.charCodeAt(i)) >>> 0;
  }
  return LAUNCHER_ACCENTS[h % LAUNCHER_ACCENTS.length];
}

export function adapterAccent(name: string, manifestAccent: string): string {
  const manifest = manifestAccent.trim();
  if (manifest.length > 0) return manifest;
  return hashAccent(name);
}

export const TOOL_ACCENTS: Record<string, string> = {
  terminal: "#cba6f7",
  browser: "#89b4fa",
  search: "#94e2d5",
  git: "#fab387",
  tasks: "#b4befe",
  notepad: "#f9e2af",
};

export function toolAccent(id: string): string {
  return TOOL_ACCENTS[id] ?? "#cba6f7";
}

export function assignHotkeys(labels: string[], reserved: string[] = []): string[] {
  const used = new Set(reserved.map((r) => r.toUpperCase()));
  return labels.map((label) => {
    for (const ch of label.toUpperCase()) {
      if (ch >= "A" && ch <= "Z" && !used.has(ch)) {
        used.add(ch);
        return ch;
      }
    }
    return "";
  });
}

export type LauncherSection = "harness" | "tool";

export interface LauncherSelection {
  section: LauncherSection;
  index: number;
}

export interface LauncherGeometry {
  harnessCount: number;
  harnessCols: number;
  toolCount: number;
}

export type LauncherArrowKey = "ArrowLeft" | "ArrowRight" | "ArrowUp" | "ArrowDown";

export function initialLauncherSelection(geo: LauncherGeometry): LauncherSelection {
  if (geo.harnessCount > 0) return { section: "harness", index: 0 };
  return { section: "tool", index: 0 };
}

export function clampLauncherSelection(sel: LauncherSelection, geo: LauncherGeometry): LauncherSelection {
  if (sel.section === "harness") {
    if (geo.harnessCount <= 0) return initialLauncherSelection(geo);
    return { section: "harness", index: Math.max(0, Math.min(sel.index, geo.harnessCount - 1)) };
  }
  if (geo.toolCount <= 0) return initialLauncherSelection(geo);
  return { section: "tool", index: Math.max(0, Math.min(sel.index, geo.toolCount - 1)) };
}

export function moveLauncherSelection(sel: LauncherSelection, key: LauncherArrowKey, geo: LauncherGeometry): LauncherSelection {
  const cols = Math.max(1, geo.harnessCols);
  const s = clampLauncherSelection(sel, geo);
  if (s.section === "harness") {
    const col = s.index % cols;
    switch (key) {
      case "ArrowLeft":
        return s.index > 0 ? { section: "harness", index: s.index - 1 } : s;
      case "ArrowRight":
        return s.index < geo.harnessCount - 1 ? { section: "harness", index: s.index + 1 } : s;
      case "ArrowUp":
        return s.index - cols >= 0 ? { section: "harness", index: s.index - cols } : s;
      case "ArrowDown": {
        const down = s.index + cols;
        if (down < geo.harnessCount) return { section: "harness", index: down };
        if (geo.toolCount > 0) return { section: "tool", index: Math.min(col, geo.toolCount - 1) };
        return s;
      }
    }
  }
  switch (key) {
    case "ArrowLeft":
      return s.index > 0 ? { section: "tool", index: s.index - 1 } : s;
    case "ArrowRight":
      return s.index < geo.toolCount - 1 ? { section: "tool", index: s.index + 1 } : s;
    case "ArrowUp": {
      if (geo.harnessCount <= 0) return s;
      const bottomRowStart = cols * Math.floor((geo.harnessCount - 1) / cols);
      const target = Math.min(bottomRowStart + Math.min(s.index, cols - 1), geo.harnessCount - 1);
      return { section: "harness", index: target };
    }
    case "ArrowDown":
      return s;
  }
  return s;
}

export function hotkeyTarget(letter: string, harnessLetters: string[], toolLetters: string[]): LauncherSelection | null {
  const up = letter.toUpperCase();
  if (up.length !== 1 || up < "A" || up > "Z") return null;
  const h = harnessLetters.findIndex((l) => l !== "" && l.toUpperCase() === up);
  if (h >= 0) return { section: "harness", index: h };
  const t = toolLetters.findIndex((l) => l !== "" && l.toUpperCase() === up);
  if (t >= 0) return { section: "tool", index: t };
  return null;
}

export interface LauncherSession {
  paneId: string;
  adapter: string;
  sessionId: string | null;
  lifecycle: string;
  resumable: boolean;
}

export function resumableSessionsFor(adapter: string, sessions: LauncherSession[]): LauncherSession[] {
  return sessions.filter((s) => s.adapter === adapter && s.resumable && (s.sessionId ?? "").length > 0);
}

export function mostRecentSession(sessions: LauncherSession[]): LauncherSession | null {
  return sessions.length > 0 ? sessions[0] : null;
}

export interface RecentSessionRow {
  adapter: string;
  sessionId: string;
  workspaceId: string;
  cwd: string;
  startedAt: string;
}

export interface RecentSessionView {
  adapter: string;
  sessionId: string;
  cwd: string;
  cwdBase: string;
  relative: string;
}

export function cwdBasename(cwd: string): string {
  const trimmed = cwd.replace(/[/\\]+$/, "");
  if (trimmed.length === 0) return "~";
  const parts = trimmed.split(/[/\\]/);
  const last = parts[parts.length - 1];
  return last.length > 0 ? last : "~";
}

export function relativeTime(startedAt: string, nowMs: number): string {
  const then = Date.parse(startedAt);
  if (Number.isNaN(then)) return "";
  const deltaSec = Math.max(0, Math.round((nowMs - then) / 1000));
  if (deltaSec < 45) return "just now";
  const min = Math.round(deltaSec / 60);
  if (min < 60) return `${min}m ago`;
  const hr = Math.round(min / 60);
  if (hr < 24) return `${hr}h ago`;
  const day = Math.round(hr / 24);
  return `${day}d ago`;
}

export function shapeRecentSessions(rows: RecentSessionRow[], nowMs: number, limit: number): RecentSessionView[] {
  return rows.slice(0, Math.max(0, limit)).map((r) => ({
    adapter: r.adapter,
    sessionId: r.sessionId,
    cwd: r.cwd,
    cwdBase: cwdBasename(r.cwd),
    relative: relativeTime(r.startedAt, nowMs),
  }));
}

export const LAUNCHER_TIPS = [
  "press a letter to launch — hold shift to reveal them",
  "⌘↵ starts a new session in the selected harness",
  "arrow keys move between cards and tools",
  "⌘l recalls the launcher in any empty pane",
  "detected harnesses launch straight away — no prompts",
];

export function tipAt(index: number): string {
  const n = LAUNCHER_TIPS.length;
  return LAUNCHER_TIPS[((index % n) + n) % n];
}
