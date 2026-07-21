import { describe, it, expect } from "vitest";
import { orderSettingsTabs, settingsTabLabel, settingsTabMetadata, resolveActiveSettingsTab } from "./settings-tabs";

describe("orderSettingsTabs", () => {
  it("prepends theme and keyboard when schema has neither", () => {
    expect(orderSettingsTabs(["appearance", "audio"])).toEqual(["theme", "appearance", "keyboard", "dictation", "tools", "audio"]);
  });

  it("does not duplicate theme when schema already has theme", () => {
    expect(orderSettingsTabs(["theme", "appearance"])).toEqual(["theme", "appearance", "keyboard", "dictation", "tools"]);
  });

  it("prepends only theme when schema already has keyboard", () => {
    expect(orderSettingsTabs(["keyboard", "appearance"])).toEqual(["theme", "appearance", "keyboard", "dictation", "tools"]);
  });

  it("uses canonical group order and preserves unknown tabs once", () => {
    expect(orderSettingsTabs(["unknown", "keyboard", "theme", "keyboard", "terminal", "bay", "updates", "diagnostics"])).toEqual([
      "bay", "theme", "keyboard", "terminal", "dictation", "tools", "updates", "diagnostics", "unknown",
    ]);
  });

  it("appends dictation and tools when absent", () => {
    expect(orderSettingsTabs(["appearance"])).toContain("tools");
    expect(orderSettingsTabs(["appearance"]).filter((t) => t === "tools")).toHaveLength(1);
    expect(orderSettingsTabs(["appearance"]).filter((t) => t === "dictation")).toHaveLength(1);
  });

  it("does not duplicate dictation or tools when schema already provides them", () => {
    expect(orderSettingsTabs(["keyboard", "tools"]).filter((t) => t === "tools")).toHaveLength(1);
    expect(orderSettingsTabs(["keyboard", "dictation"]).filter((t) => t === "dictation")).toHaveLength(1);
  });

  it("synthesizes theme and keyboard from an empty schema", () => {
    expect(orderSettingsTabs([])).toEqual(["theme", "keyboard", "dictation", "tools"]);
  });
});

describe("settingsTabMetadata", () => {
  it("provides canonical groups, labels, descriptions, and existing icon ids", () => {
    expect(settingsTabMetadata("bay")).toEqual(expect.objectContaining({ group: "Workspace", label: "Bay", icon: "bays" }));
    expect(settingsTabMetadata("theme")).toEqual(expect.objectContaining({ group: "Experience", label: "Theme", icon: "image" }));
    expect(settingsTabMetadata("tools")).toEqual(expect.objectContaining({ group: "Tools", label: "Tools", icon: "agents" }));
    expect(settingsTabMetadata("diagnostics")).toEqual(expect.objectContaining({ group: "System", label: "Diagnostics", icon: "inspect" }));
    expect(settingsTabMetadata("mystery")).toEqual({ group: "Other", label: "Mystery", icon: "gear", description: "Configure Mystery settings." });
  });
});

describe("settingsTabLabel", () => {
  it("title-cases the first letter", () => {
    expect(settingsTabLabel("appearance")).toBe("Appearance");
    expect(settingsTabLabel("audio")).toBe("Audio");
  });

  it("leaves an empty string untouched", () => {
    expect(settingsTabLabel("")).toBe("");
  });
});

describe("resolveActiveSettingsTab", () => {
  it("returns null when there are no tabs", () => {
    expect(resolveActiveSettingsTab([], "theme")).toBeNull();
  });

  it("keeps the current tab when still present", () => {
    expect(resolveActiveSettingsTab(["theme", "audio"], "audio")).toBe("audio");
  });

  it("falls back to the first tab when current is missing", () => {
    expect(resolveActiveSettingsTab(["theme", "audio"], "gone")).toBe("theme");
  });

  it("falls back to the first tab when current is null", () => {
    expect(resolveActiveSettingsTab(["theme", "audio"], null)).toBe("theme");
  });
});
