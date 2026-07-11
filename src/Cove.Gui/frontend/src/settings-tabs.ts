export function orderSettingsTabs(schemaTabs: string[]): string[] {
  const tabs = schemaTabs.includes("theme")
    ? (schemaTabs.includes("keyboard") ? [...schemaTabs] : ["theme", "keyboard", ...schemaTabs])
    : (schemaTabs.includes("keyboard") ? ["theme", ...schemaTabs] : ["theme", "keyboard", ...schemaTabs]);
  if (tabs.length === 0) return tabs;
  if (!tabs.includes("tools")) tabs.push("tools");
  return tabs;
}

export function settingsTabLabel(tab: string): string {
  return tab.charAt(0).toUpperCase() + tab.slice(1);
}

export function resolveActiveSettingsTab(tabs: string[], current: string | null): string | null {
  if (tabs.length === 0) return null;
  if (!current || !tabs.includes(current)) return tabs[0];
  return current;
}
