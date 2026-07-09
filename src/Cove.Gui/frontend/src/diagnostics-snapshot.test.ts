import { describe, it, expect } from "vitest";
import { parseSnapshotExport, snapshotRows, summarizeSnapshots, totalScrollbackBytes, formatBytes, type DiagnosticsSnapshot } from "./diagnostics-snapshot";

const sample: DiagnosticsSnapshot = {
  takenAt: "2026-07-10T12:00:00+00:00",
  managedMemoryBytes: 4 * 1024 * 1024,
  workingSetBytes: 128 * 1024 * 1024,
  threadCount: 24,
  gcGen0Collections: 12,
  gcGen1Collections: 3,
  gcGen2Collections: 1,
  activePanes: 5,
  activeWorkspaces: 2,
  activeAgents: 1,
  cpuUsagePercent: 7.5,
  paneScrollbackBytes: { "pane-a": 1024, "pane-b": 2048 },
};

describe("parseSnapshotExport", () => {
  it("rejects empty input with a warning message", () => {
    const r = parseSnapshotExport("   ");
    expect(r.ok).toBe(false);
    expect(r.snapshots).toEqual([]);
    expect(r.error).toBeTruthy();
  });
  it("rejects malformed JSON", () => {
    const r = parseSnapshotExport("{not json");
    expect(r.ok).toBe(false);
    expect(r.error).toBeTruthy();
  });
  it("rejects JSON that is not a diagnostics snapshot", () => {
    const r = parseSnapshotExport(JSON.stringify({ hello: "world" }));
    expect(r.ok).toBe(false);
    expect(r.error).toBeTruthy();
  });
  it("parses a single snapshot object", () => {
    const r = parseSnapshotExport(JSON.stringify(sample));
    expect(r.ok).toBe(true);
    expect(r.snapshots.length).toBe(1);
    expect(r.snapshots[0].threadCount).toBe(24);
  });
  it("parses an exported array of snapshots", () => {
    const r = parseSnapshotExport(JSON.stringify([sample, { ...sample, managedMemoryBytes: 8 * 1024 * 1024 }]));
    expect(r.ok).toBe(true);
    expect(r.snapshots.length).toBe(2);
  });
  it("rejects an array containing a non-snapshot element", () => {
    const r = parseSnapshotExport(JSON.stringify([sample, { hello: "world" }]));
    expect(r.ok).toBe(false);
  });
});

describe("totalScrollbackBytes", () => {
  it("sums per-pane scrollback", () => {
    expect(totalScrollbackBytes(sample)).toBe(3072);
  });
  it("returns zero with no panes", () => {
    expect(totalScrollbackBytes({ ...sample, paneScrollbackBytes: {} })).toBe(0);
  });
});

describe("snapshotRows", () => {
  it("labels the memory and gc figures", () => {
    const rows = snapshotRows(sample);
    const byLabel = Object.fromEntries(rows.map((r) => [r.label, r.value]));
    expect(byLabel["Managed memory"]).toBe("4.0 MB");
    expect(byLabel["Working set"]).toBe("128.0 MB");
    expect(byLabel["Threads"]).toBe("24");
    expect(byLabel["GC gen0 / gen1 / gen2"]).toBe("12 / 3 / 1");
    expect(byLabel["Active panes"]).toBe("5");
  });
});

describe("summarizeSnapshots", () => {
  it("reports count and peak managed memory", () => {
    const s = summarizeSnapshots([sample, { ...sample, managedMemoryBytes: 8 * 1024 * 1024 }]);
    expect(s.count).toBe(2);
    expect(s.peakManagedMemoryBytes).toBe(8 * 1024 * 1024);
    expect(s.firstTakenAt).toBe(sample.takenAt);
  });
  it("is empty-safe", () => {
    const s = summarizeSnapshots([]);
    expect(s.count).toBe(0);
    expect(s.peakManagedMemoryBytes).toBe(0);
    expect(s.firstTakenAt).toBeNull();
  });
});

describe("formatBytes", () => {
  it("formats magnitudes", () => {
    expect(formatBytes(0)).toBe("0 B");
    expect(formatBytes(2048)).toBe("2.0 KB");
    expect(formatBytes(3 * 1024 * 1024 * 1024)).toBe("3.0 GB");
  });
});
