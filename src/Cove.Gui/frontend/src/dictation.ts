export interface FocusDescriptor {
  tagName: string;
  inputType: string;
  readOnly: boolean;
  disabled: boolean;
  isContentEditable: boolean;
  className: string;
}

export type DictationRoute = "editable" | "nook" | "none";

const TextInputTypes = new Set(["", "text", "search", "url", "tel", "email", "password", "number"]);

export function classifyDictationTarget(target: FocusDescriptor, focusedNookId: string | null): DictationRoute {
  const isXtermHelper = target.className.includes("xterm-helper-textarea");
  if (!isXtermHelper && !target.readOnly && !target.disabled) {
    if (target.tagName === "TEXTAREA") return "editable";
    if (target.tagName === "INPUT" && TextInputTypes.has(target.inputType)) return "editable";
    if (target.isContentEditable) return "editable";
  }
  return focusedNookId ? "nook" : "none";
}

export type SpaceTarget = "editable" | "terminal" | "other";

export function classifySpaceTarget(target: FocusDescriptor): SpaceTarget {
  if (target.className.includes("xterm-helper-textarea")) return "terminal";
  return classifyDictationTarget(target, null) === "editable" ? "editable" : "other";
}

export type SpaceHoldState = "idle" | "armed" | "dictating";

export type SpaceHoldEvent =
  | { kind: "space-down"; repeat: boolean; modified: boolean; target: SpaceTarget }
  | { kind: "space-up" }
  | { kind: "other-key" }
  | { kind: "timeout" }
  | { kind: "blur" };

export type SpaceHoldAction = "none" | "swallow" | "arm" | "flush" | "start" | "stop" | "cancel";

export interface SpaceHoldResult {
  state: SpaceHoldState;
  action: SpaceHoldAction;
}

export function spaceHoldTransition(state: SpaceHoldState, event: SpaceHoldEvent): SpaceHoldResult {
  if (state === "idle") {
    if (event.kind === "space-down" && !event.repeat && !event.modified && event.target !== "other") {
      return { state: "armed", action: "arm" };
    }
    return { state: "idle", action: "none" };
  }
  if (state === "armed") {
    switch (event.kind) {
      case "space-down": return { state: "armed", action: "swallow" };
      case "space-up": return { state: "idle", action: "flush" };
      case "other-key": return { state: "idle", action: "flush" };
      case "timeout": return { state: "dictating", action: "start" };
      case "blur": return { state: "idle", action: "cancel" };
    }
  }
  switch (event.kind) {
    case "space-down": return { state: "dictating", action: "swallow" };
    case "space-up": return { state: "idle", action: "stop" };
    case "blur": return { state: "idle", action: "stop" };
    default: return { state: "dictating", action: "none" };
  }
}

export const SpaceHoldMs = 300;

export interface SpaceKeyEventLike {
  code: string;
  key: string;
  repeat: boolean;
  ctrlKey: boolean;
  metaKey: boolean;
  altKey: boolean;
  preventDefault(): void;
  stopImmediatePropagation(): void;
}

export interface SpaceHoldHooks {
  target: () => SpaceTarget;
  captureFocus: () => void;
  cancelPending: () => void;
  flush: () => void;
  begin: () => void;
  end: () => void;
  schedule: (fn: () => void, ms: number) => number;
  unschedule: (id: number) => void;
}

export interface SpaceHoldController {
  keydown(e: SpaceKeyEventLike): void;
  keyup(e: SpaceKeyEventLike): void;
  blur(): void;
}

