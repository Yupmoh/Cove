export const COMMENT_DIRECTIVE_FORM =
  ':comment[anchored text]{#id author="name" ts="iso8601" note="comment body" state=resolved}';

export type CommentState = "open" | "resolved";

export interface CommentMeta {
  id: string;
  author: string;
  ts: string;
  note: string;
  state?: CommentState;
}

export interface CommentEntry extends CommentMeta {
  state: CommentState;
  anchorText: string;
  start: number;
  end: number;
}

const DIRECTIVE = /:comment\[([^\]]*)\]\{([^}]*)\}/g;

export function parseComments(md: string): CommentEntry[] {
  const out: CommentEntry[] = [];
  DIRECTIVE.lastIndex = 0;
  let m: RegExpExecArray | null;
  while ((m = DIRECTIVE.exec(md)) !== null) {
    const anchorText = m[1];
    const attrs = m[2];
    const state = attrValue(attrs, "state") === "resolved" ? "resolved" : "open";
    out.push({
      id: idValue(attrs),
      author: attrValue(attrs, "author"),
      ts: attrValue(attrs, "ts"),
      note: attrValue(attrs, "note"),
      state,
      anchorText,
      start: m.index,
      end: m.index + m[0].length,
    });
  }
  return out;
}

export function insertComment(md: string, selStart: number, selEnd: number, meta: CommentMeta): string {
  const anchor = md.slice(selStart, selEnd);
  const directive = serialize(anchor, { ...meta, state: meta.state ?? "open" });
  return md.slice(0, selStart) + directive + md.slice(selEnd);
}

export function resolveComment(md: string, id: string): string {
  return rewrite(md, id, (e) => serialize(e.anchorText, { ...e, state: "resolved" }));
}

export function deleteComment(md: string, id: string): string {
  return rewrite(md, id, (e) => e.anchorText);
}

function rewrite(md: string, id: string, replace: (e: CommentEntry) => string): string {
  const target = parseComments(md).find((c) => c.id === id);
  if (!target) return md;
  return md.slice(0, target.start) + replace(target) + md.slice(target.end);
}

function serialize(anchor: string, meta: CommentMeta & { state: CommentState }): string {
  const base = `:comment[${anchor}]{#${meta.id} author="${meta.author}" ts="${meta.ts}" note="${meta.note}"`;
  return base + (meta.state === "resolved" ? " state=resolved}" : "}");
}

function idValue(attrs: string): string {
  const m = attrs.match(/#(\S+)/);
  return m ? m[1] : "";
}

function attrValue(attrs: string, key: string): string {
  const quoted = attrs.match(new RegExp(`${key}="([^"]*)"`));
  if (quoted) return quoted[1];
  const bare = attrs.match(new RegExp(`${key}=(\\S+)`));
  return bare ? bare[1] : "";
}
