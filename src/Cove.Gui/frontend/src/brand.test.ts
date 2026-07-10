import { describe, expect, it } from "vitest";
import { BRAND_LOGOS, brandLogoAt, nextBrandIndex, parseBrandIndex } from "./brand";

describe("brand", () => {
  it("has three logo variants", () => {
    expect(BRAND_LOGOS).toHaveLength(3);
    expect(new Set(BRAND_LOGOS).size).toBe(3);
  });

  it("parses stored index and rejects garbage", () => {
    expect(parseBrandIndex("0")).toBe(0);
    expect(parseBrandIndex("2")).toBe(2);
    expect(parseBrandIndex("3")).toBe(0);
    expect(parseBrandIndex("-1")).toBe(0);
    expect(parseBrandIndex("nope")).toBe(0);
    expect(parseBrandIndex(null)).toBe(0);
  });

  it("cycles through all variants and wraps", () => {
    expect(nextBrandIndex(0)).toBe(1);
    expect(nextBrandIndex(1)).toBe(2);
    expect(nextBrandIndex(2)).toBe(0);
  });

  it("resolves a logo path for any index", () => {
    expect(brandLogoAt(1)).toBe(BRAND_LOGOS[1]);
    expect(brandLogoAt(99)).toBe(BRAND_LOGOS[0]);
  });
});
