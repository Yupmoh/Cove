using System.Collections.Generic;
using System.IO;
using System.Text;
using Cove.Persistence;
using Cove.Engine.Pty;
using ZLogger;
using Microsoft.Extensions.Logging;

namespace Cove.Engine.Layout;

public static class BayPersistence
{
    public static void Save(BaySnapshot layout, NookDescriptor[] nooks, string wsDir)
    {
        AtomicJsonStore.Write(Path.Combine(wsDir, "bay.json"), layout, CoveJsonContext.Default.BaySnapshot);
        foreach (var d in nooks)
            AtomicJsonStore.Write(Path.Combine(wsDir, "nooks", d.NookId, "session.json"), d, CoveJsonContext.Default.NookDescriptor);
    }

    public static (BaySnapshot? Layout, Dictionary<string, NookDescriptor> Sessions) Load(string wsDir, ILogger logger)
    {
        var layout = AtomicJsonStore.Read<BaySnapshot>(Path.Combine(wsDir, "bay.json"), CoveJsonContext.Default.BaySnapshot, logger);
        var sessions = new Dictionary<string, NookDescriptor>(System.StringComparer.Ordinal);
        var nooksDir = Path.Combine(wsDir, "nooks");
        if (Directory.Exists(nooksDir))
        {
            foreach (var dir in Directory.EnumerateDirectories(nooksDir))
            {
                var sf = Path.Combine(dir, "session.json");
                if (!File.Exists(sf))
                    continue;
                var desc = AtomicJsonStore.Read<NookDescriptor>(sf, CoveJsonContext.Default.NookDescriptor, logger);
                if (desc is not null)
                    sessions[desc.NookId] = desc;
            }
        }
        return (layout, sessions);
    }

    public static void SaveScrollback(string nookId, byte[] bytes, string wsDir)
    {
        var dir = Path.Combine(wsDir, "nooks", nookId);
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "scrollback.bin");
        var tmp = path + ".tmp";
        var restorePath = Path.Combine(dir, "terminal-state.bin");
        if (File.Exists(restorePath))
            File.Delete(restorePath);
        File.WriteAllBytes(tmp, bytes);
        File.Move(tmp, path, true);
    }

    public static byte[]? LoadScrollback(string nookId, string wsDir)
    {
        var path = Path.Combine(wsDir, "nooks", nookId, "scrollback.bin");
        return File.Exists(path) ? File.ReadAllBytes(path) : null;
    }

    public static void SaveTerminalRestoreState(string nookId, TerminalRestoreState state, string wsDir)
    {
        var dir = Path.Combine(wsDir, "nooks", nookId);
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "terminal-state.bin");
        var tmp = path + ".tmp";
        var modeSupplement = Encoding.ASCII.GetBytes(state.ModeSupplement);
        using (var stream = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write(0x32535443);
            writer.Write(state.Offset);
            writer.Write(state.Cols);
            writer.Write(state.Rows);
            writer.Write(state.ScrollbackLines);
            writer.Write(state.Checkpoint.Length);
            writer.Write(state.Tail.Length);
            writer.Write(modeSupplement.Length);
            writer.Write(state.Checkpoint);
            writer.Write(state.Tail);
            writer.Write(modeSupplement);
            writer.Flush();
            stream.Flush(true);
        }
        File.Move(tmp, path, true);
        var scrollbackPath = Path.Combine(dir, "scrollback.bin");
        if (File.Exists(scrollbackPath))
            File.Delete(scrollbackPath);
    }

    public static TerminalRestoreState? LoadTerminalRestoreState(string nookId, string wsDir, ILogger logger)
    {
        var path = Path.Combine(wsDir, "nooks", nookId, "terminal-state.bin");
        if (!File.Exists(path))
            return null;
        try
        {
            using var stream = File.OpenRead(path);
            using var reader = new BinaryReader(stream);
            if (reader.ReadInt32() != 0x32535443)
                throw new InvalidDataException("invalid magic");
            var offset = reader.ReadInt64();
            var cols = reader.ReadInt32();
            var rows = reader.ReadInt32();
            var scrollbackLines = reader.ReadInt32();
            var checkpointLength = reader.ReadInt32();
            var tailLength = reader.ReadInt32();
            var modeSupplementLength = reader.ReadInt32();
            if (offset < 0 || cols < 1 || rows < 1 || scrollbackLines < 0 || checkpointLength < 1 || tailLength < 0 || modeSupplementLength < 0 || modeSupplementLength > 4096)
                throw new InvalidDataException("invalid metadata");
            if ((long)checkpointLength + tailLength + modeSupplementLength != stream.Length - stream.Position)
                throw new InvalidDataException("invalid payload lengths");
            var checkpoint = reader.ReadBytes(checkpointLength);
            var tail = reader.ReadBytes(tailLength);
            var modeSupplement = Encoding.ASCII.GetString(reader.ReadBytes(modeSupplementLength));
            return new TerminalRestoreState(checkpoint, tail, offset, cols, rows, scrollbackLines, modeSupplement);
        }
        catch (System.Exception ex) when (ex is IOException or InvalidDataException or EndOfStreamException)
        {
            logger.TerminalRestoreStateLoadFailed(path, ex.Message);
            return null;
        }
    }
}

internal static partial class BayPersistenceLog
{
    [ZLoggerMessage(LogLevel.Warning, "terminal restore state load failed path={path} error={error}")]
    public static partial void TerminalRestoreStateLoadFailed(this ILogger logger, string path, string error);
}
