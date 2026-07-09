import { describe, it, expect } from "vitest";
import {
  initialPerfBundlesState,
  applyBundleList,
  beginCreate,
  finishCreate,
  surfaceError,
  requestDelete,
  cancelDelete,
  bundleRows,
  PERF_BUNDLES_EMPTY_TEXT,
  type PerfBundleDto,
  type PerfBundleListResult,
} from "./perf-bundles";

const bundleA: PerfBundleDto = {
  id: "aaaaaaaa1111",
  bundlePath: "/home/moh/.cove/perf-bundles/perf-bundle-20260710-120000-aaaaaaaa.zip",
  createdAt: "2026-07-10T12:00:00.0000000+00:00",
  sizeBytes: 2 * 1024 * 1024,
  snapshotCount: 12,
  containsTrace: true,
};

const bundleB: PerfBundleDto = {
  id: "bbbbbbbb2222",
  bundlePath: "/home/moh/.cove/perf-bundles/perf-bundle-20260709-090000-bbbbbbbb.zip",
  createdAt: "2026-07-09T09:00:00.0000000+00:00",
  sizeBytes: 512,
  snapshotCount: 0,
  containsTrace: false,
};

const listResult = (bundles: PerfBundleDto[]): PerfBundleListResult => ({ bundles });

describe("initialPerfBundlesState", () => {
  it("starts empty, idle, and error-free", () => {
    const s = initialPerfBundlesState();
    expect(s.bundles).toEqual([]);
    expect(s.creating).toBe(false);
    expect(s.error).toBeNull();
    expect(s.pendingDeletePath).toBeNull();
  });
});

describe("applyBundleList", () => {
  it("stores bundles and clears any prior error", () => {
    const s = applyBundleList(surfaceError(initialPerfBundlesState(), "boom"), listResult([bundleA, bundleB]));
    expect(s.bundles.length).toBe(2);
    expect(s.error).toBeNull();
  });
  it("ignores malformed entries and non-array payloads", () => {
    const dirty = { bundles: [bundleA, { id: "x" }, null] } as unknown as PerfBundleListResult;
    expect(applyBundleList(initialPerfBundlesState(), dirty).bundles).toEqual([bundleA]);
    expect(applyBundleList(initialPerfBundlesState(), {} as PerfBundleListResult).bundles).toEqual([]);
  });
  it("clears a pending delete when its bundle is gone after refresh", () => {
    const pending = requestDelete(applyBundleList(initialPerfBundlesState(), listResult([bundleA])), bundleA.bundlePath);
    expect(pending.pendingDeletePath).toBe(bundleA.bundlePath);
    const after = applyBundleList(pending, listResult([bundleB]));
    expect(after.pendingDeletePath).toBeNull();
  });
  it("keeps a pending delete when its bundle still exists", () => {
    const pending = requestDelete(applyBundleList(initialPerfBundlesState(), listResult([bundleA])), bundleA.bundlePath);
    const after = applyBundleList(pending, listResult([bundleA, bundleB]));
    expect(after.pendingDeletePath).toBe(bundleA.bundlePath);
  });
});

describe("create lifecycle", () => {
  it("sets and clears the in-flight flag and clears error on begin", () => {
    const started = beginCreate(surfaceError(initialPerfBundlesState(), "old"));
    expect(started.creating).toBe(true);
    expect(started.error).toBeNull();
    expect(finishCreate(started).creating).toBe(false);
  });
});

describe("surfaceError", () => {
  it("records the message and stops any in-flight create", () => {
    const s = surfaceError(beginCreate(initialPerfBundlesState()), "route failed");
    expect(s.error).toBe("route failed");
    expect(s.creating).toBe(false);
  });
});

describe("delete confirmation", () => {
  it("arms and disarms the pending delete path", () => {
    const base = applyBundleList(initialPerfBundlesState(), listResult([bundleA, bundleB]));
    const armed = requestDelete(base, bundleB.bundlePath);
    expect(armed.pendingDeletePath).toBe(bundleB.bundlePath);
    expect(cancelDelete(armed).pendingDeletePath).toBeNull();
  });
});

describe("bundleRows", () => {
  it("projects display rows with name, size, and trace detail", () => {
    const rows = bundleRows(applyBundleList(initialPerfBundlesState(), listResult([bundleA, bundleB])));
    expect(rows[0].name).toBe("perf-bundle-20260710-120000-aaaaaaaa.zip");
    expect(rows[0].sizeLabel).toBe("2.0 MB");
    expect(rows[0].detail).toContain("12 snapshots");
    expect(rows[0].detail).toContain("trace");
    expect(rows[1].detail).toContain("0 snapshots");
    expect(rows[1].detail).toContain("no trace");
  });
  it("marks only the pending-delete row as confirming", () => {
    const armed = requestDelete(applyBundleList(initialPerfBundlesState(), listResult([bundleA, bundleB])), bundleA.bundlePath);
    const rows = bundleRows(armed);
    expect(rows.find((r) => r.bundlePath === bundleA.bundlePath)?.confirmingDelete).toBe(true);
    expect(rows.find((r) => r.bundlePath === bundleB.bundlePath)?.confirmingDelete).toBe(false);
  });
  it("falls back to the raw createdAt when it is not a parseable date", () => {
    const rows = bundleRows(applyBundleList(initialPerfBundlesState(), listResult([{ ...bundleA, createdAt: "not-a-date" }])));
    expect(rows[0].createdAtLabel).toBe("not-a-date");
  });
});

describe("PERF_BUNDLES_EMPTY_TEXT", () => {
  it("is a non-empty caption", () => {
    expect(PERF_BUNDLES_EMPTY_TEXT.length).toBeGreaterThan(0);
  });
});
