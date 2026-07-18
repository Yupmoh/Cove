import { describe, expect, it, vi } from "vitest";
import {
  TerminalSession,
  type TerminalSessionDependencies,
  type TerminalSessionResources,
  type TerminalSettings,
} from "./terminal-session";

function settings(): TerminalSettings {
  return {
    fontFamily: "Mono",
    fontSize: 14,
    lineHeight: 1.4,
    letterSpacing: 1,
    cursorStyle: "bar",
    cursorBlink: true,
    scrollback: 9000,
    padding: 8,
    backgroundOpacity: 0.9,
  };
}

function resources(): TerminalSessionResources {
  return {
    term: {
      options: {},
      buffer: { active: { baseY: 0, viewportY: 0 } },
      rows: 24,
      cols: 80,
      element: null,
      loadAddon: vi.fn(),
      open: vi.fn(),
      dispose: vi.fn(),
      reset: vi.fn(),
      resize: vi.fn(),
      write: vi.fn((_data: unknown, callback?: () => void) => callback?.()),
      scrollToBottom: vi.fn(),
      onData: vi.fn(() => ({ dispose: vi.fn() })),
      onResize: vi.fn(() => ({ dispose: vi.fn() })),
      onTitleChange: vi.fn(() => ({ dispose: vi.fn() })),
      attachCustomKeyEventHandler: vi.fn(),
    },
    fit: { fit: vi.fn() },
    serialize: { serialize: vi.fn(() => "") },
    search: {},
  } as unknown as TerminalSessionResources;
}

function dependencies(owned: TerminalSessionResources): TerminalSessionDependencies {
  return {
    createResources: vi.fn(() => owned),
    settings,
    theme: () => ({ background: "#000000" }),
    invoke: async <T>() => ({} as T),
    write: vi.fn(async () => {}),
    onExit: vi.fn(),
    createSocket: vi.fn(),
    createResizeObserver: vi.fn(() => ({ observe: vi.fn(), disconnect: vi.fn() } as unknown as ResizeObserver)),
    warn: vi.fn(),
  };
}

interface TestSocket {
  readyState: number;
  binaryType: string;
  onmessage: ((event: MessageEvent) => void) | null;
  onclose: (() => void) | null;
  send: ReturnType<typeof vi.fn>;
  close: ReturnType<typeof vi.fn>;
}

function socket(): TestSocket {
  return {
    readyState: 1,
    binaryType: "",
    onmessage: null,
    onclose: null,
    send: vi.fn(),
    close: vi.fn(),
  };
}

function relayFrame(offset: number, raw: number[]): ArrayBuffer {
  const frame = new ArrayBuffer(8 + raw.length);
  new DataView(frame).setBigUint64(0, BigInt(offset), true);
  new Uint8Array(frame, 8).set(raw);
  return frame;
}

