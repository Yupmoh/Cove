export function restoredSummaryText(restored: number, fresh: number, skipped: number): string {
  const parts: string[] = [];
  if (restored > 0) parts.push(`restored ${restored} ${restored === 1 ? "session" : "sessions"}`);
  if (fresh > 0) parts.push(`${fresh} started fresh`);
  if (skipped > 0) parts.push(`${skipped} skipped`);
  return parts.join(" · ");
}
