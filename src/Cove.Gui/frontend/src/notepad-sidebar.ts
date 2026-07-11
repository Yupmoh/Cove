export interface NoteListItem {
  id: string;
  title: string;
  bayId: string;
  kind: string;
  updatedAt: string;
}

export interface BayGroup {
  bayId: string;
  bayName: string;
  notes: NoteListItem[];
}

export const NoteKindIcon: Record<string, string> = {
  markdown: "\u270e",
  sketch: "\u270f",
  canvas: "\u25a3",
  mermaid: "\u25c8",
  html: "\u2261",
};

export const NoteKindColor: Record<string, string> = {
  markdown: "#e0af68",
  sketch: "#98c379",
  canvas: "#d19a66",
  mermaid: "#61afef",
  html: "#e06c75",
};

export function kindIcon(kind: string): string {
  return NoteKindIcon[kind] ?? "\u270e";
}

export function kindColor(kind: string): string {
  return NoteKindColor[kind] ?? "#6b7280";
}

export function groupByBay(notes: NoteListItem[], bayNames: Record<string, string>): BayGroup[] {
  const map = new Map<string, NoteListItem[]>();
  for (const note of notes) {
    const list = map.get(note.bayId);
    if (list) list.push(note);
    else map.set(note.bayId, [note]);
  }
  const groups: BayGroup[] = [];
  for (const [wsId, wsNotes] of map) {
    groups.push({
      bayId: wsId,
      bayName: bayNames[wsId] ?? wsId,
      notes: [...wsNotes].sort((a, b) => b.updatedAt.localeCompare(a.updatedAt)),
    });
  }
  return groups.sort((a, b) => a.bayName.localeCompare(b.bayName));
}

export interface NavState {
  groupIdx: number;
  noteIdx: number;
}

export function moveSelection(groups: BayGroup[], state: NavState, direction: "up" | "down"): NavState {
  if (groups.length === 0) return { groupIdx: -1, noteIdx: -1 };
  let { groupIdx, noteIdx } = state;
  if (groupIdx < 0) { groupIdx = 0; noteIdx = 0; return { groupIdx, noteIdx }; }

  if (direction === "down") {
    const group = groups[groupIdx];
    if (noteIdx < group.notes.length - 1) {
      noteIdx++;
    } else if (groupIdx < groups.length - 1) {
      groupIdx++;
      noteIdx = 0;
    }
  } else {
    if (noteIdx > 0) {
      noteIdx--;
    } else if (groupIdx > 0) {
      groupIdx--;
      noteIdx = groups[groupIdx].notes.length - 1;
    }
  }
  return { groupIdx, noteIdx };
}

export function flattenNotes(groups: BayGroup[]): NoteListItem[] {
  return groups.flatMap((g) => g.notes);
}

export function selectedNote(groups: BayGroup[], state: NavState): NoteListItem | null {
  if (state.groupIdx < 0 || state.groupIdx >= groups.length) return null;
  const group = groups[state.groupIdx];
  if (state.noteIdx < 0 || state.noteIdx >= group.notes.length) return null;
  return group.notes[state.noteIdx];
}
