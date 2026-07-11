export interface SyncCommit {
  sha: string;
  author: string;
  message: string;
  date: string;
}

export interface ScmLogResult {
  repoRoot: string;
  unpushed: SyncCommit[];
  unpulled: SyncCommit[];
}

export function shortSha(sha: string): string {
  return sha.slice(0, 7);
}

export function truncateCommitMessage(message: string, max = 72): string {
  const firstLine = message.split("\n")[0];
  if (firstLine.length <= max) return firstLine;
  return firstLine.slice(0, max - 1) + "…";
}

export function syncSectionHeader(label: string, count: number): string {
  return `${label} (${count})`;
}

export function isInSync(unpushed: SyncCommit[], unpulled: SyncCommit[]): boolean {
  return unpushed.length === 0 && unpulled.length === 0;
}
