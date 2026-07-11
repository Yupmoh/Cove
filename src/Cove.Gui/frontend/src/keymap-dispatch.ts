import { isReservedChord, normalizeChord } from "./keyboard-editor";

export interface ResolvedBinding {
  chord: string;
  action: string;
  actionType: string;
}

export interface ChordEvent {
  metaKey: boolean;
  ctrlKey: boolean;
  altKey: boolean;
  shiftKey: boolean;
  key: string;
}

export type DispatchDecision =
  | { kind: "reserved" }
  | { kind: "menu-owned"; action: string }
  | { kind: "terminal"; action: string }
  | { kind: "dispatch"; action: string; actionType: string }
  | { kind: "none" };

const MODIFIER_KEYS = new Set(["meta", "control", "alt", "shift"]);

export function eventToChord(e: ChordEvent): string {
  const parts: string[] = [];
  if (e.ctrlKey) parts.push("ctrl");
  if (e.altKey) parts.push("alt");
  if (e.shiftKey) parts.push("shift");
  if (e.metaKey) parts.push("cmd");
  const key = e.key.toLowerCase();
  if (MODIFIER_KEYS.has(key)) return "";
  parts.push(key === " " ? "space" : key);
  if (parts.every((p) => ["cmd", "ctrl", "alt", "shift"].includes(p))) return "";
  return normalizeChord(parts.join("+"));
}

export function buildChordMap(bindings: ResolvedBinding[]): Map<string, ResolvedBinding> {
  const map = new Map<string, ResolvedBinding>();
  for (const b of bindings) map.set(normalizeChord(b.chord), b);
  return map;
}

export function resolveDispatch(chord: string, chordMap: Map<string, ResolvedBinding>, menuChords: Set<string>): DispatchDecision {
  const normalized = normalizeChord(chord);
  if (isReservedChord(normalized)) return { kind: "reserved" };
  const binding = chordMap.get(normalized);
  if (!binding) return { kind: "none" };
  if (binding.action.startsWith("terminal.")) return { kind: "terminal", action: binding.action };
  if (binding.actionType === "send-text") return { kind: "terminal", action: binding.action };
  if (menuChords.has(normalized)) return { kind: "menu-owned", action: binding.action };
  return { kind: "dispatch", action: binding.action, actionType: binding.actionType };
}

export function defaultBindings(): ResolvedBinding[] {
  const app = (chord: string, action: string): ResolvedBinding => ({ chord, action, actionType: "app-command" });
  return [
    app("cmd+t", "shore.new"),
    app("cmd+w", "nook.close"),
    app("cmd+shift+w", "shore.close"),
    app("cmd+shift+[", "shore.prev"),
    app("cmd+shift+]", "shore.next"),
    app("cmd+shift+p", "shore.pin"),
    app("cmd+shift+n", "bay.create"),
    app("cmd+1", "bay.switch-1"),
    app("cmd+2", "bay.switch-2"),
    app("cmd+3", "bay.switch-3"),
    app("cmd+4", "bay.switch-4"),
    app("cmd+5", "bay.switch-5"),
    app("cmd+6", "bay.switch-6"),
    app("cmd+7", "bay.switch-7"),
    app("cmd+8", "bay.switch-8"),
    app("cmd+9", "bay.switch-9"),
    app("cmd+b", "view.toggle-sidebar"),
    app("cmd+shift+a", "view.toggle-notepad"),
    app("cmd+shift+`", "view.zen-mode"),
    app("cmd+=", "view.zoom-in"),
    app("cmd+-", "view.zoom-out"),
    app("cmd+0", "view.zoom-reset"),
    app("cmd+shift+g", "tool.git"),
    app("cmd+shift+f", "tool.search"),
    app("cmd+shift+b", "tool.browser"),
    app("cmd+shift+k", "tool.tasks"),
    app("cmd+shift+l", "tool.library"),
    app("cmd+shift+t", "tool.palette"),
    app("cmd+l", "tool.launcher"),
    app("cmd+d", "nook.split-right"),
    app("cmd+shift+d", "nook.split-down"),
    app("cmd+[", "nook.focus-prev"),
    app("cmd+]", "nook.focus-next"),
    app("cmd+f", "nook.find"),
    app("cmd+shift+up", "nook.scroll-top"),
    app("cmd+shift+down", "nook.scroll-bottom"),
    app("cmd+enter", "nook.maximize"),
    app("cmd+k", "tool.palette"),
    app("cmd+,", "app.settings"),
  ];
}
