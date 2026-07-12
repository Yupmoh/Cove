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

export type ForwardFn = (level: string, message: string) => void;

interface CaptureFlag {
  __coveConsoleCaptured?: boolean;
}

export function installConsoleCapture(forward: ForwardFn): void {
  const flag = window as unknown as CaptureFlag;
  if (flag.__coveConsoleCaptured) return;
  flag.__coveConsoleCaptured = true;

  let forwarding = false;
  const safeForward = (level: string, message: string): void => {
    if (forwarding) return;
    forwarding = true;
    try {
      forward(level, message);
    } catch {
      /* never throw from the capture wrapper */
    } finally {
      forwarding = false;
    }
  };

  wrapConsole("warn", "warn", safeForward);
  wrapConsole("error", "error", safeForward);

  const priorOnError = window.onerror;
  window.onerror = (message, source, lineno, colno, error): boolean => {
    const detail = error instanceof Error ? stringifyArg(error) : String(message);
    safeForward("error", truncate(`${detail} (${source ?? ""}:${lineno ?? 0}:${colno ?? 0})`));
    if (typeof priorOnError === "function") {
      return Boolean(priorOnError.call(window, message, source, lineno, colno, error));
    }
    return false;
  };

  window.addEventListener("unhandledrejection", (event: PromiseRejectionEvent): void => {
    safeForward("error", truncate("unhandledrejection: " + stringifyArg(event.reason)));
  });
}

function wrapConsole(method: "warn" | "error", level: string, safeForward: ForwardFn): void {
  const original = console[method].bind(console);
  console[method] = (...args: unknown[]): void => {
    original(...args);
    safeForward(level, formatConsoleMessage(args));
  };
}
