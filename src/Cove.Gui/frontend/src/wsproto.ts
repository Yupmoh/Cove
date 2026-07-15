export function toBase64Utf8(s: string): string {
  const bytes = new TextEncoder().encode(s);
  let bin = "";
  for (let i = 0; i < bytes.length; i++) bin += String.fromCharCode(bytes[i]);
  return btoa(bin);
}
export function decodeBase64Bytes(value: string): Uint8Array {
  const binary = atob(value);
  const bytes = new Uint8Array(binary.length);
  for (let i = 0; i < binary.length; i++) bytes[i] = binary.charCodeAt(i);
  return bytes;
}
export function decodeTerminalRestoreBytes(checkpoint: string, modes: string): Uint8Array {
  const state = decodeBase64Bytes(checkpoint);
  const supplement = modes ? decodeBase64Bytes(modes) : new Uint8Array();
  const result = new Uint8Array(state.length + supplement.length);
  result.set(state);
  result.set(supplement, state.length);
  return result;
}


export type RelayMsg =
  | { t: "base"; off: number; head: number; modes: string; checkpoint?: string; checkpointCols?: number; checkpointRows?: number }
  | { t: "resync"; base: number; modes: string; checkpoint?: string; checkpointCols?: number; checkpointRows?: number }
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
