import { describe, expect, it } from "vitest";
import {
  buildAutomationJs,
  buildClearEvalPayload,
  buildClickEvalPayload,
  buildFillEvalPayload,
  buildGetEvalPayload,
  buildIsEvalPayload,
  buildPressEvalPayload,
  buildScrollEvalPayload,
  buildSelectEvalPayload,
  buildSnapshotEvalPayload,
  buildTypeEvalPayload,
  buildWaitEvalPayload,
  clampWaitDeadline,
  collectSnapshot,
  findByText,
  findRef,
  GET_PROPS,
  isValidGetProp,
  isValidIsState,
  isValidRef,
  IS_STATES,
  normalizeScroll,
} from "./automation-snapshot";

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

describe("interaction payloads", () => {
  const attacks = ["\"", "`", "</script>", "\n", "\\", "');alert(1);('"];

  it("clear payload targets a validated ref and dispatches events", () => {
    const payload = buildClearEvalPayload("e3");
    expect(payload).toContain('[data-cove-ref="e3"]');
    expect(payload).toContain("input");
    expect(payload).toContain("change");
    expect(() => buildClearEvalPayload("e3\"]x")).toThrow(/invalid automation ref/);
  });

  it("type payload json-encodes text so injection strings cannot break out", () => {
    for (const attack of attacks) {
      const payload = buildTypeEvalPayload("e1", attack);
      expect(payload).toContain(JSON.stringify(attack));
      expect(payload).toContain('[data-cove-ref="e1"]');
    }
  });

  it("press payload json-encodes the key name", () => {
    for (const attack of attacks) {
      const payload = buildPressEvalPayload("e1", attack);
      expect(payload).toContain(JSON.stringify(attack));
      expect(payload).toContain("KeyboardEvent");
    }
    expect(buildPressEvalPayload("e1", "Enter")).toContain("keydown");
  });

  it("select payload json-encodes the option value and reports no match", () => {
    for (const attack of attacks) {
      const payload = buildSelectEvalPayload("e1", attack);
      expect(payload).toContain(JSON.stringify(attack));
    }
    expect(buildSelectEvalPayload("e1", "x")).toContain("no matching option");
  });

  it("scroll payload embeds numeric coordinates only", () => {
    const win = buildScrollEvalPayload(null, 0, 250);
    expect(win).toContain("250");
    expect(win).toContain("scrollTo");
    const elp = buildScrollEvalPayload("e2", 10, 20);
    expect(elp).toContain('[data-cove-ref="e2"]');
    expect(() => buildScrollEvalPayload("e2\"]x", 0, 0)).toThrow(/invalid automation ref/);
  });
});

describe("introspection payloads and helpers", () => {
  it("clamps wait deadlines under the bridge timeout", () => {
    expect(clampWaitDeadline(undefined)).toBe(2000);
    expect(clampWaitDeadline(500)).toBe(500);
    expect(clampWaitDeadline(50000)).toBe(8000);
    expect(clampWaitDeadline(-10)).toBe(0);
  });

  it("wait payload json-encodes the target text and stays under 8s", () => {
    for (const attack of ["</script>", "`", "\"", "\n"]) {
      const payload = buildWaitEvalPayload(null, attack, 3000);
      expect(payload).toContain(JSON.stringify(attack));
    }
    const withRef = buildWaitEvalPayload("e7", null, 90000);
    expect(withRef).toContain('data-cove-ref');
    expect(withRef).toContain("8000");
  });

  it("get whitelist rejects unknown props", () => {
    expect(GET_PROPS).toContain("value");
    expect(isValidGetProp("value")).toBe(true);
    expect(isValidGetProp("outerHTML")).toBe(false);
    expect(buildGetEvalPayload("e1", "value")).toContain('[data-cove-ref="e1"]');
    expect(() => buildGetEvalPayload("e1", "outerHTML")).toThrow(/unknown property/);
  });

  it("is whitelist rejects unknown states", () => {
    expect(IS_STATES).toContain("visible");
    expect(isValidIsState("editable")).toBe(true);
    expect(isValidIsState("frobbed")).toBe(false);
    expect(buildIsEvalPayload("e1", "checked")).toContain('[data-cove-ref="e1"]');
    expect(() => buildIsEvalPayload("e1", "frobbed")).toThrow(/unknown state/);
  });

  it("normalizeScroll defaults missing axes to zero", () => {
    expect(normalizeScroll(undefined, undefined)).toEqual({ x: 0, y: 0 });
    expect(normalizeScroll(null, 40)).toEqual({ x: 0, y: 40 });
    expect(normalizeScroll(15, null)).toEqual({ x: 15, y: 0 });
  });
});

describe("buildAutomationJs dispatch", () => {
  it("routes each new verb to its payload builder", () => {
    expect(buildAutomationJs({ requestId: "r", paneId: "p", kind: "clear", ref: "e1" })).toContain("data-cove-ref");
    expect(buildAutomationJs({ requestId: "r", paneId: "p", kind: "type", ref: "e1", value: "hi" })).toContain(JSON.stringify("hi"));
    expect(buildAutomationJs({ requestId: "r", paneId: "p", kind: "press", ref: "e1", value: "Enter" })).toContain("KeyboardEvent");
    expect(buildAutomationJs({ requestId: "r", paneId: "p", kind: "select", ref: "e1", value: "o" })).toContain("no matching option");
    expect(buildAutomationJs({ requestId: "r", paneId: "p", kind: "scroll", value: JSON.stringify({ x: 0, y: 12 }) })).toContain("12");
    expect(buildAutomationJs({ requestId: "r", paneId: "p", kind: "wait", ref: "e1", value: JSON.stringify({ timeoutMs: 1000 }) })).toContain("1000");
    expect(buildAutomationJs({ requestId: "r", paneId: "p", kind: "get", ref: "e1", value: "text" })).toContain("data-cove-ref");
    expect(buildAutomationJs({ requestId: "r", paneId: "p", kind: "is", ref: "e1", value: "visible" })).toContain("data-cove-ref");
  });

  it("throws on an unknown verb", () => {
    expect(() => buildAutomationJs({ requestId: "r", paneId: "p", kind: "teleport" })).toThrow(/unknown automation kind/);
  });
});
