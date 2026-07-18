import { describe, it, expect, vi, beforeEach, type Mock } from "vitest";
import { mediaUrl } from "./media-url";

interface RynBridgeStub {
  __ryn: { invoke: Mock };
}

function stubBridge(): Mock {
  const invoke = vi.fn();
  (globalThis as unknown as { window: RynBridgeStub }).window = { __ryn: { invoke } };
  return invoke;
}

let invokeStub: Mock;

beforeEach(() => {
  invokeStub = stubBridge();
});

describe("mediaUrl", () => {
  it("requests a lease for the file and returns the leased url", async () => {
    invokeStub.mockResolvedValue(JSON.stringify({ url: "/media?lease=ABC123" }));
    const url = await mediaUrl("/Users/moh/doc.pdf");
    expect(url).toBe("/media?lease=ABC123");
    expect(invokeStub).toHaveBeenCalledWith("app.mediaLease", { filePath: "/Users/moh/doc.pdf" });
  });

  it("propagates lease rejection", async () => {
    invokeStub.mockRejectedValue(new Error("media lease rejected"));
    await expect(mediaUrl("/etc/passwd")).rejects.toThrow("media lease rejected");
  });

  it("never embeds the raw path in the returned url", async () => {
    invokeStub.mockResolvedValue(JSON.stringify({ url: "/media?lease=DEADBEEF" }));
    const url = await mediaUrl("/a b/c&d.mp4");
    expect(url).not.toContain("c%26d");
    expect(url).not.toContain("path=");
  });
});
