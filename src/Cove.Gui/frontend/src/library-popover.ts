import { invoke } from "./invoke";

interface LibraryEntry {
  id: string;
  workspaceId: string;
  paneId: string;
  paneType: string;
  title: string | null;
  stateJson: string | null;
  scrollback: string | null;
  kind: string;
  capturedAt: string;
}

interface LibraryListResult { entries: LibraryEntry[] }

export async function renderLibraryPopover(workspaceId: string): Promise<HTMLElement> {
  const el = document.createElement("div");
  el.className = "library-popover";
  el.style.cssText = "display:flex;flex-direction:column;height:100%;background:#0b1622;color:#e5e9f0;font-family:system-ui,sans-serif;";

  const header = document.createElement("div");
  header.style.cssText = "padding:8px 12px;border-bottom:1px solid #1e2d3f;display:flex;gap:8px;align-items:center;";
  const title = document.createElement("span");
  title.style.cssText = "font-size:14px;font-weight:600;";
  title.textContent = "Library";
  header.appendChild(title);
  el.appendChild(header);

  const tabs = document.createElement("div");
  tabs.style.cssText = "display:flex;border-bottom:1px solid #1e2d3f;";
  const savedTab = document.createElement("button");
  savedTab.textContent = "Saved";
  savedTab.style.cssText = "flex:1;padding:6px;background:#14202e;border:none;color:#4a9eff;font-size:12px;cursor:pointer;border-bottom:2px solid #4a9eff;";
  const historyTab = document.createElement("button");
  historyTab.textContent = "History";
  historyTab.style.cssText = "flex:1;padding:6px;background:transparent;border:none;color:#6b7d8f;font-size:12px;cursor:pointer;";
  tabs.appendChild(savedTab);
  tabs.appendChild(historyTab);
  el.appendChild(tabs);

  const searchBox = document.createElement("input");
  searchBox.type = "text";
  searchBox.placeholder = "Search library...";
  searchBox.style.cssText = "padding:6px 10px;margin:8px;background:#14202e;border:1px solid #2b3d52;border-radius:4px;color:#e5e9f0;font-size:13px;width:calc(100% - 16px);box-sizing:border-box;";
  el.appendChild(searchBox);

  const list = document.createElement("div");
  list.className = "library-list";
  list.style.cssText = "flex:1;overflow-y:auto;";
  el.appendChild(list);

  let currentKind = "saved";
  let allEntries: LibraryEntry[] = [];

  const refresh = async () => {
    try {
      const result = await invoke<LibraryListResult>("cove://commands/library.list", { workspaceId, kind: currentKind });
      allEntries = result.entries || [];
      renderList();
    } catch (e) {
      list.innerHTML = `<div style="padding:20px;color:#ef4444;">Failed: ${(e as Error).message}</div>`;
    }
  };

  const renderList = () => {
    list.innerHTML = "";
    let filtered = allEntries;
    const query = searchBox.value.trim().toLowerCase();
    if (query) {
      filtered = allEntries
        .map(e => ({
          entry: e,
          score: fuzzyScore(e.title?.toLowerCase() || "", e.paneType.toLowerCase(), query, e.workspaceId === workspaceId),
        }))
        .filter(x => x.score > 0)
        .sort((a, b) => b.score - a.score)
        .map(x => x.entry);
    }

    if (filtered.length === 0) {
      const empty = document.createElement("div");
      empty.style.cssText = "padding:20px;color:#6b7d8f;text-align:center;font-size:13px;";
      empty.textContent = query ? "No matching entries" : "No entries";
      list.appendChild(empty);
      return;
    }

    for (const entry of filtered) {
      list.appendChild(buildEntryRow(entry, workspaceId));
    }
  };

  savedTab.addEventListener("click", () => {
    currentKind = "saved";
    savedTab.style.borderBottom = "2px solid #4a9eff";
    savedTab.style.color = "#4a9eff";
    historyTab.style.borderBottom = "none";
    historyTab.style.color = "#6b7d8f";
    refresh();
  });

  historyTab.addEventListener("click", () => {
    currentKind = "history";
    historyTab.style.borderBottom = "2px solid #4a9eff";
    historyTab.style.color = "#4a9eff";
    savedTab.style.borderBottom = "none";
    savedTab.style.color = "#6b7d8f";
    refresh();
  });

  searchBox.addEventListener("input", renderList);

  await refresh();
  return el;
}

function buildEntryRow(entry: LibraryEntry, workspaceId: string): HTMLElement {
  const row = document.createElement("div");
  row.style.cssText = "padding:8px 12px;border-bottom:1px solid #14202e;cursor:pointer;display:flex;gap:8px;align-items:center;";
  row.addEventListener("mouseenter", () => row.style.background = "#14202e");
  row.addEventListener("mouseleave", () => row.style.background = "");

  const icon = document.createElement("span");
  icon.style.cssText = "font-size:16px;";
  icon.textContent = entry.paneType === "terminal" ? "💻" : entry.paneType === "browser" ? "🌐" : "📄";
  row.appendChild(icon);

  const info = document.createElement("div");
  info.style.cssText = "flex:1;min-width:0;";

  const title = document.createElement("div");
  title.style.cssText = "font-size:13px;color:#e5e9f0;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;";
  title.textContent = entry.title || entry.paneType;
  info.appendChild(title);

  const meta = document.createElement("div");
  meta.style.cssText = "font-size:11px;color:#6b7d8f;";
  const date = new Date(entry.capturedAt);
  meta.textContent = `${entry.paneType} · ${date.toLocaleDateString()}`;
  if (entry.workspaceId === workspaceId) {
    meta.textContent += " · active";
  }
  info.appendChild(meta);

  row.appendChild(info);

  row.addEventListener("click", () => {
    invoke("cove://commands/library.materialize", { workspaceId, entryId: entry.id }).catch(e => {
      console.error("Materialize failed:", e);
    });
  });

  return row;
}

function fuzzyScore(title: string, paneType: string, query: string, isActiveWorkspace: boolean): number {
  let score = 0;
  let qi = 0;
  for (let i = 0; i < title.length && qi < query.length; i++) {
    if (title[i] === query[qi]) {
      score += 10;
      qi++;
    }
  }
  if (qi < query.length) {
    for (let i = 0; i < paneType.length && qi < query.length; i++) {
      if (paneType[i] === query[qi]) {
        score += 2;
        qi++;
      }
    }
  }
  if (qi < query.length) return 0;
  if (isActiveWorkspace) score += 50;
  return score;
}
