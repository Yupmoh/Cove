import { invoke } from "./invoke";

interface SessionCorpusEntry {
  id: string;
  workspaceId: string;
  adapter: string;
  startedAt: string;
  endedAt: string | null;
  extractorVersion: string | null;
}

interface SearchResults { entries: SessionCorpusEntry[] }

export async function renderSessionPicker(workspaceId: string): Promise<HTMLElement> {
  const el = document.createElement("div");
  el.className = "session-picker";
  el.style.cssText = "display:flex;flex-direction:column;height:100%;background:#0b1622;color:#e5e9f0;font-family:system-ui,sans-serif;";

  el.appendChild(buildHeader());
  el.appendChild(await buildSearchArea(workspaceId));
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
  subtitle.textContent = "Search past sessions and resume in a new pane";
  header.appendChild(subtitle);
  return header;
}

async function buildSearchArea(workspaceId: string): Promise<HTMLElement> {
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
      for (const entry of entries) {
        resultsList.appendChild(buildSessionRow(entry, workspaceId));
      }
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

function buildSessionRow(entry: SessionCorpusEntry, workspaceId: string): HTMLElement {
  const row = document.createElement("div");
  row.style.cssText = "padding:10px 12px;border-bottom:1px solid #14202e;cursor:pointer;display:flex;gap:10px;align-items:center;";
  row.addEventListener("mouseenter", () => row.style.background = "#14202e");
  row.addEventListener("mouseleave", () => row.style.background = "");

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

  const resumeBtn = document.createElement("button");
  resumeBtn.textContent = "Resume";
  resumeBtn.style.cssText = "padding:4px 10px;background:#2563eb;border:1px solid #3b82f6;border-radius:4px;color:#fff;cursor:pointer;font-size:11px;";
  resumeBtn.addEventListener("click", (e) => {
    e.stopPropagation();
    resumeSession(workspaceId, entry.id);
  });
  row.appendChild(resumeBtn);

  row.addEventListener("click", () => resumeSession(workspaceId, entry.id));

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

async function resumeSession(workspaceId: string, sessionId: string): Promise<void> {
  try {
    await invoke("cove://commands/vault.resume", { workspaceId, sessionId });
  } catch (e) {
    console.error("Resume failed:", e);
  }
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
