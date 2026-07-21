const STROKE_ATTRS = 'fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round"';

function svg(body: string): string {
  return `<svg viewBox="0 0 24 24" ${STROKE_ATTRS} aria-hidden="true">${body}</svg>`;
}

function markSvg(body: string): string {
  return `<svg viewBox="0 0 24 24" fill="none" aria-hidden="true">${body}</svg>`;
}

export const ICONS: Record<string, string> = {
  bays: svg('<rect x="3" y="4" width="18" height="16" rx="2"/><line x1="9.5" y1="4" x2="9.5" y2="20"/>'),
  overview: svg('<rect x="3.5" y="3.5" width="7" height="7" rx="1.5"/><rect x="13.5" y="3.5" width="7" height="7" rx="1.5"/><rect x="3.5" y="13.5" width="7" height="7" rx="1.5"/><rect x="13.5" y="13.5" width="7" height="7" rx="1.5"/>'),
  skills: svg('<path d="M12 3.5l1.9 5.6 5.6 1.9-5.6 1.9L12 18.5l-1.9-5.6-5.6-1.9 5.6-1.9z"/><path d="M19 15.5l.8 2.2 2.2.8-2.2.8-.8 2.2-.8-2.2-2.2-.8 2.2-.8z"/>'),
  activity: svg('<polyline points="3 12 7.5 12 10.5 5.5 14 18.5 16.5 12 21 12"/>'),
  timeline: svg('<circle cx="12" cy="12" r="8.5"/><polyline points="12 7 12 12 15.5 14"/>'),
  notepad: svg('<rect x="4.5" y="3.5" width="15" height="17" rx="2"/><line x1="8.5" y1="8.5" x2="15.5" y2="8.5"/><line x1="8.5" y1="12.5" x2="15.5" y2="12.5"/><line x1="8.5" y1="16.5" x2="12.5" y2="16.5"/>'),
  terminal: svg('<rect x="3" y="4.5" width="18" height="15" rx="2"/><polyline points="7 9.5 10 12 7 14.5"/><line x1="12.5" y1="15" x2="16.5" y2="15"/>'),
  browser: svg('<circle cx="12" cy="12" r="8.5"/><line x1="3.5" y1="12" x2="20.5" y2="12"/><path d="M12 3.5c2.6 2.2 4 5.2 4 8.5s-1.4 6.3-4 8.5c-2.6-2.2-4-5.2-4-8.5s1.4-6.3 4-8.5z"/>'),
  search: svg('<circle cx="11" cy="11" r="6.2"/><line x1="15.6" y1="15.6" x2="20.5" y2="20.5"/>'),
  git: svg('<circle cx="6.5" cy="6" r="2.3"/><circle cx="6.5" cy="18" r="2.3"/><circle cx="17.5" cy="8" r="2.3"/><line x1="6.5" y1="8.3" x2="6.5" y2="15.7"/><path d="M17.5 10.3c0 3-2.5 4-5.5 4.4-2 .3-3.7 1-4.6 2.6"/>'),
  tasks: svg('<polyline points="3.5 6 5.2 7.7 8.5 4.4"/><line x1="11.5" y1="6" x2="20.5" y2="6"/><polyline points="3.5 13 5.2 14.7 8.5 11.4"/><line x1="11.5" y1="13" x2="20.5" y2="13"/><line x1="3.5" y1="19.5" x2="20.5" y2="19.5"/>'),
  agents: svg('<rect x="4.5" y="8.5" width="15" height="10.5" rx="3"/><circle cx="9.5" cy="13.5" r="1.1"/><circle cx="14.5" cy="13.5" r="1.1"/><line x1="12" y1="8.5" x2="12" y2="5.5"/><circle cx="12" cy="4.3" r="1.2"/>'),
  gear: svg('<circle cx="12" cy="12" r="3.2"/><path d="M12 2.8v2.6M12 18.6v2.6M2.8 12h2.6M18.6 12h2.6M5.5 5.5l1.8 1.8M16.7 16.7l1.8 1.8M18.5 5.5l-1.8 1.8M7.3 16.7l-1.8 1.8"/>'),
  refresh: svg('<path d="M20.5 12a8.5 8.5 0 1 1-2.6-6.1"/><polyline points="20.5 3.5 20.5 9 15 9"/>'),
  plus: svg('<line x1="12" y1="5" x2="12" y2="19"/><line x1="5" y1="12" x2="19" y2="12"/>'),
  minus: svg('<line x1="5" y1="12" x2="19" y2="12"/>'),
  home: svg('<path d="M3.5 10.5L12 3.5l8.5 7"/><path d="M5.5 9.5V20h13V9.5"/>'),
  folder: svg('<path d="M3.5 7a2 2 0 0 1 2-2h4.2l2 2.5h6.8a2 2 0 0 1 2 2V17a2 2 0 0 1-2 2h-13a2 2 0 0 1-2-2z"/>'),
  file: svg('<path d="M14 3.5H7a2 2 0 0 0-2 2v13a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V8.5z"/><polyline points="14 3.5 14 8.5 19 8.5"/>'),
  image: svg('<rect x="3.5" y="4.5" width="17" height="15" rx="2"/><circle cx="9" cy="10" r="1.6"/><path d="M20.5 15.5l-4.5-4.5-8.5 8.5"/>'),
  play: svg('<rect x="3.5" y="4.5" width="17" height="15" rx="2"/><path d="M10 9l5 3-5 3z"/>'),
  diff: svg('<line x1="6" y1="7.5" x2="12" y2="7.5"/><line x1="9" y1="4.5" x2="9" y2="10.5"/><line x1="6" y1="17" x2="12" y2="17"/><line x1="14.5" y1="4" x2="19.5" y2="20"/>'),
  "split-right": svg('<rect x="3.5" y="5" width="17" height="14" rx="2"/><line x1="13.5" y1="5" x2="13.5" y2="19"/>'),
  "split-down": svg('<rect x="3.5" y="5" width="17" height="14" rx="2"/><line x1="3.5" y1="13" x2="20.5" y2="13"/>'),
  "chevron-right": svg('<polyline points="9.5 6 15.5 12 9.5 18"/>'),
  "chevron-left": svg('<polyline points="14.5 6 8.5 12 14.5 18"/>'),
  inspect: svg('<rect x="3.5" y="3.5" width="17" height="17" rx="2.5" stroke-dasharray="3.4 2.6"/><path d="M10.5 10.5l7.2 2.7-3 1.5-1.5 3z"/>'),
};

