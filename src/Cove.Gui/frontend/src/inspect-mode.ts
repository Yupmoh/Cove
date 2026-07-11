export interface InspectRect {
  x: number;
  y: number;
  width: number;
  height: number;
}

export interface InspectTarget {
  selector: string;
  tag: string;
  classes: string[];
  rect: InspectRect;
  textExcerpt: string;
}

export interface InspectElementLike {
  tagName: string;
  id: string;
  classList: Iterable<string>;
  parentElement: InspectElementLike | null;
}

export function cssPath(el: InspectElementLike | null, maxDepth = 6): string {
  const parts: string[] = [];
  let node = el;
  let depth = 0;
  while (node && depth < maxDepth) {
    const tag = node.tagName.toLowerCase();
    if (tag === "html" || tag === "body") break;
    if (node.id) {
      parts.unshift(`${tag}#${node.id}`);
      break;
    }
    const classes = [...node.classList].slice(0, 2);
    parts.unshift(classes.length > 0 ? `${tag}.${classes.join(".")}` : tag);
    node = node.parentElement;
    depth += 1;
  }
  return parts.length > 0 ? parts.join(" > ") : "unknown";
}

export interface FeedbackReport {
  kind: "cove-ui-feedback";
  createdAt: string;
  note: string;
  target: InspectTarget | null;
  regionRect: InspectRect | null;
  bay: string;
  shore: string;
  appVersion: string;
  htmlExcerpt: string;
}

export function buildFeedbackReport(input: {
  note: string;
  target: InspectTarget | null;
  regionRect: InspectRect | null;
  bay: string;
  shore: string;
  appVersion: string;
  htmlExcerpt: string;
  nowIso: string;
}): FeedbackReport {
  return {
    kind: "cove-ui-feedback",
    createdAt: input.nowIso,
    note: input.note,
    target: input.target,
    regionRect: input.regionRect,
    bay: input.bay,
    shore: input.shore,
    appVersion: input.appVersion,
    htmlExcerpt: input.htmlExcerpt.slice(0, 4000),
  };
}

export function feedbackSlug(note: string): string {
  const words = note.toLowerCase().replace(/[^a-z0-9\s]/g, "").split(/\s+/).filter((w) => w.length > 0).slice(0, 4);
  return words.length > 0 ? words.join("-") : "ui-feedback";
}

export function harnessPrompt(report: FeedbackReport, reportPath: string): string {
  const where = report.target ? `UI element: ${report.target.selector}.` : "See the region rect in the report.";
  return [
    "You are working on Cove, the terminal bay app this session is running inside.",
    `The owner flagged a UI bug from Cove's inspect mode: ${report.note}`,
    where,
    `The full structured report (selector, rect, html excerpt, context) is at: ${reportPath}`,
    "Read the report, find the responsible code in the Cove repo, and fix the bug.",
  ].join(" ");
}
