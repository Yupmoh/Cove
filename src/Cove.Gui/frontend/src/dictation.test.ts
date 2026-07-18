import { describe, expect, it } from "vitest";
import { classifyDictationTarget, classifySpaceTarget, createDictationEventSubscriptions, createNookTypist, createSpaceHold, dictationToggleEnabled, DICTATION_LIVE_TYPING_KEY, DICTATION_SPACE_KEY, encodeNookText, modelPollOutcome, partialPreview, spaceHoldTransition, SpaceHoldMs, typedRevision, type FocusDescriptor, type SpaceHoldEvent, type SpaceHoldState, type SpaceHoldHooks, type SpaceKeyEventLike, type SpaceTarget } from "./dictation";

const focus = (partial: Partial<FocusDescriptor>): FocusDescriptor => ({
  tagName: "DIV",
  inputType: "",
  readOnly: false,
  disabled: false,
  isContentEditable: false,
  className: "",
  ...partial,
});

describe("classifyDictationTarget", () => {
  it("routes a focused text input to editable insertion", () => {
    expect(classifyDictationTarget(focus({ tagName: "INPUT", inputType: "text" }), "nook-1")).toBe("editable");
  });

  it("routes a textarea to editable insertion", () => {
    expect(classifyDictationTarget(focus({ tagName: "TEXTAREA" }), null)).toBe("editable");
  });

  it("routes contenteditable to editable insertion", () => {
    expect(classifyDictationTarget(focus({ isContentEditable: true }), null)).toBe("editable");
  });

  it("skips readonly and disabled inputs in favour of the focused nook", () => {
    expect(classifyDictationTarget(focus({ tagName: "INPUT", inputType: "text", readOnly: true }), "nook-1")).toBe("nook");
    expect(classifyDictationTarget(focus({ tagName: "INPUT", inputType: "text", disabled: true }), "nook-1")).toBe("nook");
  });

  it("routes checkbox and button inputs to the nook, not insertion", () => {
    expect(classifyDictationTarget(focus({ tagName: "INPUT", inputType: "checkbox" }), "nook-1")).toBe("nook");
    expect(classifyDictationTarget(focus({ tagName: "INPUT", inputType: "button" }), "nook-1")).toBe("nook");
  });

  it("falls back to the focused nook for body focus", () => {
    expect(classifyDictationTarget(focus({ tagName: "BODY" }), "nook-9")).toBe("nook");
  });

  it("returns none when nothing is editable and no nook focused", () => {
    expect(classifyDictationTarget(focus({ tagName: "BODY" }), null)).toBe("none");
  });

  it("treats xterm helper textarea as terminal, not editable", () => {
    expect(classifyDictationTarget(focus({ tagName: "TEXTAREA", className: "xterm-helper-textarea" }), "nook-3")).toBe("nook");
  });
});

describe("encodeNookText", () => {
  it("base64-encodes utf-8 text", () => {
    expect(atob(encodeNookText("hi"))).toBe("hi");
  });

  it("survives non-ascii", () => {
    const encoded = encodeNookText("héllo wörld");
    expect(new TextDecoder().decode(Uint8Array.from(atob(encoded), c => c.charCodeAt(0)))).toBe("héllo wörld");
  });
});

describe("classifySpaceTarget", () => {
  it("treats the xterm helper textarea as terminal", () => {
    expect(classifySpaceTarget(focus({ tagName: "TEXTAREA", className: "xterm-helper-textarea" }))).toBe("terminal");
  });

  it("treats writable inputs as editable", () => {
    expect(classifySpaceTarget(focus({ tagName: "INPUT", inputType: "text" }))).toBe("editable");
    expect(classifySpaceTarget(focus({ tagName: "TEXTAREA" }))).toBe("editable");
    expect(classifySpaceTarget(focus({ isContentEditable: true }))).toBe("editable");
  });

  it("treats buttons, checkboxes, and body focus as other", () => {
    expect(classifySpaceTarget(focus({ tagName: "INPUT", inputType: "button" }))).toBe("other");
    expect(classifySpaceTarget(focus({ tagName: "INPUT", inputType: "checkbox" }))).toBe("other");
    expect(classifySpaceTarget(focus({ tagName: "BODY" }))).toBe("other");
  });
});

