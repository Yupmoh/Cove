using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Text;
using Cove.Platform;
using Microsoft.Extensions.Logging;

namespace Cove.Persistence;

public static class AtomicJsonStore
{
    public static void Write<T>(string path, T value, JsonTypeInfo<T> typeInfo, ILogger? logger = null)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, typeInfo);
        WriteCore(path, bytes, logger, null);
    }

    public static void WriteBytes(
        string path,
        ReadOnlySpan<byte> bytes,
        ILogger? logger = null,
        IFileDurability? durability = null)
    {
        WriteCore(path, bytes, logger, durability);
    }

    public static void WriteRawText(
        string path,
        string content,
        ILogger? logger = null,
        IFileDurability? durability = null)
    {
        WriteCore(path, Encoding.UTF8.GetBytes(content), logger, durability);
    }

    private static void WriteCore(
        string path,
        ReadOnlySpan<byte> bytes,
        ILogger? logger,
        IFileDurability? durability)
    {
        var result = AtomicFile.Replace(path, bytes, logger, durability);
        logger?.AtomicWrite(Path.GetFullPath(path), result.ByteCount, result.BackupCreated);
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
