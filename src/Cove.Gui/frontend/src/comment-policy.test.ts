import { readdirSync, readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import ts from "typescript";
import { describe, expect, it } from "vitest";

function sourceFiles(root: string): string[] {
  return readdirSync(root, { withFileTypes: true }).flatMap((entry) => {
    const path = join(root, entry.name);
    if (entry.isDirectory()) return sourceFiles(path);
    if (!entry.name.endsWith(".ts") || entry.name.endsWith(".test.ts")) return [];
    return [path];
  });
}

function commentOffsets(source: string): number[] {
  const file = ts.createSourceFile(
    "source.ts",
    source,
    ts.ScriptTarget.Latest,
    true,
    ts.ScriptKind.TS,
  );
  const offsets = new Set<number>();
  const visit = (node: ts.Node): void => {
    for (const range of ts.getLeadingCommentRanges(source, node.getFullStart()) ?? []) {
      offsets.add(range.pos);
    }
    for (const range of ts.getTrailingCommentRanges(source, node.end) ?? []) {
      offsets.add(range.pos);
    }
    for (const child of node.getChildren(file)) visit(child);
  };
  visit(file);
  return [...offsets].sort((left, right) => left - right);
}

describe("production TypeScript comment policy", () => {
  it("contains no comments", () => {
    const root = dirname(fileURLToPath(import.meta.url));
    const violations = sourceFiles(root).flatMap((path) => {
      const source = readFileSync(path, "utf8");
      const file = ts.createSourceFile(path, source, ts.ScriptTarget.Latest);
      return commentOffsets(source).map((offset) => {
        const position = file.getLineAndCharacterOfPosition(offset);
        return `${path.slice(root.length + 1)}:${position.line + 1}:${position.character + 1}`;
      });
    });

    expect(violations).toEqual([]);
  });

  it("detects line and block comments without matching strings or regular expressions", () => {
    const source = [
      "const uri = 'cove://commands/test';",
      "const expression = /https?:\\/\\//;",
      "// line",
      "/* block */",
    ].join("\n");

    expect(commentOffsets(source)).toHaveLength(2);
  });
});
