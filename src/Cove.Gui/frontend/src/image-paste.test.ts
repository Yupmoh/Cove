import { describe, it, expect } from "vitest";
import { buildImageMarkdown, insertAt, pastedImageFileName, imageExtension } from "./image-paste";

describe("image-paste", () => {
  it("builds a relative markdown image link", () => {
    expect(buildImageMarkdown("media/pic.png")).toBe("![](media/pic.png)");
  });

  it("uses alt text when supplied", () => {
    expect(buildImageMarkdown("media/pic.png", "diagram")).toBe("![diagram](media/pic.png)");
  });

  it("inserts a snippet at the given offset", () => {
    expect(insertAt("abcdef", 3, "XYZ")).toBe("abcXYZdef");
  });

  it("inserts at start and end", () => {
    expect(insertAt("abc", 0, "_")).toBe("_abc");
    expect(insertAt("abc", 3, "_")).toBe("abc_");
  });

  it("maps mime types to extensions", () => {
    expect(imageExtension("image/png")).toBe("png");
    expect(imageExtension("image/jpeg")).toBe("jpg");
    expect(imageExtension("image/gif")).toBe("gif");
    expect(imageExtension("image/webp")).toBe("webp");
    expect(imageExtension("image/svg+xml")).toBe("svg");
    expect(imageExtension("application/octet-stream")).toBe("png");
  });

  it("generates a timestamped file name with the right extension", () => {
    const name = pastedImageFileName("image/png", 1720569600000);
    expect(name).toBe("pasted-1720569600000.png");
  });
});
