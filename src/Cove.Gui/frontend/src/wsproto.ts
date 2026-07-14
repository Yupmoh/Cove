export function toBase64Utf8(s: string): string {
  const bytes = new TextEncoder().encode(s);
  let bin = "";
  for (let i = 0; i < bytes.length; i++) bin += String.fromCharCode(bytes[i]);
  return btoa(bin);
}

export type RelayMsg =
  | { t: "base"; off: number; head: number }
  | { t: "resync"; base: number }
  | { t: "end"; code: number };

export function parseRelayText(json: string): RelayMsg | null {
  const m = JSON.parse(json);
  if (m && (m.t === "base" || m.t === "resync" || m.t === "end")) return m as RelayMsg;
  return null;
}

export interface RelayData {
  offset: number;
  raw: Uint8Array;
}

export function decodeRelayData(frame: ArrayBuffer): RelayData {
  if (frame.byteLength < 8) throw new Error("invalid relay data frame");
  const offset = Number(new DataView(frame).getBigUint64(0, true));
  if (!Number.isSafeInteger(offset)) throw new Error("relay data offset exceeds JavaScript precision");
  return { offset, raw: new Uint8Array(frame, 8) };
}
