import { invoke } from "./invoke";
import type { RecentSessionRow } from "./launcher-model";
import { groupRecentsByAdapter, type AdapterLabel, type AdapterSessionGroup } from "./session-resume";

interface SessionCorpusEntry {
  id: string;
  workspaceId: string;
  adapter: string;
  startedAt: string;
  endedAt: string | null;
  extractorVersion: string | null;
}

interface SearchResults { entries: SessionCorpusEntry[] }
interface RecentResults { sessions: RecentSessionRow[] }

export type ResumeHandler = (adapter: string, sessionId: string, cwd: string, displayName: string) => void;

export async function renderSessionPicker(
  workspaceId: string,
  projectDir: string,
  adapters: AdapterLabel[],
  onResume: ResumeHandler,
): Promise<HTMLElement> {
  const el = document.createElement("div");
  el.className = "session-picker";
  el.style.cssText = "display:flex;flex-direction:column;height:100%;background:#0b1622;color:#e5e9f0;font-family:system-ui,sans-serif;";

  el.appendChild(buildHeader());
  el.appendChild(await buildRecentsArea(projectDir, adapters, onResume));
  el.appendChild(buildSearchArea(workspaceId));
  el.appendChild(buildSettingsPanel(workspaceId));

  return el;
}

function buildHeader(): HTMLElement {
  const header = document.createElement("div");
  header.style.cssText = "padding:12px;border-bottom:1px solid #1e2d3f;";
  const title = document.createElement("h2");
  title.style.cssText = "font-size:16px;font-weight:600;margin:0;";
  title.textContent = "Resume Session";
  header.appendChild(title);
  const subtitle = document.createElement("p");
  subtitle.style.cssText = "font-size:12px;color:#6b7d8f;margin:4px 0 0;";
  subtitle.textContent = "Resume a past session in this workspace directory";
  header.appendChild(subtitle);
  return header;
}

async function buildRecentsArea(projectDir: string, adapters: AdapterLabel[], onResume: ResumeHandler): Promise<HTMLElement> {
  const container = document.createElement("div");
  container.style.cssText = "border-bottom:1px solid #1e2d3f;padding:8px 12px;display:flex;flex-direction:column;gap:6px;max-height:40%;overflow-y:auto;";

  let groups: AdapterSessionGroup[] = [];
  try {
    const res = await invoke<RecentResults>("cove://commands/session.recent", { limit: 50 });
    groups = groupRecentsByAdapter(res.sessions ?? [], projectDir, adapters, Date.now());
  } catch (e) {
    console.warn("session.recent unavailable", e);
  }

  if (groups.length === 0) {
    const empty = document.createElement("div");
    empty.style.cssText = "padding:12px 4px;color:#6b7d8f;font-size:12px;";
    empty.textContent = "No resumable sessions for this directory";
    container.appendChild(empty);
    return container;
  }

  for (const group of groups)
    container.appendChild(buildAdapterDropdown(group, onResume));

  return container;
}

