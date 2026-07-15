import { describe, it, expect } from "vitest";
import { decodeBase64Bytes, decodeRelayData, decodeTerminalRestoreBytes, toBase64Utf8, parseRelayText } from "./wsproto";

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
    expect(parseRelayText('{"t":"base","off":5,"head":9,"modes":"G1s/MTA0OWg="}')).toEqual({ t: "base", off: 5, head: 9, modes: "G1s/MTA0OWg=" });
    expect(parseRelayText('{"t":"nope"}')).toBeNull();
    expect(parseRelayText('{"t":"base","off":12,"head":20,"modes":"","checkpoint":"U1RBVEU=","checkpointCols":132,"checkpointRows":40}')).toEqual({ t: "base", off: 12, head: 20, modes: "", checkpoint: "U1RBVEU=", checkpointCols: 132, checkpointRows: 40 });
  });
  it("decodes a terminal mode preamble", () => {
    expect(decodeBase64Bytes("G1s/MTA0OWg=")).toEqual(new Uint8Array([27, 91, 63, 49, 48, 52, 57, 104]));
  });
  it("appends mode supplements after serialized terminal state", () => {
    expect(decodeTerminalRestoreBytes("U1RBVEU=", "G1s/MTAwNmg=")).toEqual(new Uint8Array([83, 84, 65, 84, 69, 27, 91, 63, 49, 48, 48, 54, 104]));
  });
});
