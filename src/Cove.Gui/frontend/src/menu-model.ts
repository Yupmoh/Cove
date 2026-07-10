export interface MenuLeaf {
  id?: string;
  label?: string;
  action?: string;
  role?: string;
  separator?: boolean;
  accelerator?: string;
  enabled?: boolean;
}

export interface MenuSection {
  label?: string;
  role?: string;
  items?: MenuLeaf[];
}

export interface ActionChord {
  action: string;
  chord: string;
}

const MODIFIER_ORDER = ["cmd", "ctrl", "alt", "shift"] as const;

const NAMED_KEYS: Record<string, string> = {
  up: "Up",
  down: "Down",
  left: "Left",
  right: "Right",
  enter: "Enter",
  space: "Space",
  tab: "Tab",
  backspace: "Backspace",
  escape: "Escape",
  delete: "Delete",
};

export function chordToAccelerator(chord: string): string {
  const parts = chord.toLowerCase().trim().split(/[+\s]+/).filter((p) => p.length > 0);
  if (parts.length === 0) return "";
  const out: string[] = [];
  for (const m of MODIFIER_ORDER) {
    if (parts.includes(m)) out.push(m === "cmd" ? "CmdOrCtrl" : m.charAt(0).toUpperCase() + m.slice(1));
  }
  for (const p of parts) {
    if ((MODIFIER_ORDER as readonly string[]).includes(p)) continue;
    if (NAMED_KEYS[p]) out.push(NAMED_KEYS[p]);
    else if (/^[a-z]$/.test(p)) out.push(p.toUpperCase());
    else out.push(p);
  }
  return out.join("+");
}

export function buildAcceleratorMap(bindings: ActionChord[]): Record<string, string> {
  const out: Record<string, string> = {};
  for (const b of bindings) {
    const acc = chordToAccelerator(b.chord);
    if (acc.length > 0) out[b.action] = acc;
  }
  return out;
}

export function menuIA(): MenuSection[] {
  return [
    { role: "appMenu" },
    {
      label: "File",
      items: [
        { id: "new-room", label: "New Room", action: "room.new" },
        { id: "new-workspace", label: "New Workspace…", action: "workspace.create" },
        { id: "new-browser", label: "New Browser", action: "tool.browser" },
        { separator: true },
        { id: "close-pane", label: "Close Pane", action: "pane.close" },
        { id: "close-room", label: "Close Room", action: "room.close" },
      ],
    },
    { role: "editMenu" },
    {
      label: "View",
      items: [
        { id: "toggle-sidebar", label: "Toggle Left Sidebar", action: "view.toggle-sidebar" },
        { id: "toggle-right-sidebar", label: "Toggle Right Sidebar", action: "view.toggle-notepad" },
        { id: "toggle-toolbar", label: "Toggle Toolbar", action: "view.toggle-toolbar" },
        { id: "toggle-zen", label: "Toggle Zen Mode", action: "view.zen-mode" },
        { id: "toggle-backdrop", label: "Toggle Window Backdrop", action: "view.toggle-backdrop" },
        { separator: true },
        { id: "zoom-in", label: "Zoom In", action: "view.zoom-in" },
        { id: "zoom-out", label: "Zoom Out", action: "view.zoom-out" },
        { id: "zoom-reset", label: "Reset Zoom", action: "view.zoom-reset" },
      ],
    },
    {
      label: "Pane",
      items: [
        { id: "split-right", label: "Split Right", action: "pane.split-right" },
        { id: "split-down", label: "Split Down", action: "pane.split-down" },
        { separator: true },
        { id: "next-pane", label: "Next Pane", action: "pane.focus-next" },
        { id: "prev-pane", label: "Previous Pane", action: "pane.focus-prev" },
        { separator: true },
        { id: "zoom-pane", label: "Maximize Pane", action: "pane.maximize" },
        { id: "find", label: "Find in Pane…", action: "pane.find" },
      ],
    },
    {
      label: "Room",
      items: [
        { id: "next-room", label: "Next Room", action: "room.next" },
        { id: "prev-room", label: "Previous Room", action: "room.prev" },
        { id: "pin-room", label: "Pin / Unpin Room", action: "room.pin" },
      ],
    },
    {
      label: "Tools",
      items: [
        { id: "command-palette", label: "Command Palette…", action: "tool.palette" },
        { id: "launcher", label: "Launcher…", action: "tool.launcher" },
        { separator: true },
        { id: "tool-git", label: "Source Control", action: "tool.git" },
        { id: "tool-search", label: "Search", action: "tool.search" },
        { id: "tool-tasks", label: "Tasks", action: "tool.tasks" },
        { id: "tool-library", label: "Library", action: "tool.library" },
        { separator: true },
        { id: "settings", label: "Settings…", action: "app.settings" },
      ],
    },
    { role: "windowMenu" },
    {
      label: "Help",
      items: [
        { id: "help-shortcuts", label: "Keyboard Shortcuts…", action: "app.settings" },
      ],
    },
  ];
}

export function buildMenu(bindings: ActionChord[], omitCustomAccelerators = false): MenuSection[] {
  const accel = buildAcceleratorMap(bindings);
  return menuIA().map((section) => {
    if (!section.items) return section;
    return {
      ...section,
      items: section.items.map((item) => {
        if (!item.action) return item;
        const a = accel[item.action];
        return a && !omitCustomAccelerators ? { ...item, accelerator: a } : { ...item };
      }),
    };
  });
}

export function menuActionIds(): Set<string> {
  const ids = new Set<string>();
  for (const section of menuIA()) {
    for (const item of section.items ?? []) {
      if (item.action) ids.add(item.action);
    }
  }
  return ids;
}

export function menuChordSet(bindings: ActionChord[]): Set<string> {
  const ids = menuActionIds();
  const perAction = new Map<string, string>();
  for (const b of bindings) {
    if (ids.has(b.action)) perAction.set(b.action, b.chord.toLowerCase().trim());
  }
  return new Set(perAction.values());
}