export function createSpaceHold(hooks: SpaceHoldHooks): SpaceHoldController {
  let state: SpaceHoldState = "idle";
  let timer: number | null = null;

  const apply = (event: SpaceHoldEvent): SpaceHoldAction => {
    const next = spaceHoldTransition(state, event);
    state = next.state;
    if (state !== "armed" && timer !== null) {
      hooks.unschedule(timer);
      timer = null;
    }
    return next.action;
  };

  const consume = (e: SpaceKeyEventLike): void => {
    e.preventDefault();
    e.stopImmediatePropagation();
  };

  return {
    keydown(e) {
      if (e.code === "Space") {
        const action = apply({
          kind: "space-down",
          repeat: e.repeat,
          modified: e.ctrlKey || e.metaKey || e.altKey,
          target: hooks.target(),
        });
        if (action === "arm") {
          consume(e);
          hooks.captureFocus();
          timer = hooks.schedule(() => {
            timer = null;
            if (apply({ kind: "timeout" }) === "start") {
              hooks.cancelPending();
              hooks.begin();
            }
          }, SpaceHoldMs);
        } else if (action === "swallow") {
          consume(e);
        }
        return;
      }
      if (apply({ kind: "other-key" }) === "flush") hooks.flush();
    },
    keyup(e) {
      if (e.code !== "Space") return;
      const action = apply({ kind: "space-up" });
      if (action === "flush") {
        consume(e);
        hooks.flush();
      } else if (action === "stop") {
        consume(e);
        hooks.end();
      }
    },
    blur() {
      const action = apply({ kind: "blur" });
      if (action === "cancel") hooks.cancelPending();
      else if (action === "stop") hooks.end();
    },
  };
}

export function describeFocus(el: Element | null): FocusDescriptor {
  const input = el as HTMLInputElement | null;
  return {
    tagName: el?.tagName ?? "BODY",
    inputType: el?.tagName === "INPUT" ? (input?.type ?? "") : "",
    readOnly: input?.readOnly ?? false,
    disabled: input?.disabled ?? false,
    isContentEditable: (el as HTMLElement | null)?.isContentEditable ?? false,
    className: typeof (el as HTMLElement | null)?.className === "string" ? (el as HTMLElement).className : "",
  };
}

export function encodeNookText(text: string): string {
  const bytes = new TextEncoder().encode(text);
  let binary = "";
  for (const b of bytes) binary += String.fromCharCode(b);
  return btoa(binary);
}

export function insertIntoEditable(el: Element, text: string): void {
  if (el instanceof HTMLInputElement || el instanceof HTMLTextAreaElement) {
    const start = el.selectionStart ?? el.value.length;
    const end = el.selectionEnd ?? el.value.length;
    el.setRangeText(text, start, end, "end");
    el.dispatchEvent(new Event("input", { bubbles: true }));
    return;
  }
  const selection = window.getSelection();
  if (selection && selection.rangeCount > 0) {
    const range = selection.getRangeAt(0);
    range.deleteContents();
    range.insertNode(document.createTextNode(text));
    range.collapse(false);
    selection.removeAllRanges();
    selection.addRange(range);
    el.dispatchEvent(new Event("input", { bubbles: true }));
  }
}

export function partialPreview(text: string, max = 72): string {
  if (text.length <= max) return text;
  return `…${text.slice(text.length - max + 1)}`;
}

interface DictationDeps {
  invoke: (command: string, args?: Record<string, unknown>) => Promise<unknown>;
  getFocusedNookId: () => string | null;
  writeNook: (nookId: string, dataBase64: string) => Promise<void>;
  holdKey?: string;
}

export function typedRevision(prev: string, next: string): { erase: number; append: string } {
  const a = [...prev];
  const b = [...next];
  let common = 0;
  while (common < a.length && common < b.length && a[common] === b[common]) common++;
  return { erase: a.length - common, append: b.slice(common).join("") };
}

export const DICTATION_SPACE_KEY = "cove:dictation:space-hold";
export const DICTATION_LIVE_TYPING_KEY = "cove:dictation:live-typing";

export function dictationToggleEnabled(stored: string | null): boolean {
  return stored !== "false";
}

export type ModelPollOutcome = { kind: "pending" } | { kind: "ready" } | { kind: "failed"; error: string };

export function modelPollOutcome(modelReady: boolean | undefined, error: string | null): ModelPollOutcome {
  if (modelReady) return { kind: "ready" };
  if (error) return { kind: "failed", error };
  return { kind: "pending" };
}

export interface NookTypist {
  revise(next: string): Promise<void>;
}

export function createNookTypist(write: (payload: string) => Promise<void>): NookTypist {
  let lastTyped = "";
  return {
    async revise(next: string): Promise<void> {
      if (next === lastTyped) return;
      const { erase, append } = typedRevision(lastTyped, next);
      const payload = "\u007f".repeat(erase) + append;
      if (payload) await write(payload);
      lastTyped = next;
    },
  };
}

type PillState = "recording" | "transcribing" | "downloading" | "error";

