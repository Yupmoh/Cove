import { describe, it, expect } from "vitest";
import {
  initialSidebarModel,
  selectLeftMode,
  toggleSide,
  setCollapsed,
  setWidth,
  clampWidth,
  collapsedOf,
  widthOf,
  SIDEBAR_MIN_WIDTH,
  SIDEBAR_MAX_WIDTH,
  SIDEBAR_MODES,
  SIDEBAR_RAIL_MODES,
} from "./sidebar-model";

describe("initialSidebarModel", () => {
  it("defaults bays on the left, both sides expanded", () => {
    const m = initialSidebarModel();
    expect(m.leftMode).toBe("bays");
    expect(m.leftCollapsed).toBe(false);
    expect(m.rightCollapsed).toBe(false);
  });
});

describe("SIDEBAR_MODES", () => {
  it("lists the six left-rail modes with bays and notepad functional and no agents mode", () => {
    const names = SIDEBAR_MODES.map((m) => m.mode);
    expect(names).toEqual(["bays", "overview", "skills", "activity", "timeline", "notepad"]);
    expect(names).not.toContain("agents");
    const functional = SIDEBAR_MODES.filter((m) => m.functional).map((m) => m.mode);
    expect(functional).toEqual(["bays", "notepad"]);
  });

  it("shows only bays, skills, and timeline in the left rail", () => {
    expect(SIDEBAR_RAIL_MODES.map((m) => m.mode)).toEqual(["bays", "skills", "timeline"]);
  });
});

describe("selectLeftMode", () => {
  it("assigns the requested mode to the left rail", () => {
    const m = selectLeftMode(initialSidebarModel(), "timeline");
    expect(m.leftMode).toBe("timeline");
  });
  it("expands the left side when selecting a mode", () => {
    const collapsed = setCollapsed(initialSidebarModel(), "left", true);
    const m = selectLeftMode(collapsed, "notepad");
    expect(m.leftMode).toBe("notepad");
    expect(m.leftCollapsed).toBe(false);
  });
  it("never touches the right side", () => {
    const rightCollapsed = setCollapsed(initialSidebarModel(), "right", true);
    const m = selectLeftMode(rightCollapsed, "skills");
    expect(m.rightCollapsed).toBe(true);
  });
});

describe("toggleSide", () => {
  it("flips the collapsed flag for the given side only", () => {
    const m = toggleSide(initialSidebarModel(), "left");
    expect(m.leftCollapsed).toBe(true);
    expect(m.rightCollapsed).toBe(false);
    expect(toggleSide(m, "left").leftCollapsed).toBe(false);
  });
  it("toggles the right side independently", () => {
    const m = toggleSide(initialSidebarModel(), "right");
    expect(m.rightCollapsed).toBe(true);
    expect(m.leftCollapsed).toBe(false);
  });
});

describe("clampWidth / setWidth", () => {
  it("clamps below the minimum and above the maximum", () => {
    expect(clampWidth(10)).toBe(SIDEBAR_MIN_WIDTH);
    expect(clampWidth(9999)).toBe(SIDEBAR_MAX_WIDTH);
  });
  it("stores a clamped per-side width", () => {
    const m = setWidth(initialSidebarModel(), "right", 9999);
    expect(m.rightWidth).toBe(SIDEBAR_MAX_WIDTH);
    expect(m.leftWidth).toBe(initialSidebarModel().leftWidth);
  });
});

describe("helpers", () => {
  it("collapsedOf and widthOf read the correct side", () => {
    const m = setCollapsed(setWidth(initialSidebarModel(), "right", 300), "right", true);
    expect(collapsedOf(m, "right")).toBe(true);
    expect(collapsedOf(m, "left")).toBe(false);
    expect(widthOf(m, "right")).toBe(300);
  });
});
