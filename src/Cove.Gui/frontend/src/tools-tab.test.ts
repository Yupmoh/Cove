import { describe, expect, it } from "vitest";
import { adapterCardSubtitle, adapterStatusMeta } from "./tools-tab";

describe("adapterStatusMeta", () => {
  it("maps detected to a green swatch", () => {
    expect(adapterStatusMeta("detected")).toEqual({ label: "detected", cssColor: "#5fc08a" });
  });

  it("maps broken to amber", () => {
    expect(adapterStatusMeta("broken")).toEqual({ label: "broken", cssColor: "#e0a44a" });
  });

  it("maps missing to muted", () => {
    expect(adapterStatusMeta("missing")).toEqual({ label: "missing", cssColor: "var(--muted)" });
  });

  it("maps null and undefined to unknown", () => {
    expect(adapterStatusMeta(null)).toEqual({ label: "unknown", cssColor: "var(--muted)" });
    expect(adapterStatusMeta(undefined)).toEqual({ label: "unknown", cssColor: "var(--muted)" });
  });
});

describe("adapterCardSubtitle", () => {
  it("joins version and binary path", () => {
    expect(adapterCardSubtitle("1.2.3", "/usr/local/bin/x")).toBe("v1.2.3 · /usr/local/bin/x");
  });

  it("handles a missing version", () => {
    expect(adapterCardSubtitle(null, "/usr/local/bin/x")).toBe("version unknown · /usr/local/bin/x");
  });

  it("handles a missing binary path", () => {
    expect(adapterCardSubtitle("1.2.3", null)).toBe("v1.2.3 · binary not found");
  });

  it("handles both missing", () => {
    expect(adapterCardSubtitle(null, undefined)).toBe("version unknown · binary not found");
  });
});
