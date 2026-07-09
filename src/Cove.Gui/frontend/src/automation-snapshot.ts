export interface SnapshotEntry {
  ref: string;
  role: string;
  name: string;
  tag: string;
  value: string | null;
  href: string | null;
  disabled: boolean;
}

export interface SnapshotResult {
  url: string;
  title: string;
  entries: SnapshotEntry[];
}

interface MinimalElement {
  tagName: string;
  children: ArrayLike<MinimalElement>;
  getAttribute(name: string): string | null;
  textContent: string | null;
  offsetWidth?: number;
  offsetHeight?: number;
  value?: string;
  disabled?: boolean;
  href?: string;
  setAttribute?(name: string, value: string): void;
}

export function collectSnapshot(root: MinimalElement): SnapshotEntry[] {
  const interactiveTags: Record<string, string> = {
    A: "link", BUTTON: "button", INPUT: "textbox", TEXTAREA: "textbox",
    SELECT: "combobox", OPTION: "option", SUMMARY: "button", LABEL: "text",
    H1: "heading", H2: "heading", H3: "heading", H4: "heading", H5: "heading", H6: "heading",
  };
  const inputRoles: Record<string, string> = {
    button: "button", submit: "button", reset: "button", checkbox: "checkbox",
    radio: "radio", range: "slider", file: "button", search: "searchbox",
  };
  const entries: SnapshotEntry[] = [];
  let counter = 0;

  const visible = (el: MinimalElement): boolean => {
    if (typeof el.offsetWidth === "number" && typeof el.offsetHeight === "number")
      return el.offsetWidth > 0 || el.offsetHeight > 0;
    return true;
  };

  const accessibleName = (el: MinimalElement): string => {
    const aria = el.getAttribute("aria-label");
    if (aria) return aria.trim();
    const placeholder = el.getAttribute("placeholder");
    if (placeholder) return placeholder.trim();
    const title = el.getAttribute("title");
    if (title) return title.trim();
    const alt = el.getAttribute("alt");
    if (alt) return alt.trim();
    const text = (el.textContent ?? "").trim().replace(/\s+/g, " ");
    return text.length > 80 ? text.slice(0, 80) : text;
  };

  const roleOf = (el: MinimalElement): string | null => {
    const explicit = el.getAttribute("role");
    if (explicit) return explicit;
    const tag = el.tagName.toUpperCase();
    if (tag === "INPUT") {
      const type = (el.getAttribute("type") ?? "text").toLowerCase();
      if (type === "hidden") return null;
      return inputRoles[type] ?? "textbox";
    }
    return interactiveTags[tag] ?? null;
  };

  const walk = (el: MinimalElement): void => {
    if (!visible(el)) return;
    const role = roleOf(el);
    if (role !== null && role !== "text") {
      const name = accessibleName(el);
      if (name.length > 0 || role !== "heading") {
        counter += 1;
        const ref = "e" + counter;
        if (el.setAttribute) el.setAttribute("data-cove-ref", ref);
        entries.push({
          ref,
          role,
          name,
          tag: el.tagName.toLowerCase(),
          value: typeof el.value === "string" ? el.value : null,
          href: typeof el.href === "string" ? el.href : el.getAttribute("href"),
          disabled: el.disabled === true || el.getAttribute("disabled") !== null,
        });
      }
    }
    for (let i = 0; i < el.children.length; i++) {
      walk(el.children[i]);
    }
  };

  walk(root);
  return entries;
}

export function findByText(entries: SnapshotEntry[], query: string): SnapshotEntry[] {
  const q = query.toLowerCase();
  return entries.filter((e) => e.name.toLowerCase().includes(q));
}

export function findRef(entries: SnapshotEntry[], ref: string): SnapshotEntry | null {
  return entries.find((e) => e.ref === ref) ?? null;
}

export function buildSnapshotEvalPayload(): string {
  return "(() => { const collect = " + collectSnapshot.toString() +
    "; return JSON.stringify({ url: location.href, title: document.title, entries: collect(document.body) }); })()";
}

export function isValidRef(ref: string): boolean {
  return /^e\d+$/.test(ref);
}

function refSelector(ref: string): string {
  if (!isValidRef(ref)) throw new Error(`invalid automation ref: ${ref}`);
  return `[data-cove-ref="${ref}"]`;
}

export function buildClickEvalPayload(ref: string): string {
  return "(() => { const el = document.querySelector('" + refSelector(ref) + "');" +
    " if (!el) return JSON.stringify({ ok: false, error: 'ref not found' });" +
    " el.click(); return JSON.stringify({ ok: true }); })()";
}

export function buildFillEvalPayload(ref: string, value: string): string {
  const valueJson = JSON.stringify(value);
  return "(() => { const el = document.querySelector('" + refSelector(ref) + "');" +
    " if (!el) return JSON.stringify({ ok: false, error: 'ref not found' });" +
    " el.focus(); el.value = " + valueJson + ";" +
    " el.dispatchEvent(new Event('input', { bubbles: true }));" +
    " el.dispatchEvent(new Event('change', { bubbles: true }));" +
    " return JSON.stringify({ ok: true }); })()";
}