export function iconSvg(name: string): string {
  return ICONS[name] ?? ICONS.terminal;
}

export const NOOK_TYPE_ICON: Record<string, string> = {
  terminal: "terminal",
  browser: "browser",
  search: "search",
  git: "git",
  sourceControl: "git",
  "tasks-list": "tasks",
  "tasks-kanban": "tasks",
  notepad: "notepad",
  editor: "file",
  markdown: "file",
  image: "image",
  pdf: "file",
  video: "play",
  diff: "diff",
  "diff-review": "diff",
  empty: "terminal",
};

export function iconForNookType(nookType: string): string {
  return iconSvg(NOOK_TYPE_ICON[nookType] ?? "terminal");
}

function adapterImage(path: string, label: string): string {
  return `<img class="adapter-icon" src="${path}" alt="${label}" draggable="false" />`;
}

function adapterMask(path: string, label: string): string {
  return `<span class="adapter-icon adapter-icon-mask" style="--adapter-mask:url('${path}')" role="img" aria-label="${label}"></span>`;
}

function adapterGlyph(label: string, body: string): string {
  return `<svg class="adapter-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" role="img" aria-label="${label}">${body}</svg>`;
}

const ADAPTER_ICONS: Record<string, string> = {
  "claude-code": adapterMask("/adapter-icons/claude.png", "Claude Code"),
  codex: adapterMask("/adapter-icons/codex.png", "Codex"),
  omp: adapterImage("/adapter-icons/omp.svg", "Oh My Pi"),
  "cursor-agent": adapterGlyph("Cursor Agent", '<path d="M5 3.5l14 8.2-6.3 1.6-3.2 6.2z" fill="currentColor" stroke="none"/><path d="M12.5 13.4l5.2 5.2"/>'),
  hermes: adapterGlyph("Hermes", '<path d="M6.5 5v14M17.5 5v14M6.5 12h11"/><path d="M4 7l2.5-2M20 7l-2.5-2"/>'),
  openclaw: adapterGlyph("OpenClaw", '<path d="M7 18c-2.8-2.5-2.8-6.5-.5-9M12 19c-3-3.2-3-8.8 0-12M17 18c2.8-2.5 2.8-6.5.5-9"/><path d="M6.5 9L4 6M12 7V3.5M17.5 9L20 6"/>'),
  opencode: adapterGlyph("opencode", '<path d="M9.5 5L4 12l5.5 7M14.5 5l5.5 7-5.5 7"/>'),
  pi: adapterGlyph("pi", '<path d="M4.5 7.5h15M8 7.5v11M16 7.5v7.8c0 2.2 1.2 3.2 3 3.2"/>'),
};

