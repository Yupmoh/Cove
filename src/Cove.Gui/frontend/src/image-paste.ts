export function buildImageMarkdown(relativePath: string, alt = ""): string {
  return `![${alt}](${relativePath})`;
}

export function insertAt(text: string, offset: number, snippet: string): string {
  return text.slice(0, offset) + snippet + text.slice(offset);
}

export function imageExtension(mimeType: string): string {
  switch (mimeType) {
    case "image/jpeg":
      return "jpg";
    case "image/gif":
      return "gif";
    case "image/webp":
      return "webp";
    case "image/svg+xml":
      return "svg";
    case "image/png":
      return "png";
    default:
      return "png";
  }
}

export function pastedImageFileName(mimeType: string, nowMs: number): string {
  return `pasted-${nowMs}.${imageExtension(mimeType)}`;
}
