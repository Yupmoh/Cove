export interface SettingsTabMetadata { group: "Workspace" | "Experience" | "Tools" | "System" | "Other"; label: string; icon: string; description: string; }

const CANONICAL_TABS: Record<string, SettingsTabMetadata> = {
  bay: { group: "Workspace", label: "Bay", icon: "bays", description: "Configure Bay behavior and workspace defaults." },
  theme: { group: "Experience", label: "Theme", icon: "image", description: "Choose, preview, and customize Cove's colors." },
  appearance: { group: "Experience", label: "Appearance", icon: "overview", description: "Adjust Cove's layout and visual presentation." },
  keyboard: { group: "Experience", label: "Keyboard", icon: "tasks", description: "Review and customize keyboard shortcuts." },
  terminal: { group: "Experience", label: "Terminal", icon: "terminal", description: "Configure terminal rendering and session behavior." },
  dictation: { group: "Experience", label: "Dictation", icon: "activity", description: "Configure voice input and dictation behavior." },
  tools: { group: "Tools", label: "Tools", icon: "agents", description: "Manage command-line tools and launch profiles." },
  updates: { group: "System", label: "Updates", icon: "refresh", description: "Check for updates and review update status." },
  diagnostics: { group: "System", label: "Diagnostics", icon: "inspect", description: "Inspect diagnostics and troubleshooting data." },
};

const CANONICAL_ORDER = ["bay", "theme", "appearance", "keyboard", "terminal", "dictation", "tools", "updates", "diagnostics"];

export function orderSettingsTabs(schemaTabs: string[]): string[] {
  const available = new Set(schemaTabs);
  available.add("theme");
  available.add("keyboard");
  available.add("dictation");
  available.add("tools");
  const ordered = CANONICAL_ORDER.filter((tab) => available.delete(tab));
  return [...ordered, ...schemaTabs.filter((tab, index) => available.has(tab) && schemaTabs.indexOf(tab) === index)];
}

export function settingsTabLabel(tab: string): string {
  return tab.charAt(0).toUpperCase() + tab.slice(1);
}

export function settingsTabMetadata(tab: string): SettingsTabMetadata {
  const known = CANONICAL_TABS[tab];
  if (known) return known;
  const label = settingsTabLabel(tab);
  return { group: "Other", label, icon: "gear", description: `Configure ${label} settings.` };
}

export function resolveActiveSettingsTab(tabs: string[], current: string | null): string | null {
  if (tabs.length === 0) return null;
  if (!current || !tabs.includes(current)) return tabs[0];
  return current;
}
