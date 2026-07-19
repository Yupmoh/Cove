import { Window } from "happy-dom";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

const leases: Array<(url: string) => void> = [];

vi.mock("./media-url", () => ({
  mediaUrl: vi.fn(() => new Promise<string>((resolve) => leases.push(resolve))),
}));

import { renderPdfNook } from "./pdf-nook";
import { renderVideoNook } from "./video-nook";

describe("media nook ownership", () => {
  beforeEach(() => {
    const testWindow = new Window();
    vi.stubGlobal("window", testWindow);
    vi.stubGlobal("document", testWindow.document);
    vi.stubGlobal("HTMLMediaElement", testWindow.HTMLMediaElement);
    leases.length = 0;
    document.body.innerHTML = "";
  });

  afterEach(() => {
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
  });

  it("rejects a PDF lease that resolves after disposal", async () => {
    const handle = renderPdfNook("/tmp/file.pdf");
    document.body.appendChild(handle.element);

    await handle.dispose();
    leases.shift()!("http://localhost/file.pdf");
    await Promise.resolve();

    expect(handle.element.querySelector("iframe")?.getAttribute("src")).not.toBe("http://localhost/file.pdf");
  });

  it("unloads video exactly once and rejects a late lease", async () => {
    const pause = vi.spyOn(HTMLMediaElement.prototype, "pause").mockImplementation(() => undefined);
    const load = vi.spyOn(HTMLMediaElement.prototype, "load").mockImplementation(() => undefined);
    const handle = renderVideoNook("/tmp/file.mp4");
    document.body.appendChild(handle.element);

    await handle.dispose();
    await handle.dispose();
    leases.shift()!("http://localhost/file.mp4");
    await Promise.resolve();

    expect(pause).toHaveBeenCalledTimes(1);
    expect(load).toHaveBeenCalledTimes(1);
    expect(handle.element.querySelector("video")?.getAttribute("src")).toBeNull();
  });
});
