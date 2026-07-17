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

interface DictationDeps {
  invoke: (command: string, args?: Record<string, unknown>) => Promise<unknown>;
  getFocusedNookId: () => string | null;
  holdKey?: string;
}

type PillState = "recording" | "transcribing" | "downloading" | "error";

export function setupDictation(deps: DictationDeps): void {
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
    pill.textContent = state === "recording" ? "● dictating"
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
      await deps.invoke("app.nookWrite", { nookId, dataBase64: encodeNookText(text) });
    }
  };

  const doStop = async (): Promise<void> => {
    const capturedFocus = document.activeElement;
    const nookId = deps.getFocusedNookId();
    showPill("transcribing");
    try {
      const raw = await deps.invoke("app.dictationStop");
      const result = JSON.parse(String(raw)) as { text?: string };
      await deliver(result.text ?? "", capturedFocus, nookId);
      hidePill();
    } catch (err) {
      showPill("error", String(err));
      setTimeout(hidePill, 3000);
    }
  };

  let startPending: Promise<boolean> | null = null;

  const release = (): void => {
    if (!held) return;
    held = false;
    const pending = startPending;
    startPending = null;
    void (async () => {
      const started = pending ? await pending : false;
      if (started) {
        await doStop();
      } else {
        hidePill();
      }
    })();
  };

  const beginHold = (): void => {
    if (held) return;
    held = true;
    startPending = (async (): Promise<boolean> => {
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
  };

  let spaceFocus: Element | null = null;
  let synthesizing = false;

  const flushSpace = (): void => {
    const el = spaceFocus;
    spaceFocus = null;
    if (!el || !el.isConnected) return;
    if (classifySpaceTarget(describeFocus(el)) === "editable") {
      insertIntoEditable(el, " ");
      return;
    }
    const evt = new KeyboardEvent("keydown", { key: " ", code: "Space", bubbles: true, cancelable: true });
    Object.defineProperty(evt, "keyCode", { value: 32 });
    synthesizing = true;
    try {
      el.dispatchEvent(evt);
    } finally {
      synthesizing = false;
    }
  };

  const spaceHold = createSpaceHold({
    target: () => classifySpaceTarget(describeFocus(document.activeElement)),
    captureFocus: () => { spaceFocus = document.activeElement; },
    cancelPending: () => { spaceFocus = null; },
    flush: flushSpace,
    begin: beginHold,
    end: release,
    schedule: (fn, ms) => window.setTimeout(fn, ms),
    unschedule: (id) => clearTimeout(id),
  });

  window.addEventListener("keydown", (e) => {
    if (synthesizing) return;
    spaceHold.keydown(e);
    if (e.code === "Space") return;
    if (e.key !== holdKey || e.repeat || held) return;
    e.preventDefault();
    beginHold();
  }, true);

  window.addEventListener("keyup", (e) => {
    if (synthesizing) return;
    spaceHold.keyup(e);
    if (e.code === "Space") return;
    if (e.key !== holdKey) return;
    e.preventDefault();
    release();
  }, true);

  window.addEventListener("blur", () => {
    spaceHold.blur();
    release();
  });

  window.__ryn.on("engine.event", (data: unknown) => {
    const evt = data as { channel?: string; payload?: unknown };
    if (evt?.channel === "dictation.progress") {
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
  });
}
