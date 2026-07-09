import { describe, expect, it, vi } from "vitest";
import { NotificationBridge, resolveActivated, shouldRequestPermission, shouldSend, type NotificationBridgeDeps, type NotificationDeliverPayload } from "./notifications";

function payload(id: string, paneId: string): NotificationDeliverPayload {
  return { id, title: "t", body: "b", paneId };
}

function makeDeps(over: Partial<NotificationBridgeDeps> = {}): NotificationBridgeDeps {
  return {
    isPermissionGranted: vi.fn(async () => false),
    requestPermission: vi.fn(async () => true),
    send: vi.fn(async () => void 0),
    reveal: vi.fn(),
    warn: vi.fn(),
    ...over,
  };
}

describe("permission gate helpers", () => {
  it("requests only when not granted and not yet requested", () => {
    expect(shouldRequestPermission("unknown", false)).toBe(true);
    expect(shouldRequestPermission("denied", false)).toBe(true);
    expect(shouldRequestPermission("granted", false)).toBe(false);
    expect(shouldRequestPermission("unknown", true)).toBe(false);
  });

  it("sends only when granted", () => {
    expect(shouldSend("granted")).toBe(true);
    expect(shouldSend("unknown")).toBe(false);
    expect(shouldSend("denied")).toBe(false);
  });
});

describe("resolveActivated", () => {
  it("returns the tracked pane or null", () => {
    const m = new Map([["n1", "pane-1"]]);
    expect(resolveActivated(m, "n1")).toBe("pane-1");
    expect(resolveActivated(m, "missing")).toBeNull();
  });
});

describe("NotificationBridge delivery gate", () => {
  it("sends immediately when already granted", async () => {
    const deps = makeDeps({ isPermissionGranted: vi.fn(async () => true) });
    const b = new NotificationBridge(deps);
    const sent = await b.deliver(payload("n1", "pane-1"));
    expect(sent).toBe(true);
    expect(deps.send).toHaveBeenCalledTimes(1);
    expect(deps.requestPermission).not.toHaveBeenCalled();
    expect(b.trackedPaneFor("n1")).toBe("pane-1");
  });

  it("requests once when not granted and sends on grant", async () => {
    const deps = makeDeps({ isPermissionGranted: vi.fn(async () => false), requestPermission: vi.fn(async () => true) });
    const b = new NotificationBridge(deps);
    expect(await b.deliver(payload("n1", "pane-1"))).toBe(true);
    expect(deps.requestPermission).toHaveBeenCalledTimes(1);
    expect(deps.send).toHaveBeenCalledTimes(1);
  });

  it("degrades and warns once when permission denied, never re-requesting", async () => {
    const deps = makeDeps({ isPermissionGranted: vi.fn(async () => false), requestPermission: vi.fn(async () => false) });
    const b = new NotificationBridge(deps);
    expect(await b.deliver(payload("n1", "pane-1"))).toBe(false);
    expect(await b.deliver(payload("n2", "pane-2"))).toBe(false);
    expect(deps.requestPermission).toHaveBeenCalledTimes(1);
    expect(deps.send).not.toHaveBeenCalled();
    expect(deps.warn).toHaveBeenCalledTimes(1);
    expect(b.permissionState).toBe("denied");
  });

  it("drops a payload with no id", async () => {
    const deps = makeDeps({ isPermissionGranted: vi.fn(async () => true) });
    const b = new NotificationBridge(deps);
    expect(await b.deliver(payload("", "pane-1"))).toBe(false);
    expect(deps.send).not.toHaveBeenCalled();
    expect(deps.warn).toHaveBeenCalledTimes(1);
  });
});

describe("NotificationBridge activation and dismissal", () => {
  it("reveals the tracked pane on activation", async () => {
    const deps = makeDeps({ isPermissionGranted: vi.fn(async () => true) });
    const b = new NotificationBridge(deps);
    await b.deliver(payload("n1", "pane-1"));
    b.onActivated("n1");
    expect(deps.reveal).toHaveBeenCalledWith("pane-1");
  });

  it("no-ops and warns when activation id is unknown", () => {
    const deps = makeDeps();
    const b = new NotificationBridge(deps);
    b.onActivated("ghost");
    expect(deps.reveal).not.toHaveBeenCalled();
    expect(deps.warn).toHaveBeenCalledTimes(1);
  });

  it("clears tracking on dismissal so activation no longer reveals", async () => {
    const deps = makeDeps({ isPermissionGranted: vi.fn(async () => true) });
    const b = new NotificationBridge(deps);
    await b.deliver(payload("n1", "pane-1"));
    b.onDismissed("n1");
    b.onActivated("n1");
    expect(deps.reveal).not.toHaveBeenCalled();
  });
});
