import { FrontendCommand } from "../../app/frontend-command";
import {
  groupByBay,
  kindColor,
  kindIcon,
  moveSelection,
  selectedNote,
  type NavState,
  type NoteListItem,
} from "../../notepad-sidebar";
import { LifecycleScope, type ComponentHandle } from "../../app/lifecycle";

interface SidebarAction {
  icon: string;
  title: string;
  run: () => void;
}

export interface NotepadFeatureDependencies {
  document: Document;
  storage: Storage;
  invoke<T>(command: FrontendCommand, args: Record<string, unknown>): Promise<T>;
  isVisible(): boolean;
  rerenderSidebar(): void;
  sidebarHead(title: string, actions: SidebarAction[]): HTMLElement;
  spawnNook(params: Record<string, unknown>): Promise<{ nookId: string }>;
  createShore(nookId: string): Promise<{ shoreId: string }>;
  selectShore(shoreId: string): void;
  focusNook(nookId: string): void;
  openNote(bayId: string, noteId: string): Promise<void>;
}

export interface NotepadFeature extends ComponentHandle {
  render(container: HTMLElement): void;
  reload(): Promise<void>;
}

const DEFAULT_BAY_ID = "default";

export function createNotepadFeature(dependencies: NotepadFeatureDependencies): NotepadFeature {
  const document = dependencies.document;
  const lifecycle = new LifecycleScope();
  const pendingElementWaits = new Set<() => void>();
  lifecycle.own(() => {
    for (const cancel of pendingElementWaits) cancel();
    pendingElementWaits.clear();
  });
  let groups: { bayId: string; bayName: string; notes: NoteListItem[] }[] = [];
  let navigation: NavState = { groupIdx: -1, noteIdx: -1 };
  let loaded = false;
  let mountedContainer: HTMLElement | null = null;
  const collapsedGroups = new Set<string>(
    JSON.parse(dependencies.storage.getItem("cove.notepad.collapsedGroups") ?? "[]"),
  );

  const rerender = (): void => {
    if (!lifecycle.isDisposed && dependencies.isVisible()) dependencies.rerenderSidebar();
  };

  const load = async (): Promise<void> => {
    if (lifecycle.isDisposed) return;
    try {
      const result = await dependencies.invoke<{ notes: NoteListItem[] }>(
        FrontendCommand.NoteList,
        { bayId: DEFAULT_BAY_ID },
      );
      if (lifecycle.isDisposed) return;
      groups = groupByBay(result.notes ?? [], { [DEFAULT_BAY_ID]: "Default" });
    } catch (error) {
      if (lifecycle.isDisposed) return;
      console.warn("notepad load failed", DEFAULT_BAY_ID, error);
      groups = [];
    }
    loaded = true;
    rerender();
  };

  const waitForElement = (selector: string, timeoutMs: number): Promise<HTMLElement | null> => {
    return new Promise((resolve) => {
      if (lifecycle.isDisposed) {
        resolve(null);
        return;
      }
      const existing = document.querySelector<HTMLElement>(selector);
      if (existing) {
        resolve(existing);
        return;
      }
      const start = Date.now();
      let timer: ReturnType<typeof globalThis.setTimeout> | null = null;
      let settled = false;
      let cancel: (() => void) | null = null;
      const finish = (element: HTMLElement | null): void => {
        if (settled) return;
        settled = true;
        if (timer !== null) globalThis.clearTimeout(timer);
        timer = null;
        if (cancel) pendingElementWaits.delete(cancel);
        resolve(element);
      };
      const poll = (): void => {
        timer = null;
        if (lifecycle.isDisposed) {
          finish(null);
          return;
        }
        const element = document.querySelector<HTMLElement>(selector);
        if (element) {
          finish(element);
        } else if (Date.now() - start > timeoutMs) {
          finish(null);
        } else {
          timer = globalThis.setTimeout(poll, 50);
        }
      };
      cancel = () => finish(null);
      pendingElementWaits.add(cancel);
      timer = globalThis.setTimeout(poll, 50);
    });
  };

  const openNoteInNook = async (noteId: string, bayId: string): Promise<void> => {
    if (lifecycle.isDisposed) return;
    try {
      const spawned = await dependencies.spawnNook({
        command: "",
        cwd: "",
        inheritCwdFrom: "",
        cols: 80,
        rows: 24,
        adapter: "",
        agentName: "",
        bay: "",
        shore: "",
      });
      if (lifecycle.isDisposed) return;
      const created = await dependencies.createShore(spawned.nookId);
      if (lifecycle.isDisposed) return;
      dependencies.selectShore(created.shoreId);
      if (lifecycle.isDisposed) return;
      dependencies.focusNook(spawned.nookId);
      await waitForElement(".notepad-editor", 3000);
      if (lifecycle.isDisposed) return;
      await dependencies.openNote(bayId, noteId);
    } catch (error) {
      if (lifecycle.isDisposed) return;
      console.warn("notepad open failed", bayId, noteId, error);
    }
  };

  const createNote = async (): Promise<void> => {
    if (lifecycle.isDisposed) return;
    try {
      await dependencies.invoke(FrontendCommand.NoteCreate, {
        title: "Untitled",
        bayId: DEFAULT_BAY_ID,
        source: "user",
        content: "",
        kind: "markdown",
      });
      if (lifecycle.isDisposed) return;
      await load();
    } catch (error) {
      if (lifecycle.isDisposed) return;
      console.warn("notepad create failed", DEFAULT_BAY_ID, error);
    }
  };

  const onKey = (event: KeyboardEvent): void => {
    if (lifecycle.isDisposed) return;
    if (!dependencies.isVisible()) return;
    if (event.key === "ArrowDown") {
      event.preventDefault();
      navigation = moveSelection(groups, navigation, "down");
      rerender();
    } else if (event.key === "ArrowUp") {
      event.preventDefault();
      navigation = moveSelection(groups, navigation, "up");
      rerender();
    } else if (event.key === "Enter") {
      event.preventDefault();
      const note = selectedNote(groups, navigation);
      if (note) void openNoteInNook(note.id, note.bayId);
    }
  };

  const render = (container: HTMLElement): void => {
    if (lifecycle.isDisposed) return;
    container.appendChild(dependencies.sidebarHead("Notes", [{
      icon: "+",
      title: "New note",
      run: () => void createNote(),
    }]));
    const body = document.createElement("div");
    body.className = "sb-list ns-body";
    container.appendChild(body);
    container.tabIndex = 0;
    if (mountedContainer !== container) {
      mountedContainer?.removeEventListener("keydown", onKey);
      mountedContainer = container;
      mountedContainer.addEventListener("keydown", onKey);
    }
    if (!loaded) void load();

    if (groups.length === 0) {
      const empty = document.createElement("div");
      empty.className = "ns-empty";
      empty.innerHTML = "No notes yet<div class=\"ns-empty-action\" id=\"ns-empty-create\">Create a note</div>";
      body.appendChild(empty);
      const createAction = empty.querySelector("#ns-empty-create");
      if (createAction) createAction.addEventListener("click", () => void createNote());
      return;
    }

    for (let groupIndex = 0; groupIndex < groups.length; groupIndex++) {
      const group = groups[groupIndex];
      const groupElement = document.createElement("div");
      groupElement.className = "ns-group" + (collapsedGroups.has(group.bayId) ? " collapsed" : "");
      const head = document.createElement("div");
      head.className = "ns-group-head";
      head.innerHTML = "<span class=\"chevron\">▼</span><span class=\"ns-group-name\"></span><span class=\"ns-group-count\"></span>";
      head.querySelector(".ns-group-name")!.textContent = group.bayName;
      head.querySelector(".ns-group-count")!.textContent = String(group.notes.length);
      head.addEventListener("click", () => {
        if (lifecycle.isDisposed) return;
        if (collapsedGroups.has(group.bayId)) collapsedGroups.delete(group.bayId);
        else collapsedGroups.add(group.bayId);
        dependencies.storage.setItem("cove.notepad.collapsedGroups", JSON.stringify([...collapsedGroups]));
        rerender();
      });
      groupElement.appendChild(head);
      const notes = document.createElement("div");
      notes.className = "ns-group-notes";
      for (let noteIndex = 0; noteIndex < group.notes.length; noteIndex++) {
        const note = group.notes[noteIndex];
        const noteElement = document.createElement("div");
        const selected = groupIndex === navigation.groupIdx && noteIndex === navigation.noteIdx;
        noteElement.className = "ns-note" + (selected ? " selected" : "");
        noteElement.innerHTML = "<span class=\"ns-note-icon\"></span><span class=\"ns-note-title\"></span>";
        const icon = noteElement.querySelector<HTMLElement>(".ns-note-icon")!;
        icon.textContent = kindIcon(note.kind);
        icon.style.color = kindColor(note.kind);
        noteElement.querySelector(".ns-note-title")!.textContent = note.title || "Untitled";
        noteElement.addEventListener("click", () => {
          if (lifecycle.isDisposed) return;
          navigation = { groupIdx: groupIndex, noteIdx: noteIndex };
          void openNoteInNook(note.id, note.bayId);
          rerender();
        });
        notes.appendChild(noteElement);
      }
      groupElement.appendChild(notes);
      body.appendChild(groupElement);
    }
  };

  lifecycle.own(() => {
    mountedContainer?.removeEventListener("keydown", onKey);
    mountedContainer = null;
  });

  return { render, reload: load, dispose: () => lifecycle.dispose() };
}
