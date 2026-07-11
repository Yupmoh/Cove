import { describe, it, expect } from "vitest";
import { cssPath, buildFeedbackReport, feedbackSlug, harnessPrompt, type InspectElementLike } from "./inspect-mode";

function fakeEl(tag: string, opts: { id?: string; classes?: string[]; parent?: InspectElementLike | null } = {}): InspectElementLike {
  return { tagName: tag.toUpperCase(), id: opts.id ?? "", classList: opts.classes ?? [], parentElement: opts.parent ?? null };
}

describe("cssPath", () => {
  it("stops at ids and joins ancestors", () => {
    const root = fakeEl("div", { id: "grid" });
    const mid = fakeEl("div", { classes: ["box-launcher"], parent: root });
    const leaf = fakeEl("span", { classes: ["cl-card-name", "extra", "third"], parent: mid });
    expect(cssPath(leaf)).toBe("div#grid > div.box-launcher > span.cl-card-name.extra");
  });

  it("skips body and html", () => {
    const body = fakeEl("body");
    const leaf = fakeEl("div", { classes: ["nook"], parent: body });
    expect(cssPath(leaf)).toBe("div.nook");
  });

  it("handles null", () => {
    expect(cssPath(null)).toBe("unknown");
  });
});

describe("buildFeedbackReport", () => {
  it("shapes and truncates the report", () => {
    const r = buildFeedbackReport({
      note: "button misaligned",
      target: { selector: "div.x", tag: "div", classes: ["x"], rect: { x: 1, y: 2, width: 3, height: 4 }, textExcerpt: "hi" },
      regionRect: null,
      bay: "Cove",
      shore: "main",
      appVersion: "0.1.0",
      htmlExcerpt: "y".repeat(9000),
      nowIso: "2026-07-10T00:00:00Z",
    });
    expect(r.kind).toBe("cove-ui-feedback");
    expect(r.htmlExcerpt.length).toBe(4000);
    expect(r.target?.selector).toBe("div.x");
  });
});

describe("feedbackSlug", () => {
  it("slugs the first words of the note", () => {
    expect(feedbackSlug("The Gear icon looks wrong & broken")).toBe("the-gear-icon-looks");
    expect(feedbackSlug("   ")).toBe("ui-feedback");
  });
});

describe("harnessPrompt", () => {
  it("mentions note, selector, and report path", () => {
    const r = buildFeedbackReport({ note: "misaligned", target: { selector: "div.nook", tag: "div", classes: [], rect: { x: 0, y: 0, width: 1, height: 1 }, textExcerpt: "" }, regionRect: null, bay: "w", shore: "r", appVersion: "v", htmlExcerpt: "", nowIso: "t" });
    const p = harnessPrompt(r, "/tmp/fb.json");
    expect(p).toContain("misaligned");
    expect(p).toContain("div.nook");
    expect(p).toContain("/tmp/fb.json");
  });
});
