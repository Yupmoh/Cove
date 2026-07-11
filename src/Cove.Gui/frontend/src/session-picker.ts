import { invoke } from "./invoke";
import type { RecentSessionRow } from "./launcher-model";
import { adapterAccent } from "./launcher-model";
import { groupRecentsByAdapter, type AdapterLabel, type AdapterSessionGroup } from "./session-resume";

interface SessionCorpusEntry {
  id: string;
  bayId: string;
  adapter: string;
  startedAt: string;
  endedAt: string | null;
  extractorVersion: string | null;
}

interface SearchResults { entries: SessionCorpusEntry[] }
interface RecentResults { sessions: RecentSessionRow[] }

export type ResumeHandler = (adapter: string, sessionId: string, cwd: string, displayName: string) => void;

const ON_ACCENT_TEXT = "#11111b";

export async function renderSessionPicker(
  bayId: string,
  projectDir: string,
  adapters: AdapterLabel[],
  onResume: ResumeHandler,
): Promise<HTMLElement> {
  const el = document.createElement("div");
  el.className = "session-picker";
  el.style.cssText = "display:flex;flex-direction:column;height:100%;background:var(--bg);color:var(--fg);font-family:system-ui,sans-serif;";

  el.appendChild(buildHeader());
  el.appendChild(await buildRecentsArea(projectDir, adapters, onResume));
  el.appendChild(buildSearchArea(bayId));
  el.appendChild(buildSettingsPanel(bayId));

  return el;
}

function buildHeader(): HTMLElement {
  const header = document.createElement("div");
  header.style.cssText = "padding:12px 14px;border-bottom:1px solid var(--border);";
  const title = document.createElement("div");
  title.style.cssText = "font-size:13px;font-weight:650;margin:0;color:var(--fg);";
  title.textContent = "Resume a session";
  header.appendChild(title);
  const subtitle = document.createElement("div");
  subtitle.style.cssText = "font-size:11px;color:var(--muted);margin-top:3px;";
  subtitle.textContent = "Pick up a past session in this bay directory";
  header.appendChild(subtitle);
  return header;
}

async function buildRecentsArea(projectDir: string, adapters: AdapterLabel[], onResume: ResumeHandler): Promise<HTMLElement> {
  const container = document.createElement("div");
  container.style.cssText = "border-bottom:1px solid var(--border);padding:10px 14px;display:flex;flex-direction:column;gap:8px;max-height:40%;overflow-y:auto;";

  let groups: AdapterSessionGroup[] = [];
  try {
    const res = await invoke<RecentResults>("cove://commands/session.recent", { cwd: projectDir, limit: 50 });
    groups = groupRecentsByAdapter(res.sessions ?? [], projectDir, adapters, Date.now());
  } catch (e) {
    console.warn("session.recent unavailable", e);
  }

  if (groups.length === 0) {
    const empty = document.createElement("div");
    empty.style.cssText = "padding:10px 2px;color:var(--muted);font-size:12px;";
    empty.textContent = "No resumable sessions for this directory";
    container.appendChild(empty);
    return container;
  }

  for (const group of groups)
    container.appendChild(buildAdapterDropdown(group, onResume));

  return container;
}

function buildAdapterDropdown(group: AdapterSessionGroup, onResume: ResumeHandler): HTMLElement {
  const accent = adapterAccent(group.adapter, "");

  const dd = document.createElement("div");
  dd.style.cssText = "display:flex;flex-direction:column;gap:4px;";

  const trigger = document.createElement("button");
  trigger.type = "button";
  trigger.className = "cl-resume-trigger";
  trigger.style.cssText = "width:100%;justify-content:flex-start;";

  const dot = document.createElement("span");
  dot.className = "cl-session-dot";
  dot.style.background = accent;
  trigger.appendChild(dot);

  const name = document.createElement("span");
  name.className = "cl-resume-label";
  name.style.flex = "1";
  name.textContent = group.displayName;
  trigger.appendChild(name);

  const count = document.createElement("span");
  count.className = "cl-resume-count";
  count.textContent = String(group.sessions.length);
  trigger.appendChild(count);

  const chev = document.createElement("span");
  chev.className = "cl-resume-chev";
  chev.textContent = "▾";
  trigger.appendChild(chev);

  const menu = document.createElement("div");
  menu.style.cssText = "display:none;flex-direction:column;gap:1px;padding:5px;background:var(--panel);border:1px solid var(--border);border-radius:10px;";

  for (const s of group.sessions) {
    const row = document.createElement("button");
    row.type = "button";
    row.className = "cl-recent-row";
    row.style.cssText = "width:100%;border:none;background:transparent;font:inherit;text-align:left;";
    const rowDot = document.createElement("span");
    rowDot.className = "cl-session-dot";
    rowDot.style.background = accent;
    const label = document.createElement("span");
    label.className = "cl-recent-cwd";
    label.style.fontWeight = "500";
    label.textContent = s.label;
    label.title = s.label;
    const when = document.createElement("span");
    when.className = "cl-recent-when";
    when.textContent = s.relative;
    row.appendChild(rowDot);
    row.appendChild(label);
    row.appendChild(when);
    row.addEventListener("click", () => onResume(s.adapter, s.sessionId, s.cwd, group.displayName));
    menu.appendChild(row);
  }

  let open = false;
  trigger.addEventListener("click", () => {
    open = !open;
    menu.style.display = open ? "flex" : "none";
    chev.style.transform = open ? "rotate(180deg)" : "";
  });

  dd.appendChild(trigger);
  dd.appendChild(menu);
  return dd;
}

