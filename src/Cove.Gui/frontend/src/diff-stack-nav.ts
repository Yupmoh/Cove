export interface DiffStackFile {
  filePath: string;
  hunkCount: number;
}

export interface DiffStackCursor {
  fileIndex: number;
  hunkIndex: number;
}

export function initialCursor(): DiffStackCursor {
  return { fileIndex: 0, hunkIndex: 0 };
}

export function nextFile(cursor: DiffStackCursor, files: DiffStackFile[]): DiffStackCursor {
  if (files.length === 0) return cursor;
  return { fileIndex: Math.min(cursor.fileIndex + 1, files.length - 1), hunkIndex: 0 };
}

export function prevFile(cursor: DiffStackCursor, files: DiffStackFile[]): DiffStackCursor {
  if (files.length === 0) return cursor;
  return { fileIndex: Math.max(cursor.fileIndex - 1, 0), hunkIndex: 0 };
}

export function nextHunk(cursor: DiffStackCursor, files: DiffStackFile[]): DiffStackCursor {
  if (files.length === 0) return cursor;
  const current = files[cursor.fileIndex];
  if (cursor.hunkIndex + 1 < current.hunkCount) return { fileIndex: cursor.fileIndex, hunkIndex: cursor.hunkIndex + 1 };
  if (cursor.fileIndex + 1 < files.length) return { fileIndex: cursor.fileIndex + 1, hunkIndex: 0 };
  return cursor;
}

export function prevHunk(cursor: DiffStackCursor, files: DiffStackFile[]): DiffStackCursor {
  if (files.length === 0) return cursor;
  if (cursor.hunkIndex > 0) return { fileIndex: cursor.fileIndex, hunkIndex: cursor.hunkIndex - 1 };
  if (cursor.fileIndex > 0) {
    const prev = files[cursor.fileIndex - 1];
    return { fileIndex: cursor.fileIndex - 1, hunkIndex: Math.max(prev.hunkCount - 1, 0) };
  }
  return cursor;
}

export type DiffStackAction = "next-file" | "prev-file" | "next-hunk" | "prev-hunk" | "open" | "mark-reviewed";

export interface DiffStackKeyEvent {
  key: string;
  metaKey: boolean;
  ctrlKey: boolean;
  altKey: boolean;
  shiftKey: boolean;
}

export function resolveDiffStackKey(e: DiffStackKeyEvent): DiffStackAction | null {
  if (e.metaKey || e.ctrlKey || e.altKey) return null;
  switch (e.key) {
    case "j":
    case "ArrowDown":
      return "next-file";
    case "k":
    case "ArrowUp":
      return "prev-file";
    case "n":
      return "next-hunk";
    case "p":
      return "prev-hunk";
    case "Enter":
      return "open";
    case " ":
      return "mark-reviewed";
    default:
      return null;
  }
}
