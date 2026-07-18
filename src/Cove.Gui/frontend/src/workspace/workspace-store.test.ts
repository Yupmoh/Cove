import { describe, expect, it } from "vitest";
import { WorkspaceStore, type BaySnapshot } from "./workspace-store";

function snapshot(): BaySnapshot {
  return {
    schemaVersion: 1,
    id: "bay-1",
    name: "Bay",
    projectDir: "/repo",
    activeShoreId: "shore-1",
    focusedNookId: "nook-1",
    shores: [
      {
        id: "shore-1",
        name: "One",
        zoomedNookId: null,
        layoutTree: {
          kind: "leaf",
          nookId: "nook-1",
          subtabs: [],
          activeSubtab: 0,
        },
      },
      {
        id: "shore-2",
        name: "Two",
        zoomedNookId: null,
        layoutTree: {
          kind: "leaf",
          nookId: "nook-2",
          subtabs: [],
          activeSubtab: 0,
        },
      },
    ],
  };
}

describe("WorkspaceStore", () => {
  it("adopts canonical snapshots and their valid selection", () => {
    const store = new WorkspaceStore();

    store.applySnapshot(snapshot());

    expect(store.snapshot?.id).toBe("bay-1");
    expect(store.activeShoreId).toBe("shore-1");
    expect(store.focusedNookId).toBe("nook-1");
  });

  it("preserves a still-valid local selection across refresh", () => {
    const store = new WorkspaceStore();
    store.applySnapshot(snapshot());
    store.selectShore("shore-2", "nook-2");

    store.applySnapshot({ ...snapshot(), activeShoreId: "shore-1", focusedNookId: "nook-1" });

    expect(store.activeShoreId).toBe("shore-2");
    expect(store.focusedNookId).toBe("nook-2");
  });

  it("falls back when a refreshed snapshot removes the active shore", () => {
    const store = new WorkspaceStore();
    store.applySnapshot(snapshot());
    store.selectShore("shore-2", "nook-2");
    const next = snapshot();
    next.shores = [next.shores[0]];

    store.applySnapshot(next);

    expect(store.activeShoreId).toBe("shore-1");
    expect(store.focusedNookId).toBe("nook-1");
  });

  it("retains optimistic selection until the refreshed snapshot contains it", () => {
    const store = new WorkspaceStore();
    store.applySnapshot(snapshot());
    store.activeShoreId = "shore-3";
    store.focusedNookId = "nook-3";
    const next = snapshot();
    next.shores.push({
      id: "shore-3",
      name: "Three",
      zoomedNookId: null,
      layoutTree: {
        kind: "leaf",
        nookId: "nook-3",
        subtabs: [],
        activeSubtab: 0,
      },
    });

    store.applySnapshot(next);

    expect(store.activeShoreId).toBe("shore-3");
    expect(store.focusedNookId).toBe("nook-3");
  });

  it("clears focus without duplicating workspace state", () => {
    const store = new WorkspaceStore();
    store.applySnapshot(snapshot());

    store.clearFocus();

    expect(store.focusedNookId).toBeNull();
    expect(store.snapshot?.focusedNookId).toBe("nook-1");
  });

  it("updates optimistic shore order without mutating the canonical input", () => {
    const store = new WorkspaceStore();
    const input = snapshot();
    store.applySnapshot(input);

    store.reorderShores(["shore-2", "shore-1"]);

    expect(store.snapshot?.shores.map((shore) => shore.id)).toEqual(["shore-2", "shore-1"]);
    expect(input.shores.map((shore) => shore.id)).toEqual(["shore-1", "shore-2"]);
  });

  it("updates active subtabs without mutating the canonical input", () => {
    const store = new WorkspaceStore();
    const input = snapshot();
    const leaf = input.shores[0].layoutTree;
    if (leaf.kind !== "leaf") throw new Error("expected leaf fixture");
    leaf.subtabs = [
      { documentId: "nook-1", nookType: "terminal", title: "One" },
      { documentId: "nook-3", nookType: "terminal", title: "Three" },
    ];
    store.applySnapshot(input);

    store.activateSubtab("shore-1", "nook-1", 1);

    const updated = store.snapshot?.shores[0].layoutTree;
    expect(updated?.kind === "leaf" ? updated.activeSubtab : -1).toBe(1);
    expect(leaf.activeSubtab).toBe(0);
  });
});
