import { Window } from "happy-dom";
import { describe, expect, it, vi } from "vitest";
import { EngineEventRouter } from "../../app/engine-event-router";
import { createAgentStatusFeature } from "./agent-status-feature";

describe("AgentStatusFeature", () => {
  it("owns badge synchronization and one-time restore presentation", async () => {
    const window = new Window();
    let emit: (data: unknown) => void = () => {};
    const router = new EngineEventRouter((listener) => {
      emit = listener;
      return () => { emit = () => {}; };
    });
    const invokeCall = vi.fn();
    async function invoke<T>(
      command: string,
      args: Record<string, unknown> | number,
    ): Promise<T> {
      invokeCall(command, args);
      return {} as T;
    }
    const needsInput = new Set<string>();
    const showToast = vi.fn();
    const feature = createAgentStatusFeature({
      engineEvents: router,
      storage: window.localStorage as unknown as Storage,
      invoke,
      needsInputCount: () => needsInput.size,
      addNeedsInput: (id) => { needsInput.add(id); },
      removeNeedsInput: (id) => { needsInput.delete(id); },
      clearNeedsInput: () => { needsInput.clear(); },
      syncAgentNookStateClasses: vi.fn(),
      agentsVisible: () => false,
      renderAgents: vi.fn(),
      refreshAgents: vi.fn(async () => {}),
      showToast,
    });

    feature.start();
    router.start();
    emit({ channel: "dock.badge", payload: { nookId: "nook-1" } });
    expect(invokeCall).toHaveBeenCalledWith("badge.setCount", 1);

    const restore = {
      channel: "restore.summary",
      payload: { restored: 2, fresh: 1, skipped: 0, bootedAt: "boot-1" },
    };
    emit(restore);
    emit(restore);
    expect(showToast).toHaveBeenCalledOnce();

    await feature.dispose();
    await router.dispose();
  });
});