export function buildClearEvalPayload(ref: string): string {
  return "(() => { const el = document.querySelector('" + refSelector(ref) + "');" +
    " if (!el) return JSON.stringify({ ok: false, error: 'ref not found' });" +
    " el.focus();" +
    " if (el.isContentEditable) { el.textContent = ''; } else { el.value = ''; }" +
    " el.dispatchEvent(new Event('input', { bubbles: true }));" +
    " el.dispatchEvent(new Event('change', { bubbles: true }));" +
    " return JSON.stringify({ ok: true }); })()";
}

export function buildTypeEvalPayload(ref: string, text: string): string {
  const textJson = JSON.stringify(text);
  return "(() => { const el = document.querySelector('" + refSelector(ref) + "');" +
    " if (!el) return JSON.stringify({ ok: false, error: 'ref not found' });" +
    " el.focus(); const t = " + textJson + ";" +
    " for (const ch of t) {" +
    "   if (el.isContentEditable) { el.textContent = (el.textContent || '') + ch; }" +
    "   else { el.value = (el.value || '') + ch; }" +
    "   el.dispatchEvent(new Event('input', { bubbles: true }));" +
    " }" +
    " el.dispatchEvent(new Event('change', { bubbles: true }));" +
    " return JSON.stringify({ ok: true, value: (typeof el.value === 'string' ? el.value : null) }); })()";
}

export function buildPressEvalPayload(ref: string, key: string): string {
  const keyJson = JSON.stringify(key);
  return "(() => { const el = document.querySelector('" + refSelector(ref) + "') || document.activeElement || document.body;" +
    " const key = " + keyJson + ";" +
    " const opts = { key: key, bubbles: true, cancelable: true };" +
    " el.dispatchEvent(new KeyboardEvent('keydown', opts));" +
    " if (key.length === 1) el.dispatchEvent(new KeyboardEvent('keypress', opts));" +
    " el.dispatchEvent(new KeyboardEvent('keyup', opts));" +
    " return JSON.stringify({ ok: true, isTrusted: false }); })()";
}

export function buildSelectEvalPayload(ref: string, value: string): string {
  const valueJson = JSON.stringify(value);
  return "(() => { const el = document.querySelector('" + refSelector(ref) + "');" +
    " if (!el) return JSON.stringify({ ok: false, error: 'ref not found' });" +
    " const want = " + valueJson + ";" +
    " const opt = Array.from(el.options || []).find((o) => o.value === want);" +
    " if (!opt) return JSON.stringify({ ok: false, error: 'no matching option' });" +
    " el.value = want;" +
    " el.dispatchEvent(new Event('input', { bubbles: true }));" +
    " el.dispatchEvent(new Event('change', { bubbles: true }));" +
    " return JSON.stringify({ ok: true, value: el.value }); })()";
}

export function normalizeScroll(x: number | null | undefined, y: number | null | undefined): { x: number; y: number } {
  return { x: typeof x === "number" ? x : 0, y: typeof y === "number" ? y : 0 };
}

export function buildScrollEvalPayload(ref: string | null, x: number | null, y: number | null): string {
  const coords = normalizeScroll(x, y);
  const target = ref
    ? "document.querySelector('" + refSelector(ref) + "')"
    : "null";
  return "(() => { const el = " + target + ";" +
    (ref
      ? " if (!el) return JSON.stringify({ ok: false, error: 'ref not found' });" +
        " el.scrollTo ? el.scrollTo(" + coords.x + ", " + coords.y + ") : (el.scrollLeft = " + coords.x + ", el.scrollTop = " + coords.y + ");"
      : " window.scrollTo(" + coords.x + ", " + coords.y + ");") +
    " return JSON.stringify({ ok: true, x: " + coords.x + ", y: " + coords.y + " }); })()";
}

export function clampWaitDeadline(ms: number | null | undefined): number {
  const raw = typeof ms === "number" ? ms : 2000;
  if (raw < 0) return 0;
  if (raw > 8000) return 8000;
  return raw;
}

export function buildWaitEvalPayload(ref: string | null, text: string | null, timeoutMs: number): string {
  const deadline = clampWaitDeadline(timeoutMs);
  const selector = ref ? JSON.stringify(refSelector(ref)) : "null";
  const textJson = text ? JSON.stringify(text) : "null";
  return "(() => new Promise((resolve) => {" +
    " const selector = " + selector + "; const wantText = " + textJson + ";" +
    " const deadline = Date.now() + " + deadline + ";" +
    " const met = () => {" +
    "   if (selector) { const el = document.querySelector(selector); if (!el) return false;" +
    "     if (el.offsetWidth <= 0 && el.offsetHeight <= 0) return false; }" +
    "   if (wantText) { return (document.body.innerText || '').indexOf(wantText) !== -1; }" +
    "   return true;" +
    " };" +
    " const tick = () => {" +
    "   if (met()) { resolve(JSON.stringify({ ok: true, found: true })); return; }" +
    "   if (Date.now() >= deadline) { resolve(JSON.stringify({ ok: false, found: false, error: 'wait timed out' })); return; }" +
    "   setTimeout(tick, 100);" +
    " };" +
    " tick();" +
    " }))()";
}

