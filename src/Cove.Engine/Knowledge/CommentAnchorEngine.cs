namespace Cove.Engine.Knowledge;

public sealed record CommentAnchor(string FilePath, int Line, string? ContextHash)
{
    public bool IsOrphaned => ContextHash is null;
}

public sealed record DiffHunk(string OldPath, string NewPath, int OldStart, int OldCount, int NewStart, int NewCount);

public sealed record AnchorResult(string FilePath, int NewLine, bool Orphaned);

public sealed class CommentAnchorEngine
{
    public AnchorResult? ReAnchor(CommentAnchor anchor, IReadOnlyList<DiffHunk> hunks, IReadOnlyDictionary<string, IReadOnlyList<string>> newFileContents)
    {
        var hunksForFile = hunks.Where(h => h.OldPath == anchor.FilePath || h.NewPath == anchor.FilePath).ToList();
        if (hunksForFile.Count == 0)
        {
            if (newFileContents.TryGetValue(anchor.FilePath, out var lines) && lines.Count >= anchor.Line)
                return new AnchorResult(anchor.FilePath, anchor.Line, false);
            return new AnchorResult(anchor.FilePath, anchor.Line, true);
        }

        var renameHunk = hunksForFile.FirstOrDefault(h => h.OldPath != h.NewPath && h.OldPath == anchor.FilePath);
        var effectivePath = renameHunk?.NewPath ?? anchor.FilePath;

        var offset = 0;
        foreach (var h in hunksForFile.OrderBy(h => h.OldStart))
        {
            var hunkOldEnd = h.OldStart + h.OldCount;

            if (h.OldCount > 0 && anchor.Line >= h.OldStart && anchor.Line < hunkOldEnd)
            {
                var localLine = anchor.Line - h.OldStart;
                var candidateLine = h.NewStart + localLine + offset;

                if (h.NewCount == 0)
                    return new AnchorResult(effectivePath, candidateLine, true);

                if (anchor.ContextHash is not null && newFileContents.TryGetValue(effectivePath, out var newLines))
                {
                    if (candidateLine - 1 < newLines.Count && candidateLine > 0)
                    {
                        var candidateHash = ComputeContextHash(newLines[candidateLine - 1]);
                        if (candidateHash == anchor.ContextHash)
                            return new AnchorResult(effectivePath, candidateLine, false);
                    }
                    return new AnchorResult(effectivePath, candidateLine, true);
                }

                return new AnchorResult(effectivePath, candidateLine, false);
            }

            if (h.OldCount == 0)
            {
                if (anchor.Line >= h.OldStart)
                    offset += h.NewCount;
            }
            else
            {
                if (anchor.Line >= hunkOldEnd)
                    offset += h.NewCount - h.OldCount;
            }
        }

        var newLine = anchor.Line + offset;
        if (newFileContents.TryGetValue(effectivePath, out var newLines2) && newLine > 0 && newLine <= newLines2.Count)
            return new AnchorResult(effectivePath, newLine, false);

        return new AnchorResult(effectivePath, newLine, true);
    }

    public static string ComputeContextHash(string line)
    {
        var normalized = line.Trim();
        var bytes = System.Text.Encoding.UTF8.GetBytes(normalized);
        var hash = System.Security.Cryptography.SHA256.HashData(bytes);
        return System.Convert.ToHexString(hash).ToLowerInvariant();
    }
}
