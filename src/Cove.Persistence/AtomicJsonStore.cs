using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.Logging;

namespace Cove.Persistence;

public static class AtomicJsonStore
{
    public static void Write<T>(string path, T value, JsonTypeInfo<T> typeInfo, ILogger? logger = null)
    {
        var fullPath = Path.GetFullPath(path);
        var dir = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(dir);

        var temp = Path.Combine(dir, $".{Path.GetFileName(fullPath)}.tmp-{Guid.NewGuid():N}");
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(value, typeInfo);

        using (var fs = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            fs.Write(bytes);
            fs.Flush(flushToDisk: true);
        }

        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(temp, UnixFileMode.UserRead | UnixFileMode.UserWrite);

        var backup = File.Exists(fullPath);
        if (backup)
            File.Copy(fullPath, fullPath + ".bak", overwrite: true);

        File.Move(temp, fullPath, overwrite: true);

        if (!OperatingSystem.IsWindows())
            NativePosix.FsyncDir(dir, logger);

        logger?.AtomicWrite(fullPath, bytes.Length, backup);
    }

    public static void WriteBytes(string path, ReadOnlySpan<byte> bytes, ILogger? logger = null)
    {
        var fullPath = Path.GetFullPath(path);
        var dir = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(dir);

        var temp = Path.Combine(dir, $".{Path.GetFileName(fullPath)}.tmp-{Guid.NewGuid():N}");
        var byteCount = bytes.Length;
        using (var fs = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            fs.Write(bytes);
            fs.Flush(flushToDisk: true);
        }

        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(temp, UnixFileMode.UserRead | UnixFileMode.UserWrite);

        var backup = File.Exists(fullPath);
        if (backup)
            File.Copy(fullPath, fullPath + ".bak", overwrite: true);

        File.Move(temp, fullPath, overwrite: true);

        if (!OperatingSystem.IsWindows())
            NativePosix.FsyncDir(dir, logger);

        logger?.AtomicWrite(fullPath, byteCount, backup);
    }

    public static void WriteRawText(string path, string content, ILogger? logger = null)
    {
        var fullPath = Path.GetFullPath(path);
        var dir = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(dir);
        var temp = Path.Combine(dir, $".{Path.GetFileName(fullPath)}.tmp-{Guid.NewGuid():N}");
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        using (var fs = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            fs.Write(bytes);
            fs.Flush(flushToDisk: true);
        }
        File.Move(temp, fullPath, overwrite: true);
        if (!OperatingSystem.IsWindows())
            NativePosix.FsyncDir(dir, logger);

        logger?.AtomicWrite(fullPath, bytes.Length, false);
    }

    public static T? Read<T>(string path, JsonTypeInfo<T> typeInfo, ILogger logger) where T : class
    {
        if (!File.Exists(path))
        {
            logger.AtomicRead(path, false);
            return null;
        }
        logger.AtomicRead(path, true);
        try
        {
            using var fs = File.OpenRead(path);
            return JsonSerializer.Deserialize(fs, typeInfo);
        }
        catch (JsonException ex)
        {
            var bak = path + ".bak";
            if (File.Exists(bak))
            {
                logger.FlatJsonParseFailedFallbackBak(path, ex.Message);
                using var fs = File.OpenRead(bak);
                return JsonSerializer.Deserialize(fs, typeInfo);
            }
            logger.FlatJsonParseFailedNoBak(path, ex.Message);
            throw;
        }
    }
}