export function setupDictation(deps: DictationDeps): () => void {
  const holdKey = deps.holdKey ?? "F9";
  let held = false;
  let pill: HTMLElement | null = null;

  const showPill = (state: PillState, detail = ""): void => {
    if (!pill) {
      pill = document.createElement("div");
      pill.className = "dictation-pill";
      document.body.appendChild(pill);
    }
    pill.dataset.state = state;
    pill.textContent = state === "recording" ? (detail ? `● ${detail}` : "● dictating")
      : state === "transcribing" ? "… transcribing"
      : state === "downloading" ? `⇣ speech model ${detail}`
      : `dictation: ${detail}`;
    pill.style.display = "block";
  };

  const hidePill = (): void => {
    if (pill) pill.style.display = "none";
  };

  const deliver = async (text: string, capturedFocus: Element | null, nookId: string | null): Promise<void> => {
    if (!text) return;
    const route = classifyDictationTarget(describeFocus(capturedFocus), nookId);
    if (route === "editable" && capturedFocus) {
      insertIntoEditable(capturedFocus, text);
    } else if (route === "nook" && nookId) {
      await deps.writeNook(nookId, encodeNookText(text));
    }
  };

  type LiveTarget =
    | { kind: "range"; el: HTMLInputElement | HTMLTextAreaElement; start: number; end: number }
    | { kind: "ce"; el: Element; node: Text | null; range: Range | null }
    | { kind: "nook"; typist: NookTypist }
    | null;

  let recordingTarget: LiveTarget = null;
  let writeChain: Promise<void> = Promise.resolve();

  const captureLiveTarget = (): LiveTarget => {
    if (!dictationToggleEnabled(localStorage.getItem(DICTATION_LIVE_TYPING_KEY))) return null;
    const el = document.activeElement;
    const nookId = deps.getFocusedNookId();
    const route = classifyDictationTarget(describeFocus(el), nookId);
    if (route === "editable" && el) {
      if (el instanceof HTMLInputElement || el instanceof HTMLTextAreaElement) {
        const start = el.selectionStart ?? el.value.length;
        return { kind: "range", el, start, end: el.selectionEnd ?? start };
      }
      const selection = window.getSelection();
      let range: Range | null = null;
      if (selection && selection.rangeCount > 0) {
        const live = selection.getRangeAt(0);
        if (el.contains(live.commonAncestorContainer)) range = live.cloneRange();
      }
      return { kind: "ce", el, node: null, range };
    }
    if (route === "nook" && nookId) {
      return {
        kind: "nook",
        typist: createNookTypist((payload) => deps.writeNook(nookId, encodeNookText(payload))),
      };
    }
    return null;
  };

  const typeRevision = async (target: LiveTarget, next: string): Promise<void> => {
    if (!target) return;
    if (target.kind === "nook") {
      await target.typist.revise(next);
    } else if (target.kind === "range") {
      target.el.setRangeText(next, target.start, target.end, "end");
      target.end = target.start + next.length;
      target.el.dispatchEvent(new Event("input", { bubbles: true }));
    } else {
      if (!target.node) {
        target.node = document.createTextNode(next);
        if (target.range) {
          target.range.deleteContents();
          target.range.insertNode(target.node);
        } else {
          target.el.appendChild(target.node);
        }
      } else {
        target.node.nodeValue = next;
      }
      const selection = window.getSelection();
      if (selection) {
        const caret = document.createRange();
        caret.setStart(target.node, next.length);
        caret.collapse(true);
        selection.removeAllRanges();
        selection.addRange(caret);
      }
      target.el.dispatchEvent(new Event("input", { bubbles: true }));
    }
  };

  const enqueueRevision = (target: LiveTarget, text: string): void => {
    writeChain = writeChain.then(() => typeRevision(target, text)).catch((err) => {
      console.warn("dictation live typing failed", err);
    });
  };

  const doStop = async (target: LiveTarget): Promise<void> => {
    const capturedFocus = document.activeElement;
    const nookId = deps.getFocusedNookId();
    showPill("transcribing");
    try {
      const raw = await deps.invoke("app.dictationStop");
      const result = JSON.parse(String(raw)) as { text?: string };
      const text = result.text ?? "";
      if (target) {
        enqueueRevision(target, text);
        await writeChain;
      } else {
        await deliver(text, capturedFocus, nookId);
      }
      hidePill();
    } catch (err) {
      showPill("error", String(err));
      setTimeout(hidePill, 3000);
    }
  };

  let currentHold: { pending: Promise<boolean>; target: LiveTarget } | null = null;

  const release = (): void => {
    if (!held) return;
    held = false;
    const hold = currentHold;
    currentHold = null;
    void (async () => {
      const started = hold ? await hold.pending : false;
      if (started && hold) {
        await doStop(hold.target);
      } else {
        hidePill();
      }
    })();
  };

  const beginHold = (): void => {
    if (held) return;
    held = true;
    const target = captureLiveTarget();
    recordingTarget = target;
    const pending = (async (): Promise<boolean> => {
      try {
        const raw = await deps.invoke("app.dictationStart");
        const result = JSON.parse(String(raw)) as { ok?: boolean; error?: string };
        if (result.ok) {
          if (held) showPill("recording");
          return true;
        }
        held = false;
        if (result.error?.includes("model")) {
          showPill("downloading", "starting…");
          await deps.invoke("app.dictationEnsureModel");
        } else {
          showPill("error", result.error ?? "failed");
          setTimeout(hidePill, 3000);
        }
        return false;
      } catch (err) {
        held = false;
        showPill("error", String(err));
        setTimeout(hidePill, 3000);
        return false;
      }
    })();
    currentHold = { pending, target };
  };

  let spaceFocus: Element | null = null;
  let spaceNook: string | null = null;

  const flushSpace = (): void => {
    const el = spaceFocus;
    const nookId = spaceNook;
    spaceFocus = null;
    spaceNook = null;
    if (!el || !el.isConnected) return;
    if (classifySpaceTarget(describeFocus(el)) === "editable") {
      insertIntoEditable(el, " ");
      return;
    }
    if (nookId) void deps.writeNook(nookId, encodeNookText(" "));
  };

  const spaceHold = createSpaceHold({
    target: () => dictationToggleEnabled(localStorage.getItem(DICTATION_SPACE_KEY))
      ? classifySpaceTarget(describeFocus(document.activeElement))
      : "other",
    captureFocus: () => {
      spaceFocus = document.activeElement;
      spaceNook = deps.getFocusedNookId();
    },
    cancelPending: () => {
      spaceFocus = null;
      spaceNook = null;
    },
    flush: flushSpace,
    begin: beginHold,
    end: release,
    schedule: (fn, ms) => window.setTimeout(fn, ms),
    unschedule: (id) => clearTimeout(id),
  });

  const onKeyDown = (e: KeyboardEvent): void => {
    spaceHold.keydown(e);
    if (e.code === "Space") return;
    if (e.key !== holdKey || e.repeat || held) return;
    e.preventDefault();
    beginHold();
  };

  const onKeyUp = (e: KeyboardEvent): void => {
    spaceHold.keyup(e);
    if (e.code === "Space") return;
    if (e.key !== holdKey) return;
    e.preventDefault();
    release();
  };

  const onBlur = (): void => {
    spaceHold.blur();
    release();
  };

  window.addEventListener("keydown", onKeyDown, true);
  window.addEventListener("keyup", onKeyUp, true);
  window.addEventListener("blur", onBlur);

  const onEngineEvent = (data: unknown): void => {
    const evt = data as { channel?: string; payload?: unknown };
    if (evt?.channel === "dictation.partial") {
      const text = (evt.payload as { text?: string } | undefined)?.text ?? "";
      if (held && text) {
        if (recordingTarget) enqueueRevision(recordingTarget, text);
        else showPill("recording", partialPreview(text));
      }
    } else if (evt?.channel === "dictation.progress") {
      const pct = Math.round((((evt.payload as { percent?: number } | undefined)?.percent ?? 0) * 100));
      showPill("downloading", `${pct}%`);
    } else if (evt?.channel === "dictation.model") {
      const payload = evt.payload as { ready?: boolean; error?: string } | undefined;
      if (payload?.ready) {
        showPill("downloading", "ready — hold F9 or space to dictate");
        setTimeout(hidePill, 2500);
      } else if (payload?.error) {
        showPill("error", payload.error);
        setTimeout(hidePill, 5000);
      }
    }
  };
  window.__ryn.on("engine.event", onEngineEvent);
  return () => {
    window.removeEventListener("keydown", onKeyDown, true);
    window.removeEventListener("keyup", onKeyUp, true);
    window.removeEventListener("blur", onBlur);
    window.__ryn.off("engine.event", onEngineEvent);
  };
}