function buildAdapterDropdown(group: AdapterSessionGroup, onResume: ResumeHandler): HTMLElement {
  const wrap = document.createElement("div");
  wrap.style.cssText = "border:1px solid #1e2d3f;border-radius:6px;overflow:hidden;";

  const head = document.createElement("button");
  head.type = "button";
  head.style.cssText = "width:100%;display:flex;align-items:center;gap:8px;padding:8px 10px;background:#14202e;border:none;color:#e5e9f0;cursor:pointer;font-size:13px;font-weight:500;text-align:left;";

  const caret = document.createElement("span");
  caret.textContent = "▸";
  caret.style.cssText = "font-size:10px;color:#6b7d8f;transition:transform .1s;";
  head.appendChild(caret);

  const name = document.createElement("span");
  name.style.flex = "1";
  name.textContent = group.displayName;
  head.appendChild(name);

  const count = document.createElement("span");
  count.style.cssText = "font-size:11px;color:#6b7d8f;";
  count.textContent = String(group.sessions.length);
  head.appendChild(count);

  const list = document.createElement("div");
  list.style.cssText = "display:none;flex-direction:column;";

  for (const s of group.sessions) {
    const row = document.createElement("button");
    row.type = "button";
    row.style.cssText = "display:block;width:100%;text-align:left;padding:8px 12px 8px 28px;background:#0b1622;border:none;border-top:1px solid #14202e;color:#cdd6e3;cursor:pointer;font-size:12px;";
    row.textContent = s.label;
    row.addEventListener("mouseenter", () => row.style.background = "#14202e");
    row.addEventListener("mouseleave", () => row.style.background = "#0b1622");
    row.addEventListener("click", () => onResume(s.adapter, s.sessionId, s.cwd, group.displayName));
    list.appendChild(row);
  }

  let open = false;
  head.addEventListener("click", () => {
    open = !open;
    list.style.display = open ? "flex" : "none";
    caret.style.transform = open ? "rotate(90deg)" : "";
  });

  wrap.appendChild(head);
  wrap.appendChild(list);
  return wrap;
}

function buildSearchArea(workspaceId: string): HTMLElement {
  const container = document.createElement("div");
  container.style.cssText = "flex:1;display:flex;flex-direction:column;overflow:hidden;";

  const searchBar = document.createElement("div");
  searchBar.style.cssText = "padding:8px 12px;border-bottom:1px solid #1e2d3f;display:flex;gap:8px;";

  const input = document.createElement("input");
  input.type = "text";
  input.placeholder = "Search session transcripts...";
  input.style.cssText = "flex:1;padding:6px 10px;background:#14202e;border:1px solid #2b3d52;border-radius:4px;color:#e5e9f0;font-size:13px;";
  searchBar.appendChild(input);

  const searchBtn = document.createElement("button");
  searchBtn.textContent = "Search";
  searchBtn.style.cssText = "padding:6px 14px;background:#2563eb;border:1px solid #3b82f6;border-radius:4px;color:#fff;cursor:pointer;font-size:12px;";
  searchBar.appendChild(searchBtn);

  container.appendChild(searchBar);

  const resultsList = document.createElement("div");
  resultsList.className = "session-results";
  resultsList.style.cssText = "flex:1;overflow-y:auto;";
  container.appendChild(resultsList);

  const performSearch = async () => {
    resultsList.innerHTML = "";
    const query = input.value.trim();
    if (!query) {
      const empty = document.createElement("div");
      empty.style.cssText = "padding:20px;color:#6b7d8f;text-align:center;font-size:13px;";
      empty.textContent = "Enter a search query to find past sessions";
      resultsList.appendChild(empty);
      return;
    }
    try {
      const result = await invoke<SearchResults>("cove://commands/vault.search", { workspaceId, query });
      const entries = result.entries || [];
      if (entries.length === 0) {
        const empty = document.createElement("div");
        empty.style.cssText = "padding:20px;color:#6b7d8f;text-align:center;font-size:13px;";
        empty.textContent = "No sessions found";
        resultsList.appendChild(empty);
        return;
      }
      for (const entry of entries)
        resultsList.appendChild(buildSearchRow(entry));
    } catch (e) {
      resultsList.innerHTML = `<div style="padding:20px;color:#ef4444;">Search failed: ${(e as Error).message}</div>`;
    }
  };

  searchBtn.addEventListener("click", performSearch);
  input.addEventListener("keydown", (e) => {
    if (e.key === "Enter") performSearch();
  });

  return container;
}

