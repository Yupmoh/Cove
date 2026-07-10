export const BRAND_LOGOS = [
  "/brand/logo-spectrum.png",
  "/brand/logo-blue-violet.png",
  "/brand/logo-syntax.png",
];

export const BRAND_ICON = "/brand/icon-512.png";

export function parseBrandIndex(stored: string | null): number {
  const n = Number(stored);
  if (!Number.isInteger(n) || n < 0 || n >= BRAND_LOGOS.length) return 0;
  return n;
}

export function nextBrandIndex(current: number): number {
  return (current + 1) % BRAND_LOGOS.length;
}

export function brandLogoAt(index: number): string {
  return BRAND_LOGOS[parseBrandIndex(String(index))];
}
