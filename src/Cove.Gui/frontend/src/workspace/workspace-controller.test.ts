import { describe, expect, it, vi } from "vitest";
import { WorkspaceController } from "./workspace-controller";

describe("WorkspaceController", () => {
  it("serializes workspace mutations in submission order", async () => {
    let releaseFirst: (() => void) | null = null;
    const invoke = vi.fn(async (_command: string, args: unknown) => {
      if ((args as { op: string }).op === "first") {
        await new Promise<void>((resolve) => {
          releaseFirst = resolve;
        });
      }
      return { ok: true };
    });
    const controller = new WorkspaceController(invoke);

    const first = controller.mutate("first", {});
    const second = controller.mutate("second", {});
    await Promise.resolve();

    expect(invoke).toHaveBeenCalledTimes(1);
    releaseFirst!();
    await Promise.all([first, second]);
    expect(invoke.mock.calls.map((call) => (call[1] as { op: string }).op)).toEqual(["first", "second"]);
  });

  it("keeps the queue usable after a rejected mutation", async () => {
    const invoke = vi.fn()
      .mockRejectedValueOnce(new Error("rejected"))
      .mockResolvedValueOnce({ ok: true });
    const controller = new WorkspaceController(invoke);

    await expect(controller.mutate("bad", {})).rejects.toThrow("rejected");
    await expect(controller.mutate("good", {})).resolves.toEqual({ ok: true });
  });

  it("serializes complete workspace transactions around async side effects", async () => {
    let releaseFirst: (() => void) | null = null;
    const invoke = vi.fn(async (_command: string, _args: unknown) => ({}));
    const controller = new WorkspaceController(invoke);

    const first = controller.transaction(async () => {
      await controller.mutate("first", {});
      await new Promise<void>((resolve) => {
        releaseFirst = resolve;
      });
      await controller.mutate("first-after-side-effect", {});
    });
    const second = controller.transaction(async () => {
      await controller.mutate("second", {});
    });
    await vi.waitFor(() => expect(invoke).toHaveBeenCalledTimes(1));

    releaseFirst!();
    await Promise.all([first, second]);

    expect(invoke.mock.calls.map((call) => (call[1] as { op: string }).op)).toEqual([
      "first",
      "first-after-side-effect",
      "second",
    ]);
  });
});
