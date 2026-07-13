import { describe, it, expect } from "vitest";
import { scrubTerminalReports, createReplayScrubber } from "./terminal-scrub";

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

describe("createReplayScrubber", () => {
  it("passes plain text and ordinary SGR through", () => {
    const scrubber = createReplayScrubber();
    const s = "\x1b[31mred\x1b[0mplain\x1b[H";
    expect(dec(scrubber.push(enc(s)))).toBe(s);
  });

  it("keeps a benign private mode like show cursor", () => {
    const scrubber = createReplayScrubber();
    const s = "a\x1b[?25hb\x1b[?25lc";
    expect(dec(scrubber.push(enc(s)))).toBe(s);
  });

  it("neutralizes alt-screen enter and exit", () => {
    const scrubber = createReplayScrubber();
    const s = "a\x1b[?1049hui\x1b[?1049lb\x1b[?47hx\x1b[?47ly\x1b[?1047hz\x1b[?1047lw";
    expect(dec(scrubber.push(enc(s)))).toBe("auibxyzw");
  });

  it("neutralizes bracketed-paste and mouse-tracking enables", () => {
    const scrubber = createReplayScrubber();
    const s = "a\x1b[?2004hb\x1b[?1000hc\x1b[?1006hd\x1b[?1002le";
    expect(dec(scrubber.push(enc(s)))).toBe("abcde");
  });

  it("neutralizes a multi-parameter private mode set containing a dangerous mode", () => {
    const scrubber = createReplayScrubber();
    const s = "a\x1b[?25;1049hb";
    expect(dec(scrubber.push(enc(s)))).toBe("ab");
  });

  it("strips device reports and OSC color reports during replay", () => {
    const scrubber = createReplayScrubber();
    const s = "a\x1b[?62;cb\x1b]11;rgb:0000/0000/0000\x07c\x1b[24;80Rd";
    expect(dec(scrubber.push(enc(s)))).toBe("abcd");
  });

  it("neutralizes an alt-screen enter split across chunk boundaries", () => {
    const scrubber = createReplayScrubber();
    const first = dec(scrubber.push(enc("before\x1b[?10")));
    const second = dec(scrubber.push(enc("49hafter")));
    expect(first + second).toBe("beforeafter");
  });

  it("strips a device report split across chunk boundaries", () => {
    const scrubber = createReplayScrubber();
    const first = dec(scrubber.push(enc("x\x1b[?6")));
    const second = dec(scrubber.push(enc("2;cy")));
    expect(first + second).toBe("xy");
  });

  it("carries a bare trailing escape and completes it on the next chunk", () => {
    const scrubber = createReplayScrubber();
    const first = dec(scrubber.push(enc("hello\x1b")));
    const second = dec(scrubber.push(enc("[31mred")));
    expect(first + second).toBe("hello\x1b[31mred");
  });

  it("flushes a carried incomplete tail", () => {
    const scrubber = createReplayScrubber();
    const first = dec(scrubber.push(enc("done\x1b[?20")));
    const flushed = dec(scrubber.flush());
    expect(first).toBe("done");
    expect(flushed).toBe("\x1b[?20");
  });
});