function buildSearchArea(bayId: string): HTMLElement {
  const container = document.createElement("div");
  container.style.cssText = "flex:1;display:flex;flex-direction:column;overflow:hidden;";

  const searchBar = document.createElement("div");
  searchBar.style.cssText = "padding:10px 14px;border-bottom:1px solid var(--border);display:flex;gap:8px;";

  const input = document.createElement("input");
  input.type = "text";
  input.placeholder = "Search session transcripts...";
  input.style.cssText = "flex:1;height:32px;padding:0 11px;background:color-mix(in srgb, var(--panel-2) 80%, transparent);border:1px solid var(--border);border-radius:8px;color:var(--fg);font-size:12px;font-family:inherit;outline:none;";
  input.addEventListener("focus", () => input.style.borderColor = "color-mix(in srgb, var(--accent) 55%, var(--border))");
  input.addEventListener("blur", () => input.style.borderColor = "var(--border)");
  searchBar.appendChild(input);

  const searchBtn = document.createElement("button");
  searchBtn.type = "button";
  searchBtn.textContent = "Search";
  searchBtn.style.cssText = `height:32px;padding:0 14px;background:var(--accent);border:1px solid var(--accent);border-radius:8px;color:${ON_ACCENT_TEXT};cursor:pointer;font-size:12px;font-weight:600;font-family:inherit;`;
  searchBar.appendChild(searchBtn);

  container.appendChild(searchBar);

  const resultsList = document.createElement("div");
  resultsList.className = "session-results";
  resultsList.style.cssText = "flex:1;overflow-y:auto;padding:6px 10px;display:flex;flex-direction:column;gap:2px;";
  container.appendChild(resultsList);

  const performSearch = async () => {
    resultsList.innerHTML = "";
    const query = input.value.trim();
    if (!query) {
      resultsList.appendChild(buildEmptyNotice("Enter a search query to find past sessions"));
      return;
    }
    try {
      const result = await invoke<SearchResults>("cove://commands/vault.search", { bayId, query });
      const entries = result.entries || [];
      if (entries.length === 0) {
        resultsList.appendChild(buildEmptyNotice("No sessions found"));
        return;
      }
      for (const entry of entries)
        resultsList.appendChild(buildSearchRow(entry));
    } catch (e) {
      const fail = document.createElement("div");
      fail.style.cssText = "padding:16px;color:var(--danger);font-size:12px;";
      fail.textContent = `Search failed: ${(e as Error).message}`;
      resultsList.appendChild(fail);
    }
  };

  searchBtn.addEventListener("click", performSearch);
  input.addEventListener("keydown", (e) => {
    if (e.key === "Enter") performSearch();
  });

  return container;
}

function buildEmptyNotice(text: string): HTMLElement {
  const empty = document.createElement("div");
  empty.style.cssText = "padding:20px;color:var(--muted);text-align:center;font-size:12px;";
  empty.textContent = text;
  return empty;
}

