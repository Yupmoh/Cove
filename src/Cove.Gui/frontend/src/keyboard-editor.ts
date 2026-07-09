export interface KeybindDto {
  chord: string;
  actionType: string;
  action: string;
  description: string | null;
}

export interface KeybindRow {
  action: string;
  description: string | null;
  chord: string;
  actionType: string;
  isCustom: boolean;
  hasConflict: boolean;
}

export interface KeybindCategory {
  name: string;
  rows: KeybindRow[];
}

const CATEGORY_PREFIXES: ReadonlyArray<{ prefix: string; name: string }> = [
  { prefix: "room.", name: "Rooms" },
  { prefix: "workspace.", name: "Workspaces" },
  { prefix: "view.", name: "View" },
  { prefix: "tool.", name: "Tools" },
  { prefix: "pane.", name: "Panes" },
  { prefix: "terminal.", name: "Terminal" },
];

export function categorizeBindings(bindings: KeybindDto[], conflicts: string[], customActions: string[]): KeybindCategory[] {
  const conflictSet = new Set(conflicts.map((c) => c.toLowerCase()));
  const customSet = new Set(customActions.map((a) => a.toLowerCase()));
  const rows: KeybindRow[] = bindings.map((b) => ({
    action: b.action,
    description: b.description,
    chord: b.chord,
    actionType: b.actionType,
    isCustom: customSet.has(b.action.toLowerCase()),
    hasConflict: conflictSet.has(b.chord.toLowerCase()),
  }));
  const categories: KeybindCategory[] = [];
  for (const cat of CATEGORY_PREFIXES) {
    const catRows = rows.filter((r) => r.action.startsWith(cat.prefix));
    if (catRows.length > 0) categories.push({ name: cat.name, rows: catRows });
  }
  const otherRows = rows.filter((r) => !CATEGORY_PREFIXES.some((c) => r.action.startsWith(c.prefix)));
  if (otherRows.length > 0) categories.push({ name: "Other", rows: otherRows });
  return categories;
}

export function normalizeChord(chord: string): string {
  const parts = chord.toLowerCase().trim().split(/[+\s]+/).filter((p) => p.length > 0);
  const ordered: string[] = [];
  const mods = ["cmd", "ctrl", "alt", "shift"];
  for (const m of mods) {
    const idx = parts.indexOf(m);
    if (idx >= 0) { ordered.push(m === "cmd" ? "cmd" : m); }
  }
  for (const p of parts) {
    if (!mods.includes(p)) ordered.push(p);
  }
  return ordered.join("+");
}

export function isReservedChord(chord: string): boolean {
  const normalized = normalizeChord(chord);
  return RESERVED_CHORDS.has(normalized);
}

const RESERVED_CHORDS = new Set(["cmd+q", "cmd+tab", "ctrl+q"]);

export function isValidChord(chord: string): boolean {
  const normalized = normalizeChord(chord);
  if (normalized.length === 0) return false;
  return /^[a-z0-9+`[\]'",./;-]+$/.test(normalized);
}

export function chordDisplay(chord: string): string {
  const parts = normalizeChord(chord).split("+");
  return parts.map((p) => {
    if (p === "cmd") return "⌘";
    if (p === "ctrl") return "⌃";
    if (p === "alt") return "⌥";
    if (p === "shift") return "⇧";
    if (p === "enter") return "↵";
    if (p === "up") return "↑";
    if (p === "down") return "↓";
    if (p === "left") return "←";
    if (p === "right") return "→";
    if (p === "tab") return "⇥";
    if (p === "backspace") return "⌫";
    if (p === "escape") return "⎋";
    return p.toUpperCase();
  }).join("");
}

export function canRecordChord(chord: string, currentAction: string, existingBindings: KeybindDto[]): { valid: boolean; conflictAction: string | null } {
  const normalized = normalizeChord(chord);
  if (!isValidChord(normalized)) return { valid: false, conflictAction: null };
  if (isReservedChord(normalized)) return { valid: false, conflictAction: null };
  const existing = existingBindings.find((b) => b.chord.toLowerCase() === normalized.toLowerCase());
  if (existing && existing.action !== currentAction) return { valid: false, conflictAction: existing.action };
  return { valid: true, conflictAction: existing && existing.action === currentAction ? null : (existing?.action ?? null) };
}
