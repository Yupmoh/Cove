import { describe, expect, it } from "vitest";
import { PermissionPromptQueue, formatPermissionKinds, permissionOrigin } from "./browser-permissions";

describe("permissionOrigin", () => {
  it("extracts the origin from a url", () => {
    expect(permissionOrigin("https://example.com/path?q=1")).toBe("https://example.com");
  });

  it("falls back to the raw string on an invalid url", () => {
    expect(permissionOrigin("not a url")).toBe("not a url");
  });
});

describe("formatPermissionKinds", () => {
  it("maps known kinds to friendly labels", () => {
    expect(formatPermissionKinds(["camera", "microphone"])).toBe("Camera, Microphone");
  });

  it("passes through unknown kinds", () => {
    expect(formatPermissionKinds(["telepathy"])).toBe("telepathy");
  });

  it("handles an empty kinds list", () => {
    expect(formatPermissionKinds([])).toBe("unknown access");
  });
});

describe("PermissionPromptQueue", () => {
  const req = (id: string): { requestId: string; kinds: string[]; url: string } => ({ requestId: id, kinds: ["camera"], url: "https://a.com" });

  it("exposes the first request as active", () => {
    const q = new PermissionPromptQueue();
    expect(q.active).toBeNull();
    q.add(req("r1"));
    q.add(req("r2"));
    expect(q.active?.requestId).toBe("r1");
    expect(q.count).toBe(2);
  });

  it("ignores duplicate request ids", () => {
    const q = new PermissionPromptQueue();
    q.add(req("r1"));
    q.add(req("r1"));
    expect(q.count).toBe(1);
  });

  it("removing the active request advances to the next", () => {
    const q = new PermissionPromptQueue();
    q.add(req("r1"));
    q.add(req("r2"));
    const removed = q.remove("r1");
    expect(removed?.requestId).toBe("r1");
    expect(q.active?.requestId).toBe("r2");
  });

  it("remove of an unknown id is a no-op returning null", () => {
    const q = new PermissionPromptQueue();
    q.add(req("r1"));
    expect(q.remove("nope")).toBeNull();
    expect(q.count).toBe(1);
  });

  it("supports timeout auto-deny by removing the timed-out request", () => {
    const q = new PermissionPromptQueue();
    q.add(req("r1"));
    expect(q.has("r1")).toBe(true);
    q.remove("r1");
    expect(q.has("r1")).toBe(false);
    expect(q.active).toBeNull();
  });
});
