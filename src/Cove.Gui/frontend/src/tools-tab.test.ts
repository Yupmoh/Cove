import { describe, expect, it } from "vitest";
import {
  adapterCardSubtitle,
  adapterStatusMeta,
  toolsSubtitle,
  retentionChipVisible,
  retentionChipLabel,
  projectToolsAdapters,
  type ToolsRetention,
} from "./tools-tab";

describe("adapterStatusMeta", () => {
  it("maps detected to a green swatch", () => {
    expect(adapterStatusMeta("detected")).toEqual({ label: "detected", cssColor: "#5fc08a" });
  });

  it("maps broken to amber", () => {
    expect(adapterStatusMeta("broken")).toEqual({ label: "broken", cssColor: "#e0a44a" });
  });

  it("maps missing to muted", () => {
    expect(adapterStatusMeta("missing")).toEqual({ label: "missing", cssColor: "var(--muted)" });
  });

  it("maps null and undefined to unknown", () => {
    expect(adapterStatusMeta(null)).toEqual({ label: "unknown", cssColor: "var(--muted)" });
    expect(adapterStatusMeta(undefined)).toEqual({ label: "unknown", cssColor: "var(--muted)" });
  });
});

describe("adapterCardSubtitle", () => {
  it("joins version and binary path", () => {
    expect(adapterCardSubtitle("1.2.3", "/usr/local/bin/x")).toBe("v1.2.3 · /usr/local/bin/x");
  });

  it("handles a missing version", () => {
    expect(adapterCardSubtitle(null, "/usr/local/bin/x")).toBe("version unknown · /usr/local/bin/x");
  });

  it("handles a missing binary path", () => {
    expect(adapterCardSubtitle("1.2.3", null)).toBe("v1.2.3 · binary not found");
  });

  it("handles both missing", () => {
    expect(adapterCardSubtitle(null, undefined)).toBe("version unknown · binary not found");
  });
});

describe("toolsSubtitle", () => {
  it("shows version and path when detected", () => {
    expect(toolsSubtitle("detected", "1.2.3", "/usr/local/bin/x", "hint")).toBe("v1.2.3 · /usr/local/bin/x");
  });

  it("shows the install hint when not detected", () => {
    expect(toolsSubtitle("missing", null, null, "npm i -g x")).toBe("not found · npm i -g x");
  });

  it("falls back to plain not-found with no hint", () => {
    expect(toolsSubtitle("broken", null, null, "   ")).toBe("not found");
  });
});

describe("retentionChipVisible", () => {
  const base: ToolsRetention = { present: true, editable: true, hidden: false, value: "7", recommended: "30" };

  it("visible when present and not hidden", () => {
    expect(retentionChipVisible(base)).toBe(true);
  });

  it("hidden at or above recommended", () => {
    expect(retentionChipVisible({ ...base, hidden: true })).toBe(false);
  });

  it("absent when not present", () => {
    expect(retentionChipVisible({ ...base, present: false })).toBe(false);
    expect(retentionChipVisible(null)).toBe(false);
    expect(retentionChipVisible(undefined)).toBe(false);
  });
});

describe("retentionChipLabel", () => {
  it("shows the current value", () => {
    expect(retentionChipLabel({ present: true, editable: true, hidden: false, value: "7", recommended: "30" })).toBe("Retention: 7");
  });

  it("falls back to default when empty", () => {
    expect(retentionChipLabel({ present: true, editable: false, hidden: false, value: null, recommended: null })).toBe("Retention: default");
  });
});

describe("projectToolsAdapters", () => {
  const retention: ToolsRetention = { present: false, editable: false, hidden: false, value: null, recommended: null };
  const adapter = (name: string, status: string | null, installHint = ""): import("./tools-tab").ToolsAdapter => ({
    name, displayName: name, accent: "#abcdef", binary: name, status, version: null, binaryPath: null,
    iconSvg: null, installHint, bundled: true, removable: false, retention,
  });

  it("partitions adapters by canonical status in stable exhaustive order", () => {
    const detected = adapter("detected", "detected", "install detected");
    const missing = adapter("missing", "missing", "install missing");
    const broken = adapter("broken", "broken", "install broken");
    const unknown = adapter("unknown", "future", "install unknown");
    const unavailable = adapter("unavailable", "missing", "  ");
    const result = projectToolsAdapters([detected, missing, broken, unknown, unavailable]);

    expect(result.installed).toEqual([detected, broken]);
    expect(result.available).toEqual([missing, unknown]);
    expect(result.unavailable).toEqual([unavailable]);
    expect([...result.installed, ...result.available, ...result.unavailable]).toHaveLength(5);
  });
});
