using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace Cove.Platform.Pty.Unix;

public sealed class UnixPtyHost : IPtyHost
{
    private static long _nextSessionId;
    private readonly ILogger _logger;
    private readonly bool _supported;

    public UnixPtyHost(ILogger logger)
    {
        _logger = logger;
        _supported = ProbeAbi(logger);
    }

    public bool IsSupported => _supported;

    public IPtySession Spawn(PtySpawnRequest request)
    {
        if (!_supported)
            throw new PlatformNotSupportedException(
                "cove_pty native shim not found or ABI mismatch; expected libcove_pty next to the binary.");

        string path = ResolveExecutable(request.Command);
        var argv = new List<string> { request.Command };
        argv.AddRange(request.Args);
        var envp = BuildEnvironment(request.Environment);

        ushort cols = (ushort)Math.Clamp(request.Cols, PtyConstants.MinCols, PtyConstants.MaxCols);
        ushort rows = (ushort)Math.Clamp(request.Rows, PtyConstants.MinRows, PtyConstants.MaxRows);

        using var argvNative = new NativeStringArray(argv);
        using var envpNative = new NativeStringArray(envp);

        int rc = CovePtyNative.Spawn(path, argvNative.Pointer, envpNative.Pointer,
            request.WorkingDirectory, cols, rows, out int masterFd, out int pid);
        if (rc != 0)
            throw new PtySpawnException($"forkpty failed for '{path}' (errno {-rc}).");

        long id = Interlocked.Increment(ref _nextSessionId);
        return new UnixPtySession(id, masterFd, pid, _logger);
    }

    private static bool ProbeAbi(ILogger logger)
    {
        try
        {
            int v = CovePtyNative.AbiVersion();
            if (v != PtyConstants.AbiVersion)
            {
                logger.LogError("cove_pty ABI mismatch: got {Got}, expected {Expected}.", v, PtyConstants.AbiVersion);
                return false;
            }
            return true;
        }
        catch (DllNotFoundException ex)
        {
            logger.LogError(ex, "cove_pty native shim not found next to the binary.");
            return false;
        }
    }

    private static string ResolveExecutable(string command)
    {
        if (Path.IsPathRooted(command))
        {
            if (!File.Exists(command))
                throw new PtySpawnException($"executable not found: {command}");
            return command;
        }

        string? pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (pathEnv is not null)
        {
            foreach (var dir in pathEnv.Split(Path.PathSeparator))
            {
                if (dir.Length == 0) continue;
                string candidate = Path.Combine(dir, command);
                if (File.Exists(candidate))
                    return candidate;
            }
        }
        throw new PtySpawnException($"executable '{command}' not found on PATH.");
    }

    private static List<string> BuildEnvironment(IReadOnlyDictionary<string, string>? overrides)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (System.Collections.DictionaryEntry e in Environment.GetEnvironmentVariables())
            map[(string)e.Key] = (string)(e.Value ?? string.Empty);
        if (overrides is not null)
            foreach (var kv in overrides)
                map[kv.Key] = kv.Value;
        if (!map.ContainsKey("TERM"))
            map["TERM"] = PtyConstants.DefaultTerm;

        var list = new List<string>(map.Count);
        foreach (var kv in map)
            list.Add($"{kv.Key}={kv.Value}");
        return list;
    }
}
