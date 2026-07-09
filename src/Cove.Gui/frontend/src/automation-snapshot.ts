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
