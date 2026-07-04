export function toBase64Utf8(s: string): string {
  const bytes = new TextEncoder().encode(s);
  let bin = "";
  for (let i = 0; i < bytes.length; i++) bin += String.fromCharCode(bytes[i]);
  return btoa(bin);
}

export type RelayMsg =
  | { t: "base"; off: number }
  | { t: "resync"; base: number }
  | { t: "end"; code: number };

export function parseRelayText(json: string): RelayMsg | null {
  const m = JSON.parse(json);
  if (m && (m.t === "base" || m.t === "resync" || m.t === "end")) return m as RelayMsg;
  return null;
}
