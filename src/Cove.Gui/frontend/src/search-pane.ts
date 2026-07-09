import { invoke } from "./invoke";

interface SearchMatch {
  filePath: string;
  line: number;
  column: number;
  text: string;
  context: string | null;
}

interface SearchResult {
  query: string;
  matches: SearchMatch[];
  regex: boolean;
  wholeWord: boolean;
  caseInsensitive: boolean;
}

export async function renderSearchPane(workspaceId: string): Promise<HTMLElement> {
  const el = document.createElement("div");
  el.className = "search-pane";
  el.style.cssText = "display:flex;flex-direction:column;height:100%;background:#0d1117;color:#e6edf3;font-family:system-ui,sans-serif;";

  const header = document.createElement("div");
  header.style.cssText = "padding:8px 12px;border-bottom:1px solid #21262d;";
  const title = document.createElement("span");
  title.style.cssText = "font-size:14px;font-weight:600;";
  title.textContent = "Search";
  header.appendChild(title);
  el.appendChild(header);

  const searchBox = document.createElement("div");
  searchBox.style.cssText = "padding:8px 12px;border-bottom:1px solid #21262d;";

  const inputRow = document.createElement("div");
  inputRow.style.cssText = "display:flex;gap:4px;";
  const input = document.createElement("input");
  input.type = "text";
  input.placeholder = "Search in files...";
  input.style.cssText = "flex:1;padding:6px 10px;background:#161b22;border:1px solid #30363d;border-radius:4px;color:#e6edf3;font-size:13px;";
  inputRow.appendChild(input);
  const searchBtn = document.createElement("button");
  searchBtn.textContent = "Search";
  searchBtn.style.cssText = "padding:6px 14px;background:#1f6feb;border:none;color:#fff;border-radius:4px;font-size:12px;cursor:pointer;";
  inputRow.appendChild(searchBtn);
  searchBox.appendChild(inputRow);

  const toggles = document.createElement("div");
  toggles.style.cssText = "display:flex;gap:12px;margin-top:6px;font-size:11px;";
  const caseToggle = createToggle("Aa", "Case sensitive", false);
  const wordToggle = createToggle("W", "Whole word", false);
  const regexToggle = createToggle(".*", "Regex", false);
  toggles.appendChild(caseToggle.el);
  toggles.appendChild(wordToggle.el);
  toggles.appendChild(regexToggle.el);
  searchBox.appendChild(toggles);

  const globRow = document.createElement("div");
  globRow.style.cssText = "display:flex;gap:4px;margin-top:4px;";
  const includeInput = document.createElement("input");
  includeInput.type = "text";
  includeInput.placeholder = "Include: *.ts,*.cs";
  includeInput.style.cssText = "flex:1;padding:4px 8px;background:#161b22;border:1px solid #30363d;border-radius:3px;color:#e6edf3;font-size:11px;";
  const excludeInput = document.createElement("input");
  excludeInput.type = "text";
  excludeInput.placeholder = "Exclude: node_modules";
  excludeInput.style.cssText = "flex:1;padding:4px 8px;background:#161b22;border:1px solid #30363d;border-radius:3px;color:#e6edf3;font-size:11px;";
  globRow.appendChild(includeInput);
  globRow.appendChild(excludeInput);
  searchBox.appendChild(globRow);

  const replaceRow = document.createElement("div");
  replaceRow.style.cssText = "display:flex;gap:4px;margin-top:4px;";
  const replaceInput = document.createElement("input");
  replaceInput.type = "text";
  replaceInput.placeholder = "Replace with...";
  replaceInput.style.cssText = "flex:1;padding:4px 8px;background:#161b22;border:1px solid #30363d;border-radius:3px;color:#e6edf3;font-size:11px;";
  replaceRow.appendChild(replaceInput);
  const replaceBtn = document.createElement("button");
  replaceBtn.textContent = "Replace All";
  replaceBtn.style.cssText = "padding:4px 10px;background:#1f6feb;border:none;color:#fff;border-radius:3px;font-size:11px;cursor:pointer;";
  replaceRow.appendChild(replaceBtn);
  searchBox.appendChild(replaceRow);
  el.appendChild(searchBox);

  const resultsEl = document.createElement("div");
  resultsEl.style.cssText = "flex:1;overflow-y:auto;";
  el.appendChild(resultsEl);

  const doSearch = async () => {
    const query = input.value.trim();
    if (!query) return;

    resultsEl.innerHTML = `<div style="padding:20px;color:#6e7681;text-align:center;font-size:13px;">Searching...</div>`;

    try {
      const result = await invoke<SearchResult>("cove://commands/search.query", {
        query,
        path: workspaceId,
        regex: regexToggle.value,
        wholeWord: wordToggle.value,
        caseInsensitive: !caseToggle.value,
        includeGlob: includeInput.value || null,
        excludeGlob: excludeInput.value || null,
      });
      renderResults(result, resultsEl);
    } catch (e) {
      resultsEl.innerHTML = `<div style="padding:20px;color:#f85149;">Search failed: ${(e as Error).message}</div>`;
    }
  };

  const doReplace = async () => {
    const query = input.value.trim();
    const replacement = replaceInput.value;
    if (!query) return;
    try {
      const searchResult = await invoke<SearchResult>("cove://commands/search.query", {
        query,
        path: workspaceId,
        regex: regexToggle.value,
        wholeWord: wordToggle.value,
        caseInsensitive: !caseToggle.value,
        includeGlob: includeInput.value || null,
        excludeGlob: excludeInput.value || null,
      });
      const files = [...new Set(searchResult.matches.map((m) => m.filePath))];
      if (files.length === 0) {
        resultsEl.innerHTML = `<div style="padding:20px;color:#6e7681;font-size:13px;">No matches to replace.</div>`;
        return;
      }
      const replaceResult = await invoke<{ results: { filePath: string; replacements: number; saved: boolean }[] }>("cove://commands/search.replace", {
        search: query,
        replacement,
        files,
        regex: regexToggle.value,
        wholeWord: wordToggle.value,
        caseInsensitive: !caseToggle.value,
      });
      const changed = replaceResult.results.filter((r) => r.saved).length;
      resultsEl.innerHTML = `<div style="padding:20px;color:#3fb950;font-size:13px;">Replaced in ${changed} file(s).</div>`;
    } catch (e) {
      resultsEl.innerHTML = `<div style="padding:20px;color:#f85149;">Replace failed: ${(e as Error).message}</div>`;
    }
    await doSearch();
  };

  replaceBtn.addEventListener("click", doReplace);

  searchBtn.addEventListener("click", doSearch);
  input.addEventListener("keydown", (e) => {
    if (e.key === "Enter") doSearch();
  });

  return el;
}

