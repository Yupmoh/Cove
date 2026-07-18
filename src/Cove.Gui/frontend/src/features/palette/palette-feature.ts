import type { CoveAction } from "../../app/action-registry";
import { FrontendCommand } from "../../app/frontend-command";
import { LifecycleScope, type ComponentHandle } from "../../app/lifecycle";
import {
  categoryLabel,
  filterAndSort,
  MruTracker,
  parseQuery,
  type PaletteItem,
} from "../../omni-palette";

interface PaletteActionDetails {
  label: string;
  icon: string;
  key?: string;
}

export type PaletteAction = PaletteActionDetails & (
  | { kind: "action"; action: CoveAction }
  | { kind: "callback"; run(): void }
);

export interface PaletteNook {
  id: string;
  title: string;
  focus(): void;
}

export interface PaletteFeatureDependencies {
  document: Document;
  storage: Storage;
  root: HTMLElement;
  input: HTMLInputElement;
  list: HTMLElement;
  commandActions(): PaletteAction[];
  shoreActions(): PaletteAction[];
  nooks(): PaletteNook[];
  invoke<T>(command: FrontendCommand, args: unknown): Promise<T>;
  switchBay(id: string): void | Promise<void>;
  openTask(id: string): void | Promise<void>;
  openFile(path: string): void | Promise<void>;
  splitActive(): void | Promise<void>;
  focusActiveNook(): void;
  dispatchAction(action: CoveAction): void;
}

export interface PaletteFeature extends ComponentHandle {
  readonly isOpen: boolean;
  open(): void;
  close(): void;
  toggle(): void;
}

const DEFAULT_BAY_ID = "default";

