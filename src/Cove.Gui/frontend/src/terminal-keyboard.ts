const ESC = 0x1b;
const CSI_INTRODUCER = 0x5b;

const STATE_GROUND = 0;
const STATE_ESC = 1;
const STATE_CSI = 2;

const MAX_CSI_PARAM_LENGTH = 32;

export type KeyboardEncoding = "kitty" | "modifyOtherKeys" | "legacy";

export interface KeyboardProtocolTracker {
  push(chunk: Uint8Array): void;
  encoding(): KeyboardEncoding;
  reset(): void;
}

export function shiftEnterSequence(encoding: KeyboardEncoding): string {
  if (encoding === "kitty") return "\x1b[13;2u";
  if (encoding === "modifyOtherKeys") return "\x1b[27;2;13~";
  return "\\\r";
}

function parseModifyOtherKeysLevel(params: string): number | null {
  if (!params.startsWith(">4")) return null;
  const rest = params.slice(2);
  if (rest === "") return 0;
  if (!rest.startsWith(";")) return null;
  const level = Number(rest.slice(1));
  return Number.isFinite(level) ? level : 0;
}

export function createKeyboardProtocolTracker(): KeyboardProtocolTracker {
  let state = STATE_GROUND;
  let params = "";
  const kittyStack: number[] = [];
  let modifyOtherKeys = 0;

  const applyKittySequence = (): void => {
    const lead = params.charCodeAt(0);
    if (lead === 0x3e) {
      const flags = Number(params.slice(1));
      kittyStack.push(Number.isFinite(flags) ? flags : 0);
      return;
    }
    if (lead === 0x3c) {
      const count = params.length > 1 ? Number(params.slice(1)) : 1;
      const pops = Number.isFinite(count) && count > 0 ? count : 1;
      for (let k = 0; k < pops; k += 1) kittyStack.pop();
      return;
    }
    if (lead === 0x3d) {
      const flags = Number(params.split(";")[0].slice(1));
      const value = Number.isFinite(flags) ? flags : 0;
      if (kittyStack.length === 0) kittyStack.push(value);
      else kittyStack[kittyStack.length - 1] = value;
    }
  };

  const applyCsi = (final: number): void => {
    if (final === 0x75) { applyKittySequence(); return; }
    if (final === 0x6d) {
      const level = parseModifyOtherKeysLevel(params);
      if (level !== null) modifyOtherKeys = level;
    }
  };

  const push = (chunk: Uint8Array): void => {
    for (let i = 0; i < chunk.length; i += 1) {
      const b = chunk[i];
      if (state === STATE_GROUND) {
        if (b === ESC) state = STATE_ESC;
        continue;
      }
      if (state === STATE_ESC) {
        if (b === CSI_INTRODUCER) { state = STATE_CSI; params = ""; }
        else if (b === ESC) state = STATE_ESC;
        else state = STATE_GROUND;
        continue;
      }
      if (b >= 0x20 && b <= 0x3f) {
        params += String.fromCharCode(b);
        if (params.length > MAX_CSI_PARAM_LENGTH) state = STATE_GROUND;
        continue;
      }
      if (b >= 0x40 && b <= 0x7e) applyCsi(b);
      state = STATE_GROUND;
    }
  };

  const encoding = (): KeyboardEncoding => {
    const kittyFlags = kittyStack.length ? kittyStack[kittyStack.length - 1] : 0;
    if (kittyFlags > 0) return "kitty";
    if (modifyOtherKeys >= 1) return "modifyOtherKeys";
    return "legacy";
  };

  const reset = (): void => {
    state = STATE_GROUND;
    params = "";
    kittyStack.length = 0;
    modifyOtherKeys = 0;
  };

  return { push, encoding, reset };
}