function buildSearchRow(entry: SessionCorpusEntry): HTMLElement {
  const row = document.createElement("div");
  row.className = "cl-recent-row";
  row.style.cssText = "cursor:default;";

  const dot = document.createElement("span");
  dot.className = "cl-session-dot";
  dot.style.background = adapterAccent(entry.adapter, "");
  row.appendChild(dot);

  const info = document.createElement("div");
  info.style.cssText = "flex:1;min-width:0;display:flex;flex-direction:column;gap:1px;";

  const adapter = document.createElement("div");
  adapter.style.cssText = "font-size:12px;color:var(--fg);font-weight:500;overflow:hidden;text-overflow:ellipsis;white-space:nowrap;";
  adapter.textContent = entry.adapter;
  info.appendChild(adapter);

  const date = document.createElement("div");
  date.style.cssText = "font-size:10px;color:var(--muted);";
  const startDate = new Date(entry.startedAt);
  date.textContent = startDate.toLocaleString();
  if (entry.endedAt) {
    const endDate = new Date(entry.endedAt);
    const duration = Math.round((endDate.getTime() - startDate.getTime()) / 60000);
    date.textContent += ` · ${duration}m`;
  }
  info.appendChild(date);

  row.appendChild(info);
  return row;
}

function buildSettingsPanel(bayId: string): HTMLElement {
  const panel = document.createElement("div");
  panel.style.cssText = "border-top:1px solid var(--border);padding:12px 14px;";

  const header = document.createElement("div");
  header.style.cssText = "font-size:11px;color:var(--muted);text-transform:uppercase;letter-spacing:0.5px;font-weight:700;margin-bottom:10px;";
  header.textContent = "Vault Settings";
  panel.appendChild(header);

  const depthRow = document.createElement("div");
  depthRow.style.cssText = "display:flex;align-items:center;gap:8px;margin-bottom:9px;";

  const depthLabel = document.createElement("label");
  depthLabel.style.cssText = "font-size:12px;color:var(--fg);min-width:80px;";
  depthLabel.textContent = "Depth";
  depthRow.appendChild(depthLabel);

  const depthSelect = document.createElement("select");
  depthSelect.style.cssText = "flex:1;height:30px;padding:0 10px;background:color-mix(in srgb, var(--panel-2) 80%, transparent);border:1px solid var(--border);border-radius:8px;color:var(--fg);font-size:12px;font-family:inherit;outline:none;";
  for (const depth of ["quick", "standard", "deep"]) {
    const opt = document.createElement("option");
    opt.value = depth;
    opt.textContent = depth;
    depthSelect.appendChild(opt);
  }
  depthSelect.value = "standard";
  depthSelect.addEventListener("change", () => updateVaultSetting(bayId, "depth", depthSelect.value));
  depthRow.appendChild(depthSelect);

  panel.appendChild(depthRow);

  const horizonRow = document.createElement("div");
  horizonRow.style.cssText = "display:flex;align-items:center;gap:8px;margin-bottom:12px;";

  const horizonLabel = document.createElement("label");
  horizonLabel.style.cssText = "font-size:12px;color:var(--fg);min-width:80px;";
  horizonLabel.textContent = "Horizon";
  horizonRow.appendChild(horizonLabel);

  const horizonInput = document.createElement("input");
  horizonInput.type = "number";
  horizonInput.value = "30";
  horizonInput.min = "1";
  horizonInput.max = "365";
  horizonInput.style.cssText = "flex:1;height:30px;padding:0 10px;background:color-mix(in srgb, var(--panel-2) 80%, transparent);border:1px solid var(--border);border-radius:8px;color:var(--fg);font-size:12px;font-family:inherit;outline:none;";
  horizonInput.addEventListener("change", () => updateVaultSetting(bayId, "horizon", horizonInput.value));
  horizonRow.appendChild(horizonInput);

  panel.appendChild(horizonRow);

  const reindexBtn = document.createElement("button");
  reindexBtn.type = "button";
  reindexBtn.textContent = "Reindex All";
  reindexBtn.style.cssText = "height:30px;padding:0 14px;background:color-mix(in srgb, var(--panel-2) 80%, transparent);border:1px solid var(--border);border-radius:8px;color:var(--fg);cursor:pointer;font-size:12px;font-family:inherit;";
  reindexBtn.addEventListener("mouseenter", () => reindexBtn.style.borderColor = "color-mix(in srgb, var(--accent) 55%, var(--border))");
  reindexBtn.addEventListener("mouseleave", () => reindexBtn.style.borderColor = "var(--border)");
  reindexBtn.addEventListener("click", () => reindexVault(bayId));
  panel.appendChild(reindexBtn);

  return panel;
}

async function updateVaultSetting(bayId: string, key: string, value: string): Promise<void> {
  try {
    await invoke("cove://commands/vault.set-setting", { bayId, key, value });
  } catch (e) {
    console.error("Setting update failed:", e);
  }
}

async function reindexVault(bayId: string): Promise<void> {
  try {
    await invoke("cove://commands/vault.reindex", { bayId });
  } catch (e) {
    console.error("Reindex failed:", e);
  }
}
