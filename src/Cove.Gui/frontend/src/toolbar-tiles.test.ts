import { describe, it, expect } from "vitest";
import { toolbarTiles, type ToolbarTile } from "./toolbar-tiles";

describe("toolbarTiles", () => {
  const tiles = toolbarTiles();
  it("exposes the spec pane-type tiles in order T B ⌕ G K N", () => {
    expect(tiles.map((t: ToolbarTile) => t.letter)).toEqual(["T", "B", "F", "G", "K", "N"]);
  });
  it("routes each tile to a pane-creating action", () => {
    const byId = Object.fromEntries(tiles.map((t) => [t.id, t.action]));
    expect(byId.terminal).toBe("room.new");
    expect(byId.browser).toBe("tool.browser");
    expect(byId.search).toBe("tool.search");
    expect(byId.git).toBe("tool.git");
    expect(byId.tasks).toBe("tool.tasks");
    expect(byId.notepad).toBe("tool.notepad");
  });
  it("gives every tile a label and an icon", () => {
    for (const t of tiles) {
      expect(t.label.length).toBeGreaterThan(0);
      expect(t.icon.length).toBeGreaterThan(0);
    }
  });
});