export function adapterIconSvg(adapterName: string): string {
  return ADAPTER_ICONS[adapterName.toLowerCase()] ?? ICONS.agents;
}

export interface FileIconSpec {
  kind: string;
  color: string;
  svg: string;
}

function languageMark(label: string): string {
  return markSvg(`<rect x="3" y="3" width="18" height="18" rx="4" fill="currentColor" opacity=".16"/><text x="12" y="15.2" text-anchor="middle" fill="currentColor" font-size="8.5" font-weight="700" font-family="ui-monospace,monospace">${label}</text>`);
}

export function fileIcon(fileName: string): FileIconSpec {
  const name = fileName.toLowerCase();
  const extension = name.includes(".") ? name.slice(name.lastIndexOf(".")) : "";
  if (extension === ".cs") return { kind: "csharp", color: "#63c174", svg: languageMark("C#") };
  if (extension === ".fs" || extension === ".fsx") return { kind: "fsharp", color: "#70a5eb", svg: languageMark("F#") };
  if (extension === ".ts" || extension === ".tsx") return { kind: "typescript", color: "#5ba8e8", svg: languageMark("TS") };
  if (extension === ".js" || extension === ".jsx" || extension === ".mjs") return { kind: "javascript", color: "#e8d15b", svg: languageMark("JS") };
  if (extension === ".json" || name === "package.json" || name.endsWith(".jsonl")) return { kind: "json", color: "#e8cf6a", svg: languageMark("{}") };
  if (extension === ".md" || extension === ".mdx") return { kind: "markdown", color: "#8ab4f8", svg: languageMark("M") };
  if ([".sln", ".slnx", ".csproj", ".fsproj", ".props", ".targets"].includes(extension)) return { kind: "dotnet", color: "#b18cff", svg: languageMark(".N") };
  if ([".yml", ".yaml", ".toml", ".ini", ".env"].includes(extension) || name === "dockerfile") return { kind: "config", color: "#a9b1d6", svg: ICONS.gear };
  if ([".png", ".jpg", ".jpeg", ".gif", ".webp", ".svg", ".ico"].includes(extension)) return { kind: "image", color: "#e69bc2", svg: ICONS.image };
  return { kind: "file", color: "currentColor", svg: ICONS.file };
}

export function monogram(label: string): string {
  const words = label.trim().split(/\s+/).filter((w) => w.length > 0);
  if (words.length === 0) return "?";
  if (words.length >= 2) return (words[0][0] + words[1][0]).toUpperCase();
  const w = words[0];
  return w.length >= 2 ? w[0].toUpperCase() + w[1] : w[0].toUpperCase();
}
