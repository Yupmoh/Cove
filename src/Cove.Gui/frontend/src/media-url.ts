export function mediaUrl(filePath: string): string {
  return `/media?path=${encodeURIComponent(filePath)}`;
}
