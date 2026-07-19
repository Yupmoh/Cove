import { describe, expect, it } from "vitest";
import { applyCanvasSourceDraft } from "./canvas-note";

describe("canvas source drafts", () => {
  it("preserves the last valid state until a valid draft arrives", () => {
    const current = {
      root: { elements: [{ id: "existing", type: "text.label", props: { text: "kept" } }] },
      state: { answer: 42 },
    };

    const malformed = applyCanvasSourceDraft(current, "{ invalid");
    expect(malformed).toBe(current);

    const replacement = applyCanvasSourceDraft(current, JSON.stringify({
      root: { elements: [] },
      state: { answer: 43 },
    }));
    expect(replacement).toEqual({ root: { elements: [] }, state: { answer: 43 } });
    expect(replacement).not.toBe(current);
  });
});
