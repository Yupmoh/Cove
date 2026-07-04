import { describe, it, expect } from "vitest";
import { pickRenderer } from "./renderer";

describe("pickRenderer", () => {
  it("prefers webgl when webgl2 is available", () => {
    expect(pickRenderer({ webgl2: true, canvasAddon: true })).toBe("webgl");
  });
  it("falls back to canvas without webgl2", () => {
    expect(pickRenderer({ webgl2: false, canvasAddon: true })).toBe("canvas");
  });
  it("falls back to dom with neither", () => {
    expect(pickRenderer({ webgl2: false, canvasAddon: false })).toBe("dom");
  });
  it("honors an explicit override", () => {
    expect(pickRenderer({ webgl2: true, canvasAddon: true }, "dom")).toBe("dom");
  });
});
