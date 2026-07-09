import { describe, it, expect } from "vitest";
import {
  COMMENT_DIRECTIVE_FORM,
  parseComments,
  insertComment,
  resolveComment,
  deleteComment,
} from "./markdown-comments";

describe("markdown-comments", () => {
  it("documents the canonical directive form", () => {
    expect(COMMENT_DIRECTIVE_FORM).toContain(":comment[");
  });

  it("parses an inline comment directive with attributes", () => {
    const md = 'Before :comment[risky line]{#c1 author="moh" ts="2026-07-10T00:00:00Z" note="fix this"} after';
    const comments = parseComments(md);
    expect(comments).toHaveLength(1);
    expect(comments[0].id).toBe("c1");
    expect(comments[0].author).toBe("moh");
    expect(comments[0].ts).toBe("2026-07-10T00:00:00Z");
    expect(comments[0].note).toBe("fix this");
    expect(comments[0].anchorText).toBe("risky line");
    expect(comments[0].state).toBe("open");
  });

  it("parses multiple comments and records offsets", () => {
    const md = ':comment[a]{#c1 author="x" ts="t" note="n1"} mid :comment[b]{#c2 author="y" ts="t" note="n2"}';
    const comments = parseComments(md);
    expect(comments).toHaveLength(2);
    expect(md.slice(comments[0].start, comments[0].end)).toContain("#c1");
    expect(md.slice(comments[1].start, comments[1].end)).toContain("#c2");
  });

  it("inserts a comment wrapping the selected range", () => {
    const md = "the quick brown fox";
    const out = insertComment(md, 4, 9, { id: "c1", author: "moh", ts: "t", note: "why quick?" });
    expect(out).toContain(":comment[quick]{#c1");
    expect(out.startsWith("the ")).toBe(true);
    expect(out.endsWith(" brown fox")).toBe(true);
    const reparsed = parseComments(out);
    expect(reparsed[0].anchorText).toBe("quick");
    expect(reparsed[0].note).toBe("why quick?");
  });

  it("resolve flips a comment state to resolved but keeps it in the text", () => {
    const md = insertComment("alpha beta", 0, 5, { id: "c1", author: "moh", ts: "t", note: "n" });
    const resolved = resolveComment(md, "c1");
    const comments = parseComments(resolved);
    expect(comments[0].state).toBe("resolved");
    expect(comments[0].anchorText).toBe("alpha");
  });

  it("delete removes the directive but preserves the anchored text", () => {
    const md = insertComment("alpha beta", 0, 5, { id: "c1", author: "moh", ts: "t", note: "n" });
    const out = deleteComment(md, "c1");
    expect(out).toBe("alpha beta");
    expect(parseComments(out)).toHaveLength(0);
  });

  it("comments survive a round-trip through the text unchanged", () => {
    const md = insertComment("one two three", 4, 7, { id: "cX", author: "a", ts: "b", note: "c" });
    expect(parseComments(md)[0].id).toBe("cX");
  });
});
