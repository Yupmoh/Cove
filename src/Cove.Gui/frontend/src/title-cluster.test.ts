import { describe, it, expect } from "vitest";
import { clusterTools, type ClusterTool } from "./title-cluster";

describe("clusterTools", () => {
  it("always exposes the find-anything entry and the settings tool", () => {
    const ids = clusterTools({ updateStaged: false }).map((t: ClusterTool) => t.id);
    expect(ids).toContain("find-anything");
    expect(ids).toContain("settings");
  });
  it("routes find-anything to the command palette", () => {
    const find = clusterTools({ updateStaged: false }).find((t) => t.id === "find-anything")!;
    expect(find.action).toBe("tool.palette");
  });
  it("places app zoom controls between inspect and settings", () => {
    const ids = clusterTools({ updateStaged: false }).map((t: ClusterTool) => t.id);
    expect(ids.indexOf("inspect")).toBeLessThan(ids.indexOf("zoom-out"));
    expect(ids.indexOf("zoom-out")).toBeLessThan(ids.indexOf("zoom-in"));
    expect(ids.indexOf("zoom-in")).toBeLessThan(ids.indexOf("settings"));
    const zo = clusterTools({ updateStaged: false }).find((t) => t.id === "zoom-out")!;
    expect(zo.action).toBe("app.zoom-out");
  });
  it("hides the update affordance until an update is staged", () => {
    expect(clusterTools({ updateStaged: false }).some((t) => t.id === "update")).toBe(false);
    expect(clusterTools({ updateStaged: true }).some((t) => t.id === "update")).toBe(true);
  });
  it("only surfaces visible tools", () => {
    for (const t of clusterTools({ updateStaged: true })) {
      expect(t.visible).toBe(true);
    }
  });
});
