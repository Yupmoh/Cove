import { describe, it, expect } from "vitest";
import {
  initialSidebarModel,
  selectMode,
  toggleSide,
  setCollapsed,
  setWidth,
  clampWidth,
  oppositeSide,
  modeOf,
  collapsedOf,
  SIDEBAR_MIN_WIDTH,
  SIDEBAR_MAX_WIDTH,
  SIDEBAR_MODES,
} from "./sidebar-model";

describe("initialSidebarModel", () => {
  it("defaults workspaces on the left and agents on the right, both expanded", () => {
    const m = initialSidebarModel();
    expect(m.leftMode).toBe("workspaces");
    expect(m.rightMode).toBe("agents");
    expect(m.leftCollapsed).toBe(false);
    expect(m.rightCollapsed).toBe(false);
  });
});

describe("SIDEBAR_MODES", () => {
  it("lists all seven spec modes with workspaces, agents and notepad functional", () => {
    const names = SIDEBAR_MODES.map((m) => m.mode);
    expect(names).toEqual(["workspaces", "overview", "skills", "agents", "activity", "timeline", "notepad"]);
    const functional = SIDEBAR_MODES.filter((m) => m.functional).map((m) => m.mode);
    expect(functional).toEqual(["workspaces", "agents", "notepad"]);
  });
});

describe("selectMode", () => {
  it("assigns a free mode to the requested side", () => {
    const m = selectMode(initialSidebarModel(), "left", "timeline");
    expect(m.leftMode).toBe("timeline");
    expect(m.rightMode).toBe("agents");
  });
  it("atomically swaps when the mode is mounted on the opposite side", () => {
    const m = selectMode(initialSidebarModel(), "left", "agents");
    expect(m.leftMode).toBe("agents");
    expect(m.rightMode).toBe("workspaces");
  });
  it("expands the side when selecting its already-active mode", () => {
    const collapsed = setCollapsed(initialSidebarModel(), "left", true);
    const m = selectMode(collapsed, "left", "workspaces");
    expect(m.leftMode).toBe("workspaces");
    expect(m.leftCollapsed).toBe(false);
  });
  it("expands the target side after a swap", () => {
    const collapsed = setCollapsed(initialSidebarModel(), "right", true);
    const m = selectMode(collapsed, "right", "workspaces");
    expect(m.rightMode).toBe("workspaces");
    expect(m.leftMode).toBe("agents");
    expect(m.rightCollapsed).toBe(false);
  });
});

describe("toggleSide", () => {
  it("flips the collapsed flag for the given side only", () => {
    const m = toggleSide(initialSidebarModel(), "left");
    expect(m.leftCollapsed).toBe(true);
    expect(m.rightCollapsed).toBe(false);
    expect(toggleSide(m, "left").leftCollapsed).toBe(false);
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
  it("oppositeSide flips sides", () => {
    expect(oppositeSide("left")).toBe("right");
    expect(oppositeSide("right")).toBe("left");
  });
  it("modeOf and collapsedOf read the correct side", () => {
    const m = setCollapsed(initialSidebarModel(), "right", true);
    expect(modeOf(m, "right")).toBe("agents");
    expect(collapsedOf(m, "right")).toBe(true);
  });
});