export function createPaletteFeature(dependencies: PaletteFeatureDependencies): PaletteFeature {
  const lifecycle = new LifecycleScope();
  const { document, storage, root, input, list } = dependencies;
  let selected = 0;
  let visibleActions: PaletteItem[] = [];
  let cachedItems: PaletteItem[] | null = null;
  let fileSearchTimer: ReturnType<typeof globalThis.setTimeout> | null = null;
  let fileResults: PaletteItem[] = [];
  let fileQuery = "";
  let generation = 0;
  let disposed = false;
  let mruEntries: unknown = [];
  try {
    mruEntries = JSON.parse(storage.getItem("cove.palette.mru") ?? "[]");
  } catch (error) {
    console.warn("palette MRU could not be decoded", error);
  }
  const mru = new MruTracker(Array.isArray(mruEntries) ? mruEntries : []);

  const persistMru = (): void => {
    try {
      storage.setItem("cove.palette.mru", JSON.stringify(mru.toList()));
    } catch (error) {
      console.warn("palette MRU could not be persisted", error);
    }
  };

  const close = (): void => {
    root.classList.remove("open");
    dependencies.focusActiveNook();
  };

  const commandRunner = (action: PaletteAction): (() => void) => {
    if (action.kind === "callback") return action.run;
    const target = action.action;
    return () => dependencies.dispatchAction(target);
  };

  const loadItems = async (loadGeneration: number): Promise<PaletteItem[]> => {
    const items: PaletteItem[] = [];
    for (const action of dependencies.commandActions()) {
      items.push({
        id: `cmd:${action.label}`,
        label: action.label,
        category: "commands",
        icon: action.icon,
        key: action.key,
        run: commandRunner(action),
      });
    }
    for (const action of dependencies.shoreActions()) {
      items.push({
        id: `shore:${action.label}`,
        label: action.label,
        category: "shores",
        icon: action.icon,
        key: action.key,
        run: commandRunner(action),
      });
    }
    for (const nook of dependencies.nooks()) {
      items.push({
        id: `nook:${nook.id}`,
        label: nook.title || nook.id,
        category: "nooks",
        icon: "\u25a0",
        run: nook.focus,
      });
    }
    try {
      const result = await dependencies.invoke<{ bays: { id: string; name: string }[] }>(FrontendCommand.BayList, {});
      for (const bay of result.bays ?? []) {
        items.push({
          id: `ws:${bay.id}`,
          label: bay.name,
          category: "bays",
          icon: "\u25c8",
          run: () => void dependencies.switchBay(bay.id),
        });
      }
    } catch (error) {
      console.warn("palette bay list failed", error);
    }
    try {
      const result = await dependencies.invoke<{ cards: { id: string; title: string; humanId: string }[] }>(
        FrontendCommand.TaskList,
        { bayId: DEFAULT_BAY_ID },
      );
      for (const task of result.cards ?? []) {
        items.push({
          id: `task:${task.id}`,
          label: `${task.humanId}: ${task.title}`,
          category: "tasks",
          icon: "#",
          run: () => void dependencies.openTask(task.id),
        });
      }
    } catch (error) {
      console.warn("palette task list failed", { bayId: DEFAULT_BAY_ID, error });
    }
    if (disposed || loadGeneration !== generation) return [];
    return items;
  };

  const searchFiles = async (query: string, searchGeneration: number): Promise<void> => {
    if (disposed || searchGeneration !== generation) return;
    fileQuery = query;
    try {
      const result = await dependencies.invoke<{ matches: { file: string; line: number; text: string }[] }>(
        FrontendCommand.SearchQuery,
        { query, bayId: DEFAULT_BAY_ID },
      );
      if (disposed || searchGeneration !== generation) return;
      const seen = new Set<string>();
      fileResults = (result.matches ?? []).filter((match) => {
        if (seen.has(match.file)) return false;
        seen.add(match.file);
        return true;
      }).slice(0, 20).map((match) => ({
        id: `file:${match.file}`,
        label: match.file,
        category: "files" as const,
        icon: "/",
        run: () => void dependencies.openFile(match.file),
      }));
      render();
    } catch (error) {
      if (searchGeneration === generation) fileResults = [];
      console.warn("palette file search failed", { bayId: DEFAULT_BAY_ID, query, error });
    }
  };

  const render = (): void => {
    if (disposed) return;
    const parsed = parseQuery(input.value);
    visibleActions = filterAndSort(cachedItems ?? [], parsed);
    if (parsed.category === "files" && parsed.text.length > 0 && parsed.text !== fileQuery) {
      if (fileSearchTimer !== null) globalThis.clearTimeout(fileSearchTimer);
      const searchGeneration = generation;
      fileSearchTimer = globalThis.setTimeout(() => {
        fileSearchTimer = null;
        void searchFiles(parsed.text, searchGeneration);
      }, 200);
    }
    if (parsed.category === "files") {
      visibleActions = [
        ...visibleActions,
        ...fileResults.filter((file) => !visibleActions.some((action) => action.id === file.id)),
      ];
    }
    if (parsed.text.length === 0 && parsed.category === "all") {
      const mruIds = mru.toList().map((entry) => entry.id).reverse();
      const recent = mruIds
        .map((id) => visibleActions.find((item) => item.id === id))
        .filter((item): item is PaletteItem => item !== undefined);
      visibleActions = [...recent, ...visibleActions.filter((item) => !mruIds.includes(item.id))];
    }
    if (selected >= visibleActions.length) selected = Math.max(0, visibleActions.length - 1);
    list.replaceChildren();
    if (parsed.category !== "all" || parsed.text.length > 0) {
      const category = document.createElement("div");
      category.className = "pal-cat-bar";
      category.style.cssText = "display:flex;gap:4px;padding:4px 8px;border-bottom:1px solid var(--border);font-size:11px;color:var(--muted);";
      category.textContent = parsed.category === "all"
        ? `Results for "${parsed.text}"`
        : `${categoryLabel(parsed.category)}: "${parsed.text}"`;
      list.appendChild(category);
    }
    if (visibleActions.length === 0) {
      const empty = document.createElement("div");
      empty.className = "pal-empty";
      empty.style.cssText = "padding:16px;text-align:center;color:var(--muted);font-size:12px;";
      empty.textContent = cachedItems === null ? "Loading..." : "No results";
      list.appendChild(empty);
      return;
    }
    visibleActions.forEach((action, index) => {
      const row = document.createElement("div");
      row.className = "pal-item" + (index === selected ? " sel" : "");
      const icon = document.createElement("span");
      icon.className = "ic";
      icon.textContent = action.icon;
      const label = document.createElement("span");
      label.className = "lbl";
      label.textContent = action.label;
      row.append(icon, label);
      if (action.key) {
        const key = document.createElement("span");
        key.className = "key";
        key.textContent = action.key;
        row.appendChild(key);
      }
      row.addEventListener("click", (event) => {
        const split = event.metaKey || event.ctrlKey;
        close();
        mru.record(action.id);
        persistMru();
        action.run();
        if (split) void dependencies.splitActive();
      });
      list.appendChild(row);
    });
  };

  const open = (): void => {
    if (disposed) throw new Error("PaletteFeature is disposed");
    root.classList.add("open");
    input.value = "";
    selected = 0;
    cachedItems = null;
    fileResults = [];
    fileQuery = "";
    const loadGeneration = ++generation;
    void loadItems(loadGeneration).then((items) => {
      if (disposed || loadGeneration !== generation) return;
      cachedItems = items;
      render();
    });
    render();
    input.focus();
  };

  lifecycle.listen(input, "input", () => {
    selected = 0;
    render();
  });
  lifecycle.listen(input, "keydown", (rawEvent) => {
    const event = rawEvent as KeyboardEvent;
    if (event.key === "Escape") {
      event.preventDefault();
      close();
    } else if (event.key === "Enter") {
      event.preventDefault();
      const action = visibleActions[selected];
      const split = event.metaKey || event.ctrlKey;
      if (!action) {
        console.warn("palette action requested without a selection");
        close();
        return;
      }
      mru.record(action.id);
      persistMru();
      close();
      action.run();
      if (split) void dependencies.splitActive();
    } else if (event.key === "ArrowDown") {
      event.preventDefault();
      selected = Math.min(visibleActions.length - 1, selected + 1);
      render();
    } else if (event.key === "ArrowUp") {
      event.preventDefault();
      selected = Math.max(0, selected - 1);
      render();
    }
  });
  lifecycle.listen(root, "mousedown", (event) => {
    if (event.target === root) close();
  });

  return {
    get isOpen() {
      return root.classList.contains("open");
    },
    open,
    close,
    toggle() {
      if (root.classList.contains("open")) close();
      else open();
    },
    async dispose() {
      if (disposed) return;
      disposed = true;
      generation += 1;
      if (fileSearchTimer !== null) globalThis.clearTimeout(fileSearchTimer);
      fileSearchTimer = null;
      root.classList.remove("open");
      await lifecycle.dispose();
    },
  };
}
