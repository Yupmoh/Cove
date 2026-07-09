import { describe, it, expect } from "vitest";
import { mediaUrl } from "./media-url";

describe("mediaUrl", () => {
  it("builds a same-origin media route with encoded path", () => {
    expect(mediaUrl("/Users/moh/doc.pdf")).toBe("/media?path=%2FUsers%2Fmoh%2Fdoc.pdf");
  });

  it("encodes spaces and special characters", () => {
    expect(mediaUrl("/a b/c&d.mp4")).toBe("/media?path=%2Fa%20b%2Fc%26d.mp4");
  });
});
