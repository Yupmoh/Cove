import { describe, it, expect } from "vitest";
import { scrubTerminalReports } from "./terminal-scrub";

function enc(s: string): Uint8Array {
  return new TextEncoder().encode(s);
}
function dec(b: Uint8Array): string {
  return new TextDecoder().decode(b);
}

describe("scrubTerminalReports", () => {
  it("leaves plain text untouched", () => {
    expect(dec(scrubTerminalReports(enc("hello world")))).toBe("hello world");
  });

  it("keeps ordinary SGR color and cursor sequences", () => {
    const s = "\x1b[31mred\x1b[0m\x1b[2J\x1b[H$ ";
    expect(dec(scrubTerminalReports(enc(s)))).toBe(s);
  });

  it("strips a primary DA response", () => {
    const s = "before\x1b[?62;c after";
    expect(dec(scrubTerminalReports(enc(s)))).toBe("before after");
  });

  it("strips a secondary DA response", () => {
    const s = "a\x1b[>0;276;0cb";
    expect(dec(scrubTerminalReports(enc(s)))).toBe("ab");
  });

  it("strips a DECRPM mode report", () => {
    const s = "x\x1b[?2026;0$yy";
    expect(dec(scrubTerminalReports(enc(s)))).toBe("xy");
  });

  it("strips a cursor position report but keeps DECSTBM (lowercase r)", () => {
    expect(dec(scrubTerminalReports(enc("p\x1b[24;80Rq")))).toBe("pq");
    const stbm = "\x1b[1;40r";
    expect(dec(scrubTerminalReports(enc(stbm)))).toBe(stbm);
  });

  it("strips a DECRQSS response", () => {
    const s = "a\x1bP1$r0;1m\x1b\\b";
    expect(dec(scrubTerminalReports(enc(s)))).toBe("ab");
  });

  it("strips an XTVERSION response", () => {
    const s = "a\x1bP>|xterm(370)\x1b\\b";
    expect(dec(scrubTerminalReports(enc(s)))).toBe("ab");
  });

  it("does not strip OSC color reports by default", () => {
    const s = "a\x1b]11;rgb:1e1e/2e2e/3e3e\x07b";
    expect(dec(scrubTerminalReports(enc(s)))).toBe(s);
  });

  it("strips OSC 11 color report when opted in (backlog phase)", () => {
    const s = "a\x1b]11;rgb:1e1e/2e2e/3e3e\x07b";
    expect(dec(scrubTerminalReports(enc(s), { includeOscColorReports: true }))).toBe("ab");
  });

  it("strips OSC 4 indexed color report when opted in", () => {
    const s = "a\x1b]4;1;rgb:ffff/0000/0000\x1b\\b";
    expect(dec(scrubTerminalReports(enc(s), { includeOscColorReports: true }))).toBe("ab");
  });

  it("keeps a hex OSC 11 background set even when opted in", () => {
    const s = "a\x1b]11;#1e1e2e\x07b";
    expect(dec(scrubTerminalReports(enc(s), { includeOscColorReports: true }))).toBe(s);
  });

  it("leaves an incomplete DCS response intact rather than dropping to end", () => {
    const s = "a\x1bP1$r0;1m";
    expect(dec(scrubTerminalReports(enc(s)))).toBe(s);
  });

  it("scrubs multiple leading reports from a restore backlog", () => {
    const s = "\x1b[?62;c\x1b[>0;10;0c\x1b]11;rgb:0000/0000/0000\x07user@host:~$ ";
    expect(dec(scrubTerminalReports(enc(s), { includeOscColorReports: true }))).toBe("user@host:~$ ");
  });
});