function createToggle(label: string, title: string, initial: boolean): { el: HTMLElement; value: boolean } {
  const btn = document.createElement("button");
  btn.textContent = label;
  btn.title = title;
  let value = initial;
  const update = () => {
    btn.style.cssText = `padding:2px 6px;border:1px solid ${value ? "#58a6ff" : "#30363d"};background:${value ? "#1f6feb33" : "transparent"};color:${value ? "#58a6ff" : "#6e7681"};border-radius:3px;cursor:pointer;font-size:11px;font-weight:600;`;
  };
  update();
  btn.addEventListener("click", () => { value = !value; update(); });
  return { el: btn, get value() { return value; } };
}

function renderResults(result: SearchResult, container: HTMLElement): void {
  container.innerHTML = "";

  if (result.matches.length === 0) {
    const empty = document.createElement("div");
    empty.style.cssText = "padding:20px;color:#6e7681;text-align:center;font-size:13px;";
    empty.textContent = "No results";
    container.appendChild(empty);
    return;
  }

  const header = document.createElement("div");
  header.style.cssText = "padding:6px 12px;font-size:11px;color:#6e7681;border-bottom:1px solid #21262d;";
  header.textContent = `${result.matches.length} results`;
  container.appendChild(header);

  const grouped = new Map<string, SearchMatch[]>();
  for (const match of result.matches) {
    if (!grouped.has(match.filePath)) grouped.set(match.filePath, []);
    grouped.get(match.filePath)!.push(match);
  }

  for (const [filePath, matches] of grouped) {
    const group = document.createElement("div");
    group.style.cssText = "border-bottom:1px solid #161b22;";

    const fileHeader = document.createElement("div");
    fileHeader.style.cssText = "padding:4px 12px;font-size:12px;color:#58a6ff;cursor:pointer;";
    fileHeader.textContent = `${filePath} (${matches.length})`;
    group.appendChild(fileHeader);

    for (const match of matches) {
      const row = document.createElement("div");
      row.style.cssText = "padding:2px 12px 2px 24px;font-family:ui-monospace,monospace;font-size:12px;cursor:pointer;display:flex;gap:8px;";
      row.addEventListener("mouseenter", () => row.style.background = "#161b22");
      row.addEventListener("mouseleave", () => row.style.background = "");

      const lineNum = document.createElement("span");
      lineNum.style.cssText = "color:#6e7681;min-width:40px;";
      lineNum.textContent = `${match.line}:${match.column}`;
      row.appendChild(lineNum);

      const text = document.createElement("span");
      text.style.cssText = "color:#e6edf3;white-space:pre;overflow:hidden;text-overflow:ellipsis;";
      text.textContent = match.text;
      row.appendChild(text);

      group.appendChild(row);
    }
    container.appendChild(group);
  }
}
