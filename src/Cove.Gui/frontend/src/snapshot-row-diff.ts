export type RowChangeType = "added" | "removed" | "changed" | "unchanged";

export interface RowDiff {
  id: string;
  changeType: RowChangeType;
  before: Record<string, unknown> | null;
  after: Record<string, unknown> | null;
  changedFields: string[];
}

export function parseRowSet(value: string | null): Record<string, unknown>[] | null {
  if (value === null) return null;
  let parsed: unknown;
  try {
    parsed = JSON.parse(value);
  } catch {
    return null;
  }
  if (Array.isArray(parsed)) {
    if (parsed.length === 0) return [];
    if (parsed.every((r) => r !== null && typeof r === "object" && !Array.isArray(r))) {
      return parsed as Record<string, unknown>[];
    }
    return null;
  }
  if (parsed !== null && typeof parsed === "object") {
    const values = Object.values(parsed as Record<string, unknown>);
    if (values.length > 0 && values.every((r) => r !== null && typeof r === "object" && !Array.isArray(r))) {
      return values as Record<string, unknown>[];
    }
  }
  return null;
}

function rowKey(row: Record<string, unknown>, idKey: string, fallbackIndex: number): string {
  const v = row[idKey];
  if (typeof v === "string" || typeof v === "number") return String(v);
  return `#${fallbackIndex}`;
}

function changedFieldsOf(before: Record<string, unknown>, after: Record<string, unknown>): string[] {
  const keys = new Set([...Object.keys(before), ...Object.keys(after)]);
  const changed: string[] = [];
  for (const k of keys) {
    if (JSON.stringify(before[k]) !== JSON.stringify(after[k])) changed.push(k);
  }
  return changed.sort();
}

export function diffRowSets(oldValue: string | null, newValue: string | null, idKey = "id"): RowDiff[] | null {
  const before = parseRowSet(oldValue);
  const after = parseRowSet(newValue);
  if (before === null && after === null) return null;

  const beforeMap = new Map<string, Record<string, unknown>>();
  (before ?? []).forEach((r, i) => beforeMap.set(rowKey(r, idKey, i), r));
  const afterMap = new Map<string, Record<string, unknown>>();
  (after ?? []).forEach((r, i) => afterMap.set(rowKey(r, idKey, i), r));

  const ids: string[] = [];
  const seen = new Set<string>();
  for (const id of [...beforeMap.keys(), ...afterMap.keys()]) {
    if (seen.has(id)) continue;
    seen.add(id);
    ids.push(id);
  }

  const result: RowDiff[] = [];
  for (const id of ids) {
    const b = beforeMap.get(id) ?? null;
    const a = afterMap.get(id) ?? null;
    if (b && !a) {
      result.push({ id, changeType: "removed", before: b, after: null, changedFields: [] });
    } else if (!b && a) {
      result.push({ id, changeType: "added", before: null, after: a, changedFields: [] });
    } else if (b && a) {
      const changedFields = changedFieldsOf(b, a);
      result.push({
        id,
        changeType: changedFields.length > 0 ? "changed" : "unchanged",
        before: b,
        after: a,
        changedFields,
      });
    }
  }
  return result;
}

export function isTaskLikeKey(key: string): boolean {
  return /task/i.test(key);
}