export const GET_PROPS = ["text", "value", "href", "title", "checked", "disabled", "visible"] as const;
export const IS_STATES = ["visible", "enabled", "checked", "editable"] as const;

export function isValidGetProp(prop: string): boolean {
  return (GET_PROPS as readonly string[]).includes(prop);
}

export function isValidIsState(state: string): boolean {
  return (IS_STATES as readonly string[]).includes(state);
}

export function buildGetEvalPayload(ref: string, prop: string): string {
  if (!isValidGetProp(prop)) throw new Error(`unknown property: ${prop}`);
  const propJson = JSON.stringify(prop);
  return "(() => { const el = document.querySelector('" + refSelector(ref) + "');" +
    " if (!el) return JSON.stringify({ ok: false, error: 'ref not found' });" +
    " const prop = " + propJson + "; let value = null;" +
    " if (prop === 'text') value = (el.innerText || el.textContent || '').trim();" +
    " else if (prop === 'value') value = typeof el.value === 'string' ? el.value : null;" +
    " else if (prop === 'href') value = el.href || el.getAttribute('href');" +
    " else if (prop === 'title') value = el.title || el.getAttribute('title');" +
    " else if (prop === 'checked') value = el.checked === true;" +
    " else if (prop === 'disabled') value = el.disabled === true || el.getAttribute('disabled') !== null;" +
    " else if (prop === 'visible') value = el.offsetWidth > 0 || el.offsetHeight > 0;" +
    " return JSON.stringify({ ok: true, prop: prop, value: value }); })()";
}

export function buildIsEvalPayload(ref: string, state: string): string {
  if (!isValidIsState(state)) throw new Error(`unknown state: ${state}`);
  const stateJson = JSON.stringify(state);
  return "(() => { const el = document.querySelector('" + refSelector(ref) + "');" +
    " if (!el) return JSON.stringify({ ok: false, error: 'ref not found' });" +
    " const state = " + stateJson + "; let result = false;" +
    " if (state === 'visible') result = el.offsetWidth > 0 || el.offsetHeight > 0;" +
    " else if (state === 'enabled') result = !(el.disabled === true || el.getAttribute('disabled') !== null);" +
    " else if (state === 'checked') result = el.checked === true;" +
    " else if (state === 'editable') result = el.isContentEditable === true || (('value' in el) && !(el.disabled === true) && !(el.readOnly === true));" +
    " return JSON.stringify({ ok: true, state: state, result: result }); })()";
}

export interface AutomationExecEvent {
  requestId: string;
  paneId: string;
  kind: string;
  ref?: string | null;
  value?: string | null;
  js?: string | null;
}

interface ScrollValue { x?: number | null; y?: number | null }
interface WaitValue { text?: string | null; timeoutMs?: number | null }

export function buildAutomationJs(ev: AutomationExecEvent): string {
  switch (ev.kind) {
    case "snapshot":
      return buildSnapshotEvalPayload();
    case "click":
      return buildClickEvalPayload(ev.ref ?? "");
    case "fill":
      return buildFillEvalPayload(ev.ref ?? "", ev.value ?? "");
    case "clear":
      return buildClearEvalPayload(ev.ref ?? "");
    case "type":
      return buildTypeEvalPayload(ev.ref ?? "", ev.value ?? "");
    case "press":
      return buildPressEvalPayload(ev.ref ?? "", ev.value ?? "");
    case "select":
      return buildSelectEvalPayload(ev.ref ?? "", ev.value ?? "");
    case "scroll": {
      const sv = (ev.value ? JSON.parse(ev.value) : {}) as ScrollValue;
      return buildScrollEvalPayload(ev.ref ?? null, sv.x ?? null, sv.y ?? null);
    }
    case "wait": {
      const wv = (ev.value ? JSON.parse(ev.value) : {}) as WaitValue;
      return buildWaitEvalPayload(ev.ref ?? null, wv.text ?? null, wv.timeoutMs ?? 2000);
    }
    case "get":
      return buildGetEvalPayload(ev.ref ?? "", ev.value ?? "");
    case "is":
      return buildIsEvalPayload(ev.ref ?? "", ev.value ?? "");
    case "eval":
      if (!ev.js) throw new Error("eval action requires js");
      return ev.js;
    default:
      throw new Error(`unknown automation kind: ${ev.kind}`);
  }
}
