import { describe, it, expect } from "vitest";
import { decodeRelayData, toBase64Utf8, parseRelayText } from "./wsproto";

describe("wsproto", () => {
  it("base64-encodes UTF-8 (multibyte) correctly", () => {
    expect(toBase64Utf8("ls\n")).toBe("bHMK");
    expect(toBase64Utf8("é")).toBe("w6k=");
  });
  it("decodes absolute offsets from binary relay frames", () => {
    const frame = new Uint8Array(12);
    new DataView(frame.buffer).setBigUint64(0, 42n, true);
    frame.set(new TextEncoder().encode("data"), 8);
    expect(decodeRelayData(frame.buffer)).toEqual({ offset: 42, raw: new Uint8Array([100, 97, 116, 97]) });
    expect(() => decodeRelayData(new ArrayBuffer(7))).toThrow("relay data frame");
  });
  it("parses relay messages and rejects junk", () => {
    expect(parseRelayText('{"t":"base","off":5,"head":9}')).toEqual({ t: "base", off: 5, head: 9 });
    expect(parseRelayText('{"t":"nope"}')).toBeNull();
  });
});
