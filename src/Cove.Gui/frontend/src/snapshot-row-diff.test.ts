import { describe, it, expect } from "vitest";
import { parseRowSet, diffRowSets, isTaskLikeKey } from "./snapshot-row-diff";

describe("parseRowSet", () => {
  it("parses a JSON array of objects", () => {
    expect(parseRowSet('[{"id":1},{"id":2}]')).toHaveLength(2);
  });

  it("parses an object-of-objects map into rows", () => {
    const rows = parseRowSet('{"a":{"id":"a"},"b":{"id":"b"}}');
    expect(rows).toHaveLength(2);
  });

  it("returns null for non-tabular values", () => {
    expect(parseRowSet("42")).toBeNull();
    expect(parseRowSet('"plain string"')).toBeNull();
    expect(parseRowSet("not json")).toBeNull();
    expect(parseRowSet(null)).toBeNull();
    expect(parseRowSet("[1,2,3]")).toBeNull();
  });
});

describe("diffRowSets", () => {
  it("returns null when neither side is tabular", () => {
    expect(diffRowSets("hello", "world")).toBeNull();
  });

  it("classifies added, removed, changed and unchanged rows", () => {
    const before = '[{"id":1,"title":"a","done":false},{"id":2,"title":"keep"},{"id":3,"title":"gone"}]';
    const after = '[{"id":1,"title":"a","done":true},{"id":2,"title":"keep"},{"id":4,"title":"new"}]';
    const diffs = diffRowSets(before, after)!;
    const byId = Object.fromEntries(diffs.map((d) => [d.id, d]));
    expect(byId["1"].changeType).toBe("changed");
    expect(byId["1"].changedFields).toEqual(["done"]);
    expect(byId["2"].changeType).toBe("unchanged");
    expect(byId["3"].changeType).toBe("removed");
    expect(byId["4"].changeType).toBe("added");
  });

  it("treats a fully-added row set as all additions", () => {
    const diffs = diffRowSets(null, '[{"id":1},{"id":2}]')!;
    expect(diffs.every((d) => d.changeType === "added")).toBe(true);
  });

  it("falls back to positional keys without an id field", () => {
    const diffs = diffRowSets('[{"name":"x"}]', '[{"name":"y"}]')!;
    expect(diffs[0].id).toBe("#0");
    expect(diffs[0].changeType).toBe("changed");
    expect(diffs[0].changedFields).toEqual(["name"]);
  });
});

describe("isTaskLikeKey", () => {
  it("recognizes task-related keys", () => {
    expect(isTaskLikeKey("tasks.json")).toBe(true);
    expect(isTaskLikeKey("state/task-board.json")).toBe(true);
    expect(isTaskLikeKey("workspace.json")).toBe(false);
  });
});
