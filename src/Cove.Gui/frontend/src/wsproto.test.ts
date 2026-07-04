import { describe, it, expect } from "vitest";
import { toBase64Utf8, parseRelayText } from "./wsproto";

describe("wsproto", () => {
  it("base64-encodes UTF-8 (multibyte) correctly", () => {
    expect(toBase64Utf8("ls\n")).toBe("bHMK");
    expect(toBase64Utf8("é")).toBe("w6k=");
  });
  it("parses relay messages and rejects junk", () => {
    expect(parseRelayText('{"t":"base","off":5}')).toEqual({ t: "base", off: 5 });
    expect(parseRelayText('{"t":"nope"}')).toBeNull();
  });
});
