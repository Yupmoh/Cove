import { describe, expect, it } from "vitest";
import { buildClickEvalPayload, buildFillEvalPayload, buildSnapshotEvalPayload, collectSnapshot, findByText, findRef, isValidRef } from "./automation-snapshot";

interface FakeElement {
  tagName: string;
  children: FakeElement[];
  attrs: Record<string, string>;
  textContent: string | null;
  offsetWidth: number;
  offsetHeight: number;
  value?: string;
  disabled?: boolean;
  href?: string;
  getAttribute(name: string): string | null;
  setAttribute(name: string, value: string): void;
}

function el(tag: string, attrs: Record<string, string> = {}, children: FakeElement[] = [], text = "", size = 10): FakeElement {
  const node: FakeElement = {
    tagName: tag.toUpperCase(),
    children,
    attrs: { ...attrs },
    textContent: text || children.map((c) => c.textContent ?? "").join(" "),
    offsetWidth: size,
    offsetHeight: size,
    getAttribute(name: string) { return this.attrs[name] ?? null; },
    setAttribute(name: string, value: string) { this.attrs[name] = value; },
  };
  return node;
}

describe("collectSnapshot", () => {
  it("assigns sequential refs to interactive elements in document order", () => {
    const root = el("div", {}, [
      el("button", {}, [], "Save"),
      el("a", { href: "/docs" }, [], "Docs"),
      el("input", { type: "text", placeholder: "Search" }),
    ]);

    const entries = collectSnapshot(root);

    expect(entries.map((e) => e.ref)).toEqual(["e1", "e2", "e3"]);
    expect(entries[0]).toMatchObject({ role: "button", name: "Save", tag: "button" });
    expect(entries[1]).toMatchObject({ role: "link", name: "Docs" });
    expect(entries[2]).toMatchObject({ role: "textbox", name: "Search" });
  });

  it("tags matched elements with data-cove-ref for later targeting", () => {
    const button = el("button", {}, [], "Go");
    const root = el("div", {}, [button]);

    collectSnapshot(root);

    expect(button.attrs["data-cove-ref"]).toBe("e1");
  });

  it("prefers aria-label over text content for names", () => {
    const root = el("div", {}, [el("button", { "aria-label": "Close dialog" }, [], "X")]);

    const entries = collectSnapshot(root);

    expect(entries[0].name).toBe("Close dialog");
  });

  it("skips invisible elements and their subtrees", () => {
    const hidden = el("div", {}, [el("button", {}, [], "Hidden")], "", 0);
    const root = el("div", {}, [hidden, el("button", {}, [], "Visible")]);

    const entries = collectSnapshot(root);

    expect(entries).toHaveLength(1);
    expect(entries[0].name).toBe("Visible");
  });

  it("skips hidden inputs and unnamed headings", () => {
    const root = el("div", {}, [
      el("input", { type: "hidden" }),
      el("h2", {}, [], ""),
      el("h2", {}, [], "Section"),
    ]);

    const entries = collectSnapshot(root);

    expect(entries).toHaveLength(1);
    expect(entries[0]).toMatchObject({ role: "heading", name: "Section" });
  });

  it("maps input types to roles and captures value plus disabled state", () => {
    const box = el("input", { type: "checkbox", "aria-label": "Agree", disabled: "" });
    box.disabled = true;
    const range = el("input", { type: "range", "aria-label": "Volume" });
    range.value = "70";
    const root = el("div", {}, [box, range]);

    const entries = collectSnapshot(root);

    expect(entries[0]).toMatchObject({ role: "checkbox", disabled: true });
    expect(entries[1]).toMatchObject({ role: "slider", value: "70" });
  });

  it("honors explicit role attributes", () => {
    const root = el("div", {}, [el("div", { role: "tab", "aria-label": "Settings" })]);

    const entries = collectSnapshot(root);

    expect(entries[0].role).toBe("tab");
  });

  it("truncates very long text names to 80 chars", () => {
    const root = el("div", {}, [el("button", {}, [], "x".repeat(200))]);

    const entries = collectSnapshot(root);

    expect(entries[0].name).toHaveLength(80);
  });
});

describe("findByText and findRef", () => {
  it("filters case-insensitively and resolves refs", () => {
    const root = el("div", {}, [
      el("button", {}, [], "Save file"),
      el("button", {}, [], "Discard"),
    ]);
    const entries = collectSnapshot(root);

    expect(findByText(entries, "SAVE")).toHaveLength(1);
    expect(findRef(entries, "e2")?.name).toBe("Discard");
    expect(findRef(entries, "e9")).toBeNull();
  });
});

describe("eval payloads", () => {
  it("snapshot payload is self-contained and stringifies the walker", () => {
    const payload = buildSnapshotEvalPayload();

    expect(payload).toContain("location.href");
    expect(payload).toContain("document.body");
    expect(payload).not.toContain("import ");
    expect(payload).not.toContain("require(");
  });

  it("click and fill payloads embed only validated refs", () => {
    expect(buildClickEvalPayload("e5")).toContain('[data-cove-ref="e5"]');
    expect(buildFillEvalPayload("e2", "hi \"there\"")).toContain("\"hi \\\"there\\\"\"");
    expect(() => buildClickEvalPayload("e5\"] , body [x=\"")).toThrow(/invalid automation ref/);
    expect(isValidRef("e12")).toBe(true);
    expect(isValidRef("12")).toBe(false);
  });
});
