import { describe, expect, it } from "vitest";
import { DownloadShelfState, downloadPercent, formatBytes, joinPath } from "./browser-downloads";

describe("formatBytes", () => {
  it("formats byte magnitudes", () => {
    expect(formatBytes(0)).toBe("0 B");
    expect(formatBytes(512)).toBe("512 B");
    expect(formatBytes(2048)).toBe("2.0 KB");
    expect(formatBytes(5 * 1024 * 1024)).toBe("5.0 MB");
  });
});

describe("joinPath", () => {
  it("joins a directory and a file name", () => {
    expect(joinPath("/Users/x/Downloads", "a.zip")).toBe("/Users/x/Downloads/a.zip");
    expect(joinPath("/Users/x/Downloads/", "a.zip")).toBe("/Users/x/Downloads/a.zip");
    expect(joinPath("", "a.zip")).toBe("a.zip");
  });
});

describe("DownloadShelfState", () => {
  it("keeps a requested download in prompt state until resolved", () => {
    const s = new DownloadShelfState();
    s.requested("d1", "https://x.com/a.zip", "a.zip");
    expect(s.prompts.map((i) => i.downloadId)).toEqual(["d1"]);
    expect(s.shelf).toHaveLength(0);
  });

  it("deny removes the pending prompt with no shelf entry", () => {
    const s = new DownloadShelfState();
    s.requested("d1", "https://x.com/a.zip", "a.zip");
    s.deny("d1");
    expect(s.prompts).toHaveLength(0);
    expect(s.shelf).toHaveLength(0);
    expect(s.get("d1")).toBeNull();
  });

  it("allow moves the download onto the shelf as in-progress", () => {
    const s = new DownloadShelfState();
    s.requested("d1", "https://x.com/a.zip", "a.zip");
    s.allow("d1", "/Downloads/a.zip");
    expect(s.prompts).toHaveLength(0);
    expect(s.shelf).toHaveLength(1);
    const item = s.get("d1")!;
    expect(item.state).toBe("inProgress");
    expect(item.path).toBe("/Downloads/a.zip");
  });

  it("tracks progress with a known total", () => {
    const s = new DownloadShelfState();
    s.requested("d1", "https://x.com/a.zip", "a.zip");
    s.allow("d1", "/Downloads/a.zip");
    s.progress("d1", 50, 100);
    const item = s.get("d1")!;
    expect(item.receivedBytes).toBe(50);
    expect(item.totalBytes).toBe(100);
    expect(downloadPercent(item)).toBe(50);
  });

  it("ignores progress for a still-prompting download", () => {
    const s = new DownloadShelfState();
    s.requested("d1", "https://x.com/a.zip", "a.zip");
    expect(s.progress("d1", 10, 100)).toBeNull();
  });

  it("completes correctly on platforms with no progress events (request + completion only)", () => {
    const s = new DownloadShelfState();
    s.requested("d1", "https://x.com/a.zip", "a.zip");
    s.allow("d1", "/Downloads/a.zip");
    s.completed("d1", "/Downloads/a.zip");
    const item = s.get("d1")!;
    expect(item.state).toBe("completed");
    expect(downloadPercent(item)).toBe(100);
  });

  it("indeterminate percent when total is unknown and not complete", () => {
    const s = new DownloadShelfState();
    s.requested("d1", "https://x.com/a.zip", "a.zip");
    s.allow("d1", "/Downloads/a.zip");
    s.progress("d1", 1234, 0);
    const item = s.get("d1")!;
    expect(downloadPercent(item)).toBeNull();
  });

  it("marks a download as failed with an error", () => {
    const s = new DownloadShelfState();
    s.requested("d1", "https://x.com/a.zip", "a.zip");
    s.allow("d1", "/Downloads/a.zip");
    s.failed("d1", "network error");
    const item = s.get("d1")!;
    expect(item.state).toBe("failed");
    expect(item.error).toBe("network error");
  });

  it("preserves insertion order across the shelf", () => {
    const s = new DownloadShelfState();
    s.requested("d1", "u1", "a.zip");
    s.allow("d1", "/p/a.zip");
    s.requested("d2", "u2", "b.zip");
    s.allow("d2", "/p/b.zip");
    expect(s.shelf.map((i) => i.downloadId)).toEqual(["d1", "d2"]);
  });
});