describe("spaceHoldTransition", () => {
  const down = (over: Partial<Extract<SpaceHoldEvent, { kind: "space-down" }>> = {}): SpaceHoldEvent =>
    ({ kind: "space-down", repeat: false, modified: false, target: "terminal", ...over });
  const step = (state: SpaceHoldState, event: SpaceHoldEvent) => spaceHoldTransition(state, event);

  it("arms on a fresh unmodified space press over a typing target", () => {
    expect(step("idle", down())).toEqual({ state: "armed", action: "arm" });
    expect(step("idle", down({ target: "editable" }))).toEqual({ state: "armed", action: "arm" });
  });

  it("passes through modified, repeated, or non-typing-target presses", () => {
    expect(step("idle", down({ modified: true }))).toEqual({ state: "idle", action: "none" });
    expect(step("idle", down({ repeat: true }))).toEqual({ state: "idle", action: "none" });
    expect(step("idle", down({ target: "other" }))).toEqual({ state: "idle", action: "none" });
  });

  it("flushes a plain space on a quick tap", () => {
    expect(step("armed", { kind: "space-up" })).toEqual({ state: "idle", action: "flush" });
  });

  it("cancels and flushes when another key rolls over the held space", () => {
    expect(step("armed", { kind: "other-key" })).toEqual({ state: "idle", action: "flush" });
  });

  it("starts dictation when the hold timer fires", () => {
    expect(step("armed", { kind: "timeout" })).toEqual({ state: "dictating", action: "start" });
  });

  it("swallows key repeats while armed or dictating", () => {
    expect(step("armed", down({ repeat: true }))).toEqual({ state: "armed", action: "swallow" });
    expect(step("dictating", down({ repeat: true }))).toEqual({ state: "dictating", action: "swallow" });
  });

  it("stops dictation on release or window blur", () => {
    expect(step("dictating", { kind: "space-up" })).toEqual({ state: "idle", action: "stop" });
    expect(step("dictating", { kind: "blur" })).toEqual({ state: "idle", action: "stop" });
  });

  it("cancels silently when the window blurs while armed", () => {
    expect(step("armed", { kind: "blur" })).toEqual({ state: "idle", action: "cancel" });
  });

  it("lets other keys pass while dictating and ignores stray events when idle", () => {
    expect(step("dictating", { kind: "other-key" })).toEqual({ state: "dictating", action: "none" });
    expect(step("idle", { kind: "space-up" })).toEqual({ state: "idle", action: "none" });
    expect(step("idle", { kind: "timeout" })).toEqual({ state: "idle", action: "none" });
    expect(step("idle", { kind: "blur" })).toEqual({ state: "idle", action: "none" });
  });
});

interface FakeKeyEvent extends SpaceKeyEventLike {
  prevented: boolean;
  stopped: boolean;
}

function fakeKey(over: Partial<SpaceKeyEventLike> = {}): FakeKeyEvent {
  const e: FakeKeyEvent = {
    code: "Space",
    key: " ",
    repeat: false,
    ctrlKey: false,
    metaKey: false,
    altKey: false,
    prevented: false,
    stopped: false,
    preventDefault() { e.prevented = true; },
    stopImmediatePropagation() { e.stopped = true; },
    ...over,
  };
  return e;
}

function harness(target: SpaceTarget = "terminal") {
  const calls: string[] = [];
  let pendingTimer: (() => void) | null = null;
  let scheduledMs = -1;
  const hooks: SpaceHoldHooks = {
    target: () => target,
    captureFocus: () => { calls.push("capture"); },
    cancelPending: () => { calls.push("cancel"); },
    flush: () => { calls.push("flush"); },
    begin: () => { calls.push("begin"); },
    end: () => { calls.push("end"); },
    schedule: (fn, ms) => { pendingTimer = fn; scheduledMs = ms; return 1; },
    unschedule: () => { pendingTimer = null; },
  };
  return {
    controller: createSpaceHold(hooks),
    calls,
    fireTimer: () => { const fn = pendingTimer; pendingTimer = null; fn?.(); },
    timerArmed: () => pendingTimer !== null,
    scheduledMs: () => scheduledMs,
  };
}

