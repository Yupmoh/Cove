import { describe, it, expect } from "vitest";
import { shortSha, truncateCommitMessage, syncSectionHeader, isInSync, type SyncCommit } from "./scm-sync";

describe("shortSha", () => {
  it("shortens a full sha to 7 chars", () => {
    expect(shortSha("abcdef1234567890")).toBe("abcdef1");
  });

  it("leaves a short sha untouched", () => {
    expect(shortSha("abc")).toBe("abc");
  });
});

describe("truncateCommitMessage", () => {
  it("keeps a short single-line message", () => {
    expect(truncateCommitMessage("fix the bug", 72)).toBe("fix the bug");
  });

  it("uses only the first line of a multi-line message", () => {
    expect(truncateCommitMessage("first line\n\nbody text", 72)).toBe("first line");
  });

  it("ellipsizes a message longer than max", () => {
    expect(truncateCommitMessage("abcdefghij", 5)).toBe("abcd…");
  });
});

describe("syncSectionHeader", () => {
  it("renders label with count", () => {
    expect(syncSectionHeader("Unpushed", 3)).toBe("Unpushed (3)");
  });

  it("renders zero count", () => {
    expect(syncSectionHeader("Incoming", 0)).toBe("Incoming (0)");
  });
});

describe("isInSync", () => {
  const commit: SyncCommit = { sha: "deadbeef", author: "Ada", message: "m", date: "2026-01-01T00:00:00Z" };

  it("is true when both lists are empty", () => {
    expect(isInSync([], [])).toBe(true);
  });

  it("is false when there are unpushed commits", () => {
    expect(isInSync([commit], [])).toBe(false);
  });

  it("is false when there are incoming commits", () => {
    expect(isInSync([], [commit])).toBe(false);
  });
});
