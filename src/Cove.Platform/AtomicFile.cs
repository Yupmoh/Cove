using Microsoft.Extensions.Logging;

namespace Cove.Platform;

public readonly record struct AtomicReplaceResult(int ByteCount, bool BackupCreated);

public static class AtomicFile
{
    public static AtomicReplaceResult Replace(
        string path,
        ReadOnlySpan<byte> bytes,
        ILogger? logger = null,
        IFileDurability? durability = null)
    {
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(directory);

        var tempPath = Path.Combine(
            directory,
            $".{Path.GetFileName(fullPath)}.tmp-{Guid.NewGuid():N}");
        var fileDurability = durability ?? FileDurability.System;
        var backupCreated = false;

        try
        {
            using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }

            fileDurability.SetOwnerOnly(tempPath, logger);

            if (File.Exists(fullPath))
            {
                var backupPath = fullPath + ".bak";
                File.Copy(fullPath, backupPath, overwrite: true);
                fileDurability.SetOwnerOnly(backupPath, logger);
                backupCreated = true;
            }

            File.Move(tempPath, fullPath, overwrite: true);
            fileDurability.FlushDirectory(directory, logger);
            return new AtomicReplaceResult(bytes.Length, backupCreated);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }
}