describe("createSpaceHold", () => {
  it("consumes the arming space so xterm never receives the real keydown", () => {
    const h = harness();
    const e = fakeKey();
    h.controller.keydown(e);
    expect(e.prevented).toBe(true);
    expect(e.stopped).toBe(true);
    expect(h.calls).toEqual(["capture"]);
    expect(h.scheduledMs()).toBe(SpaceHoldMs);
  });

  it("flushes exactly one space on a quick tap and consumes the keyup", () => {
    const h = harness();
    h.controller.keydown(fakeKey());
    const up = fakeKey();
    h.controller.keyup(up);
    expect(up.prevented).toBe(true);
    expect(up.stopped).toBe(true);
    expect(h.calls).toEqual(["capture", "flush"]);
    expect(h.timerArmed()).toBe(false);
  });

  it("flushes the pending space when another key rolls over, leaving that key untouched", () => {
    const h = harness();
    h.controller.keydown(fakeKey());
    const other = fakeKey({ code: "KeyA", key: "a" });
    h.controller.keydown(other);
    expect(other.prevented).toBe(false);
    expect(other.stopped).toBe(false);
    expect(h.calls).toEqual(["capture", "flush"]);
  });

  it("starts dictation after the hold threshold and stops on release without flushing", () => {
    const h = harness();
    h.controller.keydown(fakeKey());
    h.fireTimer();
    const up = fakeKey();
    h.controller.keyup(up);
    expect(up.prevented).toBe(true);
    expect(up.stopped).toBe(true);
    expect(h.calls).toEqual(["capture", "cancel", "begin", "end"]);
  });

  it("swallows auto-repeat spaces while armed and while dictating", () => {
    const h = harness();
    h.controller.keydown(fakeKey());
    const armedRepeat = fakeKey({ repeat: true });
    h.controller.keydown(armedRepeat);
    expect(armedRepeat.stopped).toBe(true);
    h.fireTimer();
    const dictatingRepeat = fakeKey({ repeat: true });
    h.controller.keydown(dictatingRepeat);
    expect(dictatingRepeat.stopped).toBe(true);
    expect(h.calls).toEqual(["capture", "cancel", "begin"]);
  });

  it("passes modified space and non-typing targets through untouched", () => {
    const modified = fakeKey({ metaKey: true });
    const h = harness();
    h.controller.keydown(modified);
    expect(modified.prevented).toBe(false);
    expect(modified.stopped).toBe(false);
    const other = harness("other");
    const plain = fakeKey();
    other.controller.keydown(plain);
    expect(plain.prevented).toBe(false);
    expect(other.calls).toEqual([]);
  });

  it("cancels silently on blur while armed and stops on blur while dictating", () => {
    const armed = harness();
    armed.controller.keydown(fakeKey());
    armed.controller.blur();
    expect(armed.calls).toEqual(["capture", "cancel"]);
    const dictating = harness();
    dictating.controller.keydown(fakeKey());
    dictating.fireTimer();
    dictating.controller.blur();
    expect(dictating.calls).toEqual(["capture", "cancel", "begin", "end"]);
  });
});

describe("partialPreview", () => {
  it("returns short text unchanged", () => {
    expect(partialPreview("hello world")).toBe("hello world");
  });

  it("keeps the trailing portion of long text with a leading ellipsis", () => {
    const text = "a".repeat(50) + " the quick brown fox jumps over the lazy dog";
    const preview = partialPreview(text, 20);
    expect(preview.length).toBe(20);
    expect(preview.startsWith("…")).toBe(true);
    expect(preview.endsWith("lazy dog")).toBe(true);
  });

  it("returns empty for empty text", () => {
    expect(partialPreview("")).toBe("");
  });
});

describe("createDictationEventSubscriptions", () => {
  it("registers every typed channel and disposes every registration once", async () => {
    const channels: string[] = [];
    const disposed: string[] = [];
    const subscriptions = createDictationEventSubscriptions(
      (channel) => {
        channels.push(channel);
        return { dispose: () => { disposed.push(channel); } };
      },
      {
        partial: () => {},
        progress: () => {},
        model: () => {},
      },
    );

    expect(channels).toEqual(["dictation.partial", "dictation.progress", "dictation.model"]);
    await subscriptions.dispose();
    await subscriptions.dispose();
    expect(disposed).toEqual(["dictation.model", "dictation.progress", "dictation.partial"]);
  });
});

