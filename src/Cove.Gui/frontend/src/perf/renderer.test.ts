import { describe, it, expect } from "vitest";
import { pickRenderer } from "./renderer";

describe("pickRenderer", () => {
  it("defaults to canvas even when WebGL2 is available", () => {
    expect(pickRenderer({ webgl2: true, canvasAddon: true }, undefined, 4)).toBe("canvas");
  });
  it("allows explicit WebGL only for at most eight visible terminals", () => {
    expect(pickRenderer({ webgl2: true, canvasAddon: true }, "webgl", 8)).toBe("webgl");
    expect(pickRenderer({ webgl2: true, canvasAddon: true }, "webgl", 9)).toBe("canvas");
  });
  it("falls back to canvas without WebGL2", () => {
    expect(pickRenderer({ webgl2: false, canvasAddon: true }, undefined, 1)).toBe("canvas");
  });
  it("falls back to dom with neither", () => {
    expect(pickRenderer({ webgl2: false, canvasAddon: false })).toBe("dom");
  });
  it("honors an explicit override", () => {
    expect(pickRenderer({ webgl2: true, canvasAddon: true }, "dom", 1)).toBe("dom");
  });
});
