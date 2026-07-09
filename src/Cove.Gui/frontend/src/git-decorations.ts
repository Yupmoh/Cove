export type GutterKind = "added" | "modified" | "deleted";

export interface GutterDecoration {
  line: number;
  kind: GutterKind;
}

export function parseDiffDecorations(patch: string): GutterDecoration[] {
  const decorations: GutterDecoration[] = [];
  const lines = patch.split("\n");
  let newLine = 0;
  let removedRun = 0;
  let addedRun = 0;
  let addedStart = 0;

  const flush = () => {
    if (addedRun > 0) {
      const kind: GutterKind = removedRun > 0 ? "modified" : "added";
      for (let i = 0; i < addedRun; i++) decorations.push({ line: addedStart + i, kind });
    } else if (removedRun > 0) {
      decorations.push({ line: newLine > 0 ? newLine : 1, kind: "deleted" });
    }
    removedRun = 0;
    addedRun = 0;
  };

  for (const raw of lines) {
    if (raw.startsWith("@@")) {
      flush();
      const m = /@@ -\d+(?:,\d+)? \+(\d+)(?:,\d+)? @@/.exec(raw);
      newLine = m ? parseInt(m[1], 10) : 1;
      continue;
    }
    if (raw.startsWith("+++") || raw.startsWith("---") || raw.startsWith("diff ") || raw.startsWith("index ")) continue;
    const c = raw[0];
    if (c === "+") {
      if (addedRun === 0) addedStart = newLine;
      addedRun++;
      newLine++;
    } else if (c === "-") {
      removedRun++;
    } else {
      flush();
      newLine++;
    }
  }
  flush();
  return decorations;
}

export interface BlameLineLike {
  line: number;
  commit: string;
  author: string;
  relativeTime: string;
}

export function blameForLine(lines: BlameLineLike[], line: number): BlameLineLike | null {
  return lines.find((l) => l.line === line) ?? null;
}

export function formatBlameHover(b: BlameLineLike): string {
  const sha = b.commit.length > 8 ? b.commit.slice(0, 8) : b.commit;
  const when = b.relativeTime ? ` · ${b.relativeTime}` : "";
  return `${b.author} · ${sha}${when}`;
}
