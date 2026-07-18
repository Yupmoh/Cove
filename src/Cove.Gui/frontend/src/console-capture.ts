const MAX_MESSAGE_LENGTH = 2000;
const TRUNCATION_MARKER = "…";

export function stringifyArg(arg: unknown): string {
  if (typeof arg === "string") return arg;
  if (arg === undefined) return "undefined";
  if (arg === null) return "null";
  if (arg instanceof Error) return arg.stack ?? `${arg.name}: ${arg.message}`;
  if (typeof arg === "object") {
    try {
      return JSON.stringify(arg, circularSafeReplacer());
    } catch {
      return Object.prototype.toString.call(arg);
    }
  }
  return String(arg);
}

export function formatConsoleMessage(args: unknown[]): string {
  return truncate(args.map(stringifyArg).join(" "));
}

export function truncate(value: string, max = MAX_MESSAGE_LENGTH): string {
  return value.length > max ? value.slice(0, max) + TRUNCATION_MARKER : value;
}

function circularSafeReplacer(): (key: string, value: unknown) => unknown {
  const seen = new WeakSet<object>();
  return (_key: string, value: unknown): unknown => {
    if (typeof value === "object" && value !== null) {
      if (seen.has(value as object)) return "[Circular]";
      seen.add(value as object);
    }
    return value;
  };
}

export type ForwardFn = (level: string, message: string) => void | PromiseLike<void>;

interface CaptureFlag {
  __coveConsoleCaptureOwner?: object;
}

export function installConsoleCapture(forward: ForwardFn): () => void {
  const flag = window as unknown as CaptureFlag;
  if (flag.__coveConsoleCaptureOwner) return () => {};

  const owner = {};
  flag.__coveConsoleCaptureOwner = owner;

  let active = true;
  let forwarding = false;
  const safeForward = (level: string, message: string): void => {
    if (!active || forwarding) return;
    forwarding = true;
    try {
      const pending = forward(level, message);
      if (pending) void pending.then(undefined, () => {});
    } catch {
    } finally {
      forwarding = false;
    }
  };

  const priorWarn = console.warn;
  const priorError = console.error;
  const capturedWarn = wrapConsole(priorWarn, "warn", safeForward);
  const capturedError = wrapConsole(priorError, "error", safeForward);
  console.warn = capturedWarn;
  console.error = capturedError;

  const priorOnError = window.onerror;
  const capturedOnError = (message: string | Event, source?: string, lineno?: number, colno?: number, error?: Error): boolean => {
    const detail = error instanceof Error ? stringifyArg(error) : String(message);
    safeForward("error", truncate(`${detail} (${source ?? ""}:${lineno ?? 0}:${colno ?? 0})`));
    if (typeof priorOnError === "function") {
      return Boolean(priorOnError.call(window, message, source, lineno, colno, error));
    }
    return false;
  };
  window.onerror = capturedOnError;

  const captureUnhandledRejection = (event: PromiseRejectionEvent): void => {
    safeForward("error", truncate("unhandledrejection: " + stringifyArg(event.reason)));
  };
  window.addEventListener("unhandledrejection", captureUnhandledRejection);

  return (): void => {
    if (!active) return;
    active = false;
    window.removeEventListener("unhandledrejection", captureUnhandledRejection);
    if (console.warn === capturedWarn) console.warn = priorWarn;
    if (console.error === capturedError) console.error = priorError;
    if (window.onerror === capturedOnError) window.onerror = priorOnError;
    if (flag.__coveConsoleCaptureOwner === owner) {
      delete flag.__coveConsoleCaptureOwner;
    }
  };
}

function wrapConsole(
  original: (...args: unknown[]) => void,
  level: string,
  safeForward: ForwardFn,
): (...args: unknown[]) => void {
  return (...args: unknown[]): void => {
    original.apply(console, args);
    safeForward(level, formatConsoleMessage(args));
  };
}
