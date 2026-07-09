export interface ToolbarTile {
  id: string;
  label: string;
  letter: string;
  action: string;
  icon: string;
}

export function toolbarTiles(): ToolbarTile[] {
  return [
    { id: "terminal", label: "Terminal", letter: "T", action: "room.new", icon: "▌" },
    { id: "browser", label: "Browser", letter: "B", action: "tool.browser", icon: "◑" },
    { id: "search", label: "Search", letter: "F", action: "tool.search", icon: "⌕" },
    { id: "git", label: "Source Control", letter: "G", action: "tool.git", icon: "⎇" },
    { id: "tasks", label: "Tasks", letter: "K", action: "tool.tasks", icon: "▤" },
    { id: "notepad", label: "Notepad", letter: "N", action: "tool.notepad", icon: "✎" },
  ];
}
