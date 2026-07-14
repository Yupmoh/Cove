import { describe, expect, it } from "vitest";
import { createStreamGenerations, replayViewportAction, shouldDisposeNook, shouldResetReplay, streamVisibilityAction } from "./stream-guard";

describe("shouldDisposeNook", () => {
  it("keeps a nook that is still in the layout even when its socket is closed", () => {
    expect(shouldDisposeNook({ inLayout: true, wsClosed: true })).toBe(false);
  });

  it("keeps a detached nook whose socket is still open so switching shores never destroys it", () => {
    expect(shouldDisposeNook({ inLayout: false, wsClosed: false })).toBe(false);
  });

  it("disposes only a nook that has left the layout and whose stream is dead", () => {
    expect(shouldDisposeNook({ inLayout: false, wsClosed: true })).toBe(true);
  });

  it("keeps a live in-layout nook", () => {
    expect(shouldDisposeNook({ inLayout: true, wsClosed: false })).toBe(false);
  });
});

describe("shouldResetReplay", () => {
  it("does not reset the first render of a locally spawned nook with no history", () => {
    expect(shouldResetReplay({ locallySpawned: true, renderedBefore: false })).toBe(false);
  });

  it("resets before replaying a daemon-restored nook", () => {
    expect(shouldResetReplay({ locallySpawned: false, renderedBefore: false })).toBe(true);
  });

  it("resets before recreating a locally spawned nook that already rendered", () => {
    expect(shouldResetReplay({ locallySpawned: true, renderedBefore: true })).toBe(true);
  });

  it("resets a re-rendered restored nook", () => {
    expect(shouldResetReplay({ locallySpawned: false, renderedBefore: true })).toBe(true);
  });
});

describe("replayViewportAction", () => {
  it("follows a restored or recreated terminal to the live prompt after replay", () => {
    expect(replayViewportAction({ resetOnReplay: true, resynced: false })).toBe("bottom");
  });

  it("follows an explicit resync to the live prompt", () => {
    expect(replayViewportAction({ resetOnReplay: false, resynced: true })).toBe("bottom");
  });

  it("preserves the viewport across an ordinary reconnect", () => {
    expect(replayViewportAction({ resetOnReplay: false, resynced: false })).toBe("preserve");
  });
});

describe("streamVisibilityAction", () => {
  it("disconnects a hidden terminal and reconnects it when visible", () => {
    expect(streamVisibilityAction({ visible: false, connected: true })).toBe("disconnect");
    expect(streamVisibilityAction({ visible: true, connected: false })).toBe("connect");
  });

  it("does nothing when visibility and connection already agree", () => {
    expect(streamVisibilityAction({ visible: true, connected: true })).toBe("none");
    expect(streamVisibilityAction({ visible: false, connected: false })).toBe("none");
  });
});

describe("createStreamGenerations", () => {
  it("marks the latest claim current and every earlier claim stale for the same nook", () => {
    const gens = createStreamGenerations();
    const first = gens.claim("a");
    const second = gens.claim("a");

    expect(first).not.toBe(second);
    expect(gens.isCurrent("a", first)).toBe(false);
    expect(gens.isCurrent("a", second)).toBe(true);
  });

  it("keeps generations independent across nooks", () => {
    const gens = createStreamGenerations();
    const a = gens.claim("a");
    const b = gens.claim("b");
    gens.invalidate("b");

    expect(gens.isCurrent("a", a)).toBe(true);
    expect(gens.isCurrent("b", b)).toBe(false);
  });

  it("invalidate retires the current generation so a lingering socket stops writing", () => {
    const gens = createStreamGenerations();
    const live = gens.claim("a");
    gens.invalidate("a");

    expect(gens.isCurrent("a", live)).toBe(false);
  });

  it("treats an unknown nook and generation zero as never current", () => {
    const gens = createStreamGenerations();

    expect(gens.isCurrent("missing", 1)).toBe(false);
    expect(gens.isCurrent("missing", 0)).toBe(false);
  });

  it("lets a reclaim after invalidate become current again", () => {
    const gens = createStreamGenerations();
    const stale = gens.claim("a");
    gens.invalidate("a");
    const fresh = gens.claim("a");

    expect(gens.isCurrent("a", stale)).toBe(false);
    expect(gens.isCurrent("a", fresh)).toBe(true);
  });
});
