import { describe, it, expect } from "vitest";
import { buildBayTree, nookLabel, bayTreeEmptyMessage, NO_BAYS_MESSAGE, type BayTreeInput } from "./bay-tree";

function baseInput(overrides: Partial<BayTreeInput> = {}): BayTreeInput {
  return {
    bayName: "Cove",
    activeShoreId: "r1",
    focusedNookId: "p1",
    bayCollapsed: false,
    collapsedShoreIds: new Set<string>(),
    shores: [
      { id: "r1", name: "shell", leaves: [{ nookId: "p1", nookType: "terminal", title: "" }] },
      {
        id: "r2",
        name: "split",
        leaves: [
          { nookId: "p2", nookType: "terminal", title: "bash" },
          { nookId: "p3", nookType: "git", title: "" },
        ],
      },
    ],
    ...overrides,
  };
}

describe("nookLabel", () => {
  it("prefers a non-empty title", () => {
    expect(nookLabel({ nookId: "p", nookType: "terminal", title: "vim" })).toBe("vim");
  });
  it("falls back to a friendly nook-type label", () => {
    expect(nookLabel({ nookId: "p", nookType: "git", title: "" })).toBe("source control");
  });
  it("falls back to the raw type for unknown kinds", () => {
    expect(nookLabel({ nookId: "p", nookType: "custom", title: "" })).toBe("custom");
  });
});

describe("buildBayTree", () => {
  it("emits a bay root then shores with nook children including single-nook shores", () => {
    const rows = buildBayTree(baseInput());
    expect(rows.map((r) => r.kind)).toEqual(["bay", "shore", "nook", "shore", "nook", "nook"]);
    const single = rows.find((r) => r.shoreId === "r1" && r.kind === "shore")!;
    expect(single.expandable).toBe(true);
    const multi = rows.find((r) => r.shoreId === "r2" && r.kind === "shore")!;
    expect(multi.expandable).toBe(true);
    expect(multi.count).toBe(2);
  });

  it("skips placeholder empty nooks but keeps the shore row", () => {
    const rows = buildBayTree(baseInput({ shores: [{ id: "r9", name: "empty shore", leaves: [{ nookId: "e1", nookType: "empty", title: "" }] }] }));
    expect(rows.map((r) => r.kind)).toEqual(["bay", "shore"]);
    expect(rows[1].expandable).toBe(false);
  });

  it("marks the active shore and focused nook", () => {
    const rows = buildBayTree(baseInput());
    expect(rows.find((r) => r.shoreId === "r1" && r.kind === "shore")!.active).toBe(true);
    expect(rows.find((r) => r.nookId === "p1")!.active).toBe(true);
    expect(rows.filter((r) => r.kind === "nook" && r.nookId !== "p1").every((r) => r.active === false)).toBe(true);
  });

  it("hides shore children when the shore is collapsed", () => {
    const rows = buildBayTree(baseInput({ collapsedShoreIds: new Set(["r2"]) }));
    expect(rows.filter((r) => r.kind === "nook").map((r) => r.nookId)).toEqual(["p1"]);
    expect(rows.find((r) => r.shoreId === "r2" && r.kind === "shore")!.collapsed).toBe(true);
  });

  it("hides all shores when the bay is collapsed", () => {
    const rows = buildBayTree(baseInput({ bayCollapsed: true }));
    expect(rows).toHaveLength(1);
    expect(rows[0].kind).toBe("bay");
    expect(rows[0].collapsed).toBe(true);
  });

  it("renders a lone bay row with a zero count when it has no shores", () => {
    const rows = buildBayTree(baseInput({ shores: [] }));
    expect(rows).toHaveLength(1);
    expect(rows[0].kind).toBe("bay");
    expect(rows[0].count).toBe(0);
    expect(rows[0].expandable).toBe(false);
  });

  it("lists every bay, expanding only the active one", () => {
    const rows = buildBayTree(baseInput({
      bays: [{ id: "w1", name: "Cove" }, { id: "w2", name: "Raptor" }],
      activeBayId: "w1",
    }));
    const wsRows = rows.filter((r) => r.kind === "bay");
    expect(wsRows.map((r) => r.label)).toEqual(["Cove", "Raptor"]);
    expect(wsRows[0].active).toBe(true);
    expect(wsRows[1].active).toBe(false);
    expect(wsRows[1].collapsed).toBe(true);
    const shoreRows = rows.filter((r) => r.kind === "shore");
    expect(shoreRows.every((r) => r.bayId === "w1")).toBe(true);
  });

  it("keeps a single unlisted bay expanded regardless of active id", () => {
    const rows = buildBayTree(baseInput());
    expect(rows.filter((r) => r.kind === "bay")).toHaveLength(1);
    expect(rows.some((r) => r.kind === "shore")).toBe(true);
  });
});

describe("bayTreeEmptyMessage", () => {
  it("returns the calm empty message when there are no bays", () => {
    expect(bayTreeEmptyMessage(0)).toBe(NO_BAYS_MESSAGE);
  });
  it("returns null once at least one bay exists", () => {
    expect(bayTreeEmptyMessage(1)).toBeNull();
    expect(bayTreeEmptyMessage(5)).toBeNull();
  });
});