describe("TerminalSession", () => {
  it("applies live settings through its owned terminal", () => {
    const owned = resources();
    const deps = dependencies(owned);
    const session = new TerminalSession("nook-1", 0, {} as HTMLElement, {} as HTMLElement, deps, false);

    session.applySettings();

    expect(deps.createResources).toHaveBeenCalledOnce();
    expect(owned.term.attachCustomKeyEventHandler).toHaveBeenCalledOnce();
    expect(owned.term.onTitleChange).toHaveBeenCalledOnce();
    expect(owned.term.options).toMatchObject({
      fontFamily: "Mono",
      fontSize: 14,
      lineHeight: 1.4,
      letterSpacing: 1,
      cursorStyle: "bar",
      cursorBlink: true,
      scrollback: 9000,
      theme: { background: "#000000" },
    });
  });

  it("pauses the live stream and prevents stale callbacks", () => {
    const owned = resources();
    const socket = {
      readyState: 1,
      close: vi.fn(),
    };
    const session = new TerminalSession("nook-1", 0, { isConnected: true } as HTMLElement, {} as HTMLElement, dependencies(owned), false);
    session.socket = socket as unknown as WebSocket;

    session.pause();

    expect(socket.close).toHaveBeenCalledWith(1000, "terminal hidden");
    expect(session.socket).toBeNull();
  });

  it("releases every terminal resource exactly once", () => {
    vi.useFakeTimers();
    const owned = resources();
    const socket = { readyState: 1, close: vi.fn() };
    const observer = { disconnect: vi.fn() };
    const session = new TerminalSession("nook-1", 0, {} as HTMLElement, {} as HTMLElement, dependencies(owned), false);
    session.socket = socket as unknown as WebSocket;
    session.resizeObserver = observer as unknown as ResizeObserver;
    session.reconnectTimer = globalThis.setTimeout(() => {}, 100);
    session.checkpointTimer = globalThis.setTimeout(() => {}, 100);

    session.dispose();
    session.dispose();
    vi.runAllTimers();

    expect(observer.disconnect).toHaveBeenCalledOnce();
    expect(socket.close).toHaveBeenCalledOnce();
    expect(owned.term.dispose).toHaveBeenCalledOnce();
    vi.useRealTimers();
  });

  it("keeps the replacement acknowledgement timer when stale socket callbacks run", () => {
    vi.useFakeTimers();
    vi.stubGlobal("location", { host: "localhost" });
    vi.stubGlobal("WebSocket", { OPEN: 1, CLOSING: 2, CLOSED: 3 });
    const intervalSpy = vi.spyOn(globalThis, "setInterval");
    const owned = resources();
    const first = socket();
    const second = socket();
    const deps = dependencies(owned);
    vi.mocked(deps.createSocket)
      .mockReturnValueOnce(first as unknown as WebSocket)
      .mockReturnValueOnce(second as unknown as WebSocket);
    const session = new TerminalSession(
      "nook-1",
      0,
      { isConnected: true } as HTMLElement,
      {} as HTMLElement,
      deps,
      false,
    );

    session.connect();
    session.pause();
    session.connect();
    first.onclose?.();
    const staleTimerCallback = intervalSpy.mock.calls[0][0];
    if (typeof staleTimerCallback === "function") staleTimerCallback();
    second.onmessage?.({ data: JSON.stringify({ t: "base", off: 0, head: 0, modes: "" }) } as MessageEvent);
    second.onmessage?.({ data: relayFrame(0, [65]) } as MessageEvent);
    vi.advanceTimersByTime(100);

    expect(second.send).toHaveBeenCalledWith(JSON.stringify({ t: "ack", off: 1 }));
    session.dispose();
    intervalSpy.mockRestore();
    vi.useRealTimers();
    vi.unstubAllGlobals();
  });

  it("ignores terminal write acknowledgements queued before a resync epoch", () => {
    vi.stubGlobal("location", { host: "localhost" });
    vi.stubGlobal("WebSocket", { OPEN: 1, CLOSING: 2, CLOSED: 3 });
    const callbacks: Array<() => void> = [];
    const owned = resources();
    owned.term.write = vi.fn((_data: unknown, callback?: () => void) => {
      if (callback) callbacks.push(callback);
    });
    const liveSocket = socket();
    const deps = dependencies(owned);
    vi.mocked(deps.createSocket).mockReturnValue(liveSocket as unknown as WebSocket);
    const session = new TerminalSession(
      "nook-1",
      0,
      { isConnected: true } as HTMLElement,
      {} as HTMLElement,
      deps,
      false,
    );

    session.connect();
    liveSocket.onmessage?.({ data: JSON.stringify({ t: "base", off: 0, head: 3, modes: "" }) } as MessageEvent);
    liveSocket.onmessage?.({ data: relayFrame(0, [65, 66, 67]) } as MessageEvent);
    liveSocket.onmessage?.({ data: JSON.stringify({ t: "resync", base: 10, modes: "" }) } as MessageEvent);
    callbacks[0]();

    expect(session.consumed).toBe(10);
    expect(session.expectedOffset).toBe(10);
    expect(session.lastAck).toBe(10);
    session.dispose();
    vi.unstubAllGlobals();
  });

  it("accepts a reconnect base at the last committed offset", () => {
    vi.useFakeTimers();
    vi.stubGlobal("location", { host: "localhost" });
    vi.stubGlobal("WebSocket", { OPEN: 1, CLOSING: 2, CLOSED: 3 });
    const callbacks: Array<() => void> = [];
    const owned = resources();
    owned.term.write = vi.fn((_data: unknown, callback?: () => void) => {
      if (callback) callbacks.push(callback);
    });
    const first = socket();
    const second = socket();
    const deps = dependencies(owned);
    vi.mocked(deps.createSocket)
      .mockReturnValueOnce(first as unknown as WebSocket)
      .mockReturnValueOnce(second as unknown as WebSocket);
    const session = new TerminalSession(
      "nook-1",
      0,
      { isConnected: true } as HTMLElement,
      {} as HTMLElement,
      deps,
      false,
    );

    session.connect();
    first.onmessage?.({ data: JSON.stringify({ t: "base", off: 0, head: 3, modes: "" }) } as MessageEvent);
    first.onmessage?.({ data: relayFrame(0, [65, 66, 67]) } as MessageEvent);
    first.onclose?.();
    vi.advanceTimersByTime(250);
    second.onmessage?.({ data: JSON.stringify({ t: "base", off: 0, head: 3, modes: "" }) } as MessageEvent);

    expect(second.close).not.toHaveBeenCalled();
    expect(session.consumed).toBe(0);
    expect(session.expectedOffset).toBe(0);
    session.dispose();
    vi.useRealTimers();
    vi.unstubAllGlobals();
  });

  it.each([
    ["malformed base offset", { t: "base", off: "10", head: 10, modes: "" }],
    ["unsafe base offset", { t: "base", off: Number.MAX_SAFE_INTEGER + 1, head: Number.MAX_SAFE_INTEGER + 1, modes: "" }],
    ["descending base range", { t: "base", off: 12, head: 11, modes: "" }],
    ["regressing base offset", { t: "base", off: 9, head: 11, modes: "" }],
    ["regressing resync offset", { t: "resync", base: 9, modes: "" }],
  ])("rejects %s without mutating counters", (_name, message) => {
    vi.stubGlobal("location", { host: "localhost" });
    vi.stubGlobal("WebSocket", { OPEN: 1, CLOSING: 2, CLOSED: 3 });
    const owned = resources();
    const liveSocket = socket();
    const deps = dependencies(owned);
    vi.mocked(deps.createSocket).mockReturnValue(liveSocket as unknown as WebSocket);
    const session = new TerminalSession(
      "nook-1",
      10,
      { isConnected: true } as HTMLElement,
      {} as HTMLElement,
      deps,
      false,
    );

    session.connect();
    liveSocket.onmessage?.({ data: JSON.stringify(message) } as MessageEvent);

    expect({
      consumed: session.consumed,
      expectedOffset: session.expectedOffset,
      replayUntilOffset: session.replayUntilOffset,
      lastAck: session.lastAck,
    }).toEqual({
      consumed: 10,
      expectedOffset: 10,
      replayUntilOffset: 10,
      lastAck: 10,
    });
    expect(deps.warn).toHaveBeenCalledOnce();
    expect(liveSocket.close).toHaveBeenCalledWith(1008, "invalid terminal control offset");
    session.dispose();
    vi.unstubAllGlobals();
  });
});
