using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.Logging;

namespace Cove.Persistence;

public static class AtomicJsonStore
{
    public static void Write<T>(string path, T value, JsonTypeInfo<T> typeInfo)
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

        if (File.Exists(fullPath))
            File.Copy(fullPath, fullPath + ".bak", overwrite: true);

        File.Move(temp, fullPath, overwrite: true);

        if (!OperatingSystem.IsWindows())
            NativePosix.FsyncDir(dir);
    }

    public static void WriteRawText(string path, string content)
    {
        var fullPath = Path.GetFullPath(path);
        var dir = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(dir);
        var temp = Path.Combine(dir, $".{Path.GetFileName(fullPath)}.tmp-{Guid.NewGuid():N}");
        using (var fs = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(content);
            fs.Write(bytes);
            fs.Flush(flushToDisk: true);
        }
        File.Move(temp, fullPath, overwrite: true);
        if (!OperatingSystem.IsWindows())
            NativePosix.FsyncDir(dir);
    }

    public static T? Read<T>(string path, JsonTypeInfo<T> typeInfo, ILogger logger) where T : class
    {
        if (!File.Exists(path))
            return null;
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
                logger.LogWarning(ex, "flat-json file {Path} failed to parse; falling back to .bak", path);
                using var fs = File.OpenRead(bak);
                return JsonSerializer.Deserialize(fs, typeInfo);
            }
            logger.LogWarning(ex, "flat-json file {Path} failed to parse and no .bak exists", path);
            throw;
        }
    }
}