function buildSearchRow(entry: SessionCorpusEntry): HTMLElement {
  const row = document.createElement("div");
  row.style.cssText = "padding:10px 12px;border-bottom:1px solid #14202e;display:flex;gap:10px;align-items:center;";

  const icon = document.createElement("span");
  icon.style.cssText = "font-size:18px;";
  icon.textContent = "💬";
  row.appendChild(icon);

  const info = document.createElement("div");
  info.style.cssText = "flex:1;min-width:0;";

  const adapter = document.createElement("div");
  adapter.style.cssText = "font-size:13px;color:#e5e9f0;font-weight:500;";
  adapter.textContent = entry.adapter;
  info.appendChild(adapter);

  const date = document.createElement("div");
  date.style.cssText = "font-size:11px;color:#6b7d8f;";
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

function buildSettingsPanel(workspaceId: string): HTMLElement {
  const panel = document.createElement("div");
  panel.style.cssText = "border-top:1px solid #1e2d3f;padding:12px;";

  const header = document.createElement("div");
  header.style.cssText = "font-size:12px;color:#6b7d8f;text-transform:uppercase;letter-spacing:0.5px;margin-bottom:8px;";
  header.textContent = "Vault Settings";
  panel.appendChild(header);

  const depthRow = document.createElement("div");
  depthRow.style.cssText = "display:flex;align-items:center;gap:8px;margin-bottom:8px;";

  const depthLabel = document.createElement("label");
  depthLabel.style.cssText = "font-size:13px;color:#e5e9f0;min-width:80px;";
  depthLabel.textContent = "Depth:";
  depthRow.appendChild(depthLabel);

  const depthSelect = document.createElement("select");
  depthSelect.style.cssText = "flex:1;padding:4px 8px;background:#14202e;border:1px solid #2b3d52;border-radius:4px;color:#e5e9f0;font-size:12px;";
  for (const depth of ["quick", "standard", "deep"]) {
    const opt = document.createElement("option");
    opt.value = depth;
    opt.textContent = depth;
    depthSelect.appendChild(opt);
  }
  depthSelect.value = "standard";
  depthSelect.addEventListener("change", () => updateVaultSetting(workspaceId, "depth", depthSelect.value));
  depthRow.appendChild(depthSelect);

  panel.appendChild(depthRow);

  const horizonRow = document.createElement("div");
  horizonRow.style.cssText = "display:flex;align-items:center;gap:8px;margin-bottom:8px;";

  const horizonLabel = document.createElement("label");
  horizonLabel.style.cssText = "font-size:13px;color:#e5e9f0;min-width:80px;";
  horizonLabel.textContent = "Horizon:";
  horizonRow.appendChild(horizonLabel);

  const horizonInput = document.createElement("input");
  horizonInput.type = "number";
  horizonInput.value = "30";
  horizonInput.min = "1";
  horizonInput.max = "365";
  horizonInput.style.cssText = "flex:1;padding:4px 8px;background:#14202e;border:1px solid #2b3d52;border-radius:4px;color:#e5e9f0;font-size:12px;";
  horizonInput.addEventListener("change", () => updateVaultSetting(workspaceId, "horizon", horizonInput.value));
  horizonRow.appendChild(horizonInput);

  panel.appendChild(horizonRow);

  const reindexBtn = document.createElement("button");
  reindexBtn.textContent = "Reindex All";
  reindexBtn.style.cssText = "padding:4px 12px;background:#1e2d3f;border:1px solid #2b3d52;border-radius:4px;color:#e5e9f0;cursor:pointer;font-size:12px;";
  reindexBtn.addEventListener("click", () => reindexVault(workspaceId));
  panel.appendChild(reindexBtn);

  return panel;
}

async function updateVaultSetting(workspaceId: string, key: string, value: string): Promise<void> {
  try {
    await invoke("cove://commands/vault.set-setting", { workspaceId, key, value });
  } catch (e) {
    console.error("Setting update failed:", e);
  }
}

async function reindexVault(workspaceId: string): Promise<void> {
  try {
    await invoke("cove://commands/vault.reindex", { workspaceId });
  } catch (e) {
    console.error("Reindex failed:", e);
  }
}