describe("typedRevision", () => {
  it("appends when the new text extends the old", () => {
    expect(typedRevision("hello", "hello world")).toEqual({ erase: 0, append: " world" });
  });

  it("erases only the changed tail on revision", () => {
    expect(typedRevision("hello word", "hello world")).toEqual({ erase: 1, append: "ld" });
  });

  it("does nothing for identical text", () => {
    expect(typedRevision("same", "same")).toEqual({ erase: 0, append: "" });
  });

  it("erases everything when the new text is empty", () => {
    expect(typedRevision("gone", "")).toEqual({ erase: 4, append: "" });
  });

  it("counts astral characters as single erasures", () => {
    expect(typedRevision("a🎤", "a")).toEqual({ erase: 1, append: "" });
    expect(typedRevision("", "🎤 test")).toEqual({ erase: 0, append: "🎤 test" });
  });

  it("types everything from scratch", () => {
    expect(typedRevision("", "hello")).toEqual({ erase: 0, append: "hello" });
  });
});

describe("createNookTypist", () => {
  function typist() {
    const writes: string[] = [];
    return { writes, t: createNookTypist(async (p) => { writes.push(p); }) };
  }

  it("types incrementally as partials grow", async () => {
    const { writes, t } = typist();
    await t.revise("hello");
    await t.revise("hello world");
    expect(writes).toEqual(["hello", " world"]);
  });

  it("sends DEL for the changed tail then the correction in one payload", async () => {
    const { writes, t } = typist();
    await t.revise("hello word");
    await t.revise("hello world");
    expect(writes).toEqual(["hello word", "\u007fld"]);
  });

  it("writes nothing for identical revisions", async () => {
    const { writes, t } = typist();
    await t.revise("same");
    await t.revise("same");
    expect(writes).toEqual(["same"]);
  });

  it("erases the whole preview when the final text is empty", async () => {
    const { writes, t } = typist();
    await t.revise("oops");
    await t.revise("");
    expect(writes).toEqual(["oops", "\u007f\u007f\u007f\u007f"]);
  });

  it("keeps sessions isolated: an old session's final diffs against its own preview", async () => {
    const a = typist();
    const b = typist();
    await a.t.revise("hello");
    await b.t.revise("hi");
    await a.t.revise("hello world");
    expect(a.writes).toEqual(["hello", " world"]);
    expect(b.writes).toEqual(["hi"]);
  });
});

describe("dictation preferences", () => {
  it("defaults to enabled when nothing is stored", () => {
    expect(dictationToggleEnabled(null)).toBe(true);
    expect(dictationToggleEnabled("")).toBe(true);
    expect(dictationToggleEnabled("true")).toBe(true);
  });

  it("disables only on an explicit false", () => {
    expect(dictationToggleEnabled("false")).toBe(false);
    expect(dictationToggleEnabled("garbage")).toBe(true);
  });

  it("uses stable storage keys", () => {
    expect(DICTATION_SPACE_KEY).toBe("cove:dictation:space-hold");
    expect(DICTATION_LIVE_TYPING_KEY).toBe("cove:dictation:live-typing");
  });
});

describe("modelPollOutcome", () => {
  it("stays pending while downloading with no error", () => {
    expect(modelPollOutcome(false, null)).toEqual({ kind: "pending" });
    expect(modelPollOutcome(undefined, null)).toEqual({ kind: "pending" });
  });

  it("fails with the reported error so the poll terminates and retry appears", () => {
    expect(modelPollOutcome(false, "checksum mismatch")).toEqual({ kind: "failed", error: "checksum mismatch" });
  });

  it("a ready model trumps a stale error", () => {
    expect(modelPollOutcome(true, "old failure")).toEqual({ kind: "ready" });
    expect(modelPollOutcome(true, null)).toEqual({ kind: "ready" });
  });
});
