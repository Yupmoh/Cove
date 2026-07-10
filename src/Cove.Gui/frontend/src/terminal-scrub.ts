const ESC = 0x1b;
const CSI = 0x5b;
const DCS = 0x50;
const OSC = 0x5d;
const BEL = 0x07;
const BACKSLASH = 0x5c;

export interface ScrubOptions {
  includeOscColorReports: boolean;
}

const DEFAULT_OPTIONS: ScrubOptions = { includeOscColorReports: false };

function isParamByte(b: number): boolean {
  return b >= 0x30 && b <= 0x3f;
}

function isIntermediateByte(b: number): boolean {
  return b >= 0x20 && b <= 0x2f;
}

function isFinalByte(b: number): boolean {
  return b >= 0x40 && b <= 0x7e;
}

function matchCsiReport(input: Uint8Array, start: number): number {
  const n = input.length;
  let i = start + 2;
  const firstParam = i < n ? input[i] : -1;
  let sawDollar = false;
  while (i < n && isParamByte(input[i])) i += 1;
  while (i < n && isIntermediateByte(input[i])) {
    if (input[i] === 0x24) sawDollar = true;
    i += 1;
  }
  if (i >= n || !isFinalByte(input[i])) return start;
  const final = input[i];
  const end = i + 1;
  const introduced = firstParam === 0x3f || firstParam === 0x3e || firstParam === 0x3d;
  if (final === 0x63 && introduced) return end;
  if (final === 0x79 && sawDollar && firstParam === 0x3f) return end;
  if (final === 0x52) {
    let onlyCursorParams = true;
    for (let j = start + 2; j < i; j += 1) {
      const b = input[j];
      if (!((b >= 0x30 && b <= 0x39) || b === 0x3b)) {
        onlyCursorParams = false;
        break;
      }
    }
    if (onlyCursorParams) return end;
  }
  return start;
}

function findStringTerminator(input: Uint8Array, from: number): number {
  const n = input.length;
  for (let i = from; i < n; i += 1) {
    if (input[i] === BEL) return i + 1;
    if (input[i] === ESC && i + 1 < n && input[i + 1] === BACKSLASH) return i + 2;
  }
  return -1;
}

function matchDcsReport(input: Uint8Array, start: number): number {
  const n = input.length;
  const a = start + 2 < n ? input[start + 2] : -1;
  const b = start + 3 < n ? input[start + 3] : -1;
  const c = start + 4 < n ? input[start + 4] : -1;
  const isDecrqss = (a === 0x30 || a === 0x31) && b === 0x24 && c === 0x72;
  const isXtversion = a === 0x3e && b === 0x7c;
  if (!isDecrqss && !isXtversion) return start;
  const end = findStringTerminator(input, start + 2);
  return end > 0 ? end : start;
}

function matchOscColorReport(input: Uint8Array, start: number): number {
  const n = input.length;
  let i = start + 2;
  const digits: number[] = [];
  while (i < n && input[i] >= 0x30 && input[i] <= 0x39) {
    digits.push(input[i]);
    i += 1;
  }
  const num = String.fromCharCode(...digits);
  const isColorNum = num === "10" || num === "11" || num === "12" || num === "4";
  if (!isColorNum) return start;
  if (i >= n || input[i] !== 0x3b) return start;
  const bodyStart = i + 1;
  let payloadStart = bodyStart;
  if (num === "4") {
    let j = bodyStart;
    while (j < n && input[j] >= 0x30 && input[j] <= 0x39) j += 1;
    if (j >= n || input[j] !== 0x3b) return start;
    payloadStart = j + 1;
  }
  const rgb = [0x72, 0x67, 0x62];
  const looksLikeReport = rgb.every((rb, k) => input[payloadStart + k] === rb);
  if (!looksLikeReport) return start;
  const end = findStringTerminator(input, payloadStart);
  return end > 0 ? end : start;
}

export function scrubTerminalReports(input: Uint8Array, options: ScrubOptions = DEFAULT_OPTIONS): Uint8Array {
  const n = input.length;
  const out = new Uint8Array(n);
  let o = 0;
  let i = 0;
  while (i < n) {
    if (input[i] === ESC && i + 1 < n) {
      const kind = input[i + 1];
      if (kind === CSI) {
        const end = matchCsiReport(input, i);
        if (end > i) { i = end; continue; }
      } else if (kind === DCS) {
        const end = matchDcsReport(input, i);
        if (end > i) { i = end; continue; }
      } else if (kind === OSC && options.includeOscColorReports) {
        const end = matchOscColorReport(input, i);
        if (end > i) { i = end; continue; }
      }
    }
    out[o] = input[i];
    o += 1;
    i += 1;
  }
  return out.subarray(0, o);
}
