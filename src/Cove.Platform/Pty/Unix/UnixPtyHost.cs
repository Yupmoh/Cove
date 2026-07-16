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

        ushort cols = (ushort)Math.Clamp(request.Cols, PtyConstants.MinCols, PtyConstants.MaxCols);
        ushort rows = (ushort)Math.Clamp(request.Rows, PtyConstants.MinRows, PtyConstants.MaxRows);
        _logger.UnixSpawnBegin(request.Command, request.WorkingDirectory, cols, rows);

        string path = ResolveExecutable(request.Command, _logger);
        var argv = new List<string> { request.Command };
        argv.AddRange(request.Args);
        var envp = BuildEnvironment(request.Environment);
        _logger.UnixEnvironmentBuilt(envp.Count);

        using var argvNative = new NativeStringArray(argv);
        using var envpNative = new NativeStringArray(envp);

        _logger.UnixForkptyBegin(path);
        int rc = CovePtyNative.Spawn(path, argvNative.Pointer, envpNative.Pointer,
            request.WorkingDirectory, cols, rows, out int masterFd, out int pid);
        if (rc != 0)
        {
            _logger.UnixForkptyFailed(path, -rc);
            throw new PtySpawnException($"forkpty failed for '{path}' (errno {-rc}).");
        }

        long id = Interlocked.Increment(ref _nextSessionId);
        _logger.UnixMasterFdReady(id, masterFd >= 0);
        _logger.SessionSpawned(id, request.Command, request.WorkingDirectory, pid, cols, rows);
        return new UnixPtySession(id, masterFd, pid, _logger);
    }

    public bool TryExportSession(IPtySession session, out int masterFd, out int pid)
    {
        if (session is UnixPtySession unix)
        {
            masterFd = unix.MasterFd;
            pid = unix.Pid;
            return true;
        }
        _logger.UnixExportUnsupportedSession(session?.SessionId ?? -1);
        masterFd = -1;
        pid = -1;
        return false;
    }

    public IPtySession AdoptSession(int masterFd, int pid)
    {
        if (!_supported)
            throw new PlatformNotSupportedException(
                "cove_pty native shim not found or ABI mismatch; expected libcove_pty next to the binary.");
        if (masterFd < 0 || pid <= 0)
            throw new ArgumentOutOfRangeException(nameof(masterFd), $"invalid adoption target fd={masterFd} pid={pid}");
        long id = Interlocked.Increment(ref _nextSessionId);
        _logger.UnixSessionAdopted(id, pid, masterFd);
        return new UnixPtySession(id, masterFd, pid, _logger, adopted: true);
    }

    private static bool ProbeAbi(ILogger logger)
    {
        try
        {
            int v = CovePtyNative.AbiVersion();
            if (v != PtyConstants.AbiVersion)
            {
                logger.UnixAbiMismatch(v, PtyConstants.AbiVersion);
                return false;
            }
            logger.UnixAbiOk(v);
            return true;
        }
        catch (DllNotFoundException ex)
        {
            logger.UnixNativeLibNotFound(ex.Message);
            return false;
        }
    }

    private static string ResolveExecutable(string command, ILogger logger)
    {
        if (Path.IsPathRooted(command))
        {
            if (!File.Exists(command))
            {
                logger.UnixExecutableNotFound(command, "rooted path does not exist");
                throw new PtySpawnException($"executable not found: {command}");
            }
            logger.UnixExecutableResolved(command, command);
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
                {
                    logger.UnixExecutableResolved(command, candidate);
                    return candidate;
                }
            }
        }
        logger.UnixExecutableNotFound(command, "not found on PATH");
        throw new PtySpawnException($"executable '{command}' not found on PATH.");
    }

    private static List<string> BuildEnvironment(IReadOnlyDictionary<string, string>? overrides)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        if (overrides is not null)
        {
            foreach (var kv in overrides)
                map[kv.Key] = kv.Value;
        }
        else
        {
            foreach (System.Collections.DictionaryEntry e in Environment.GetEnvironmentVariables())
                map[(string)e.Key] = (string)(e.Value ?? string.Empty);
        }
        if (!map.ContainsKey("TERM"))
            map["TERM"] = PtyConstants.DefaultTerm;

        var list = new List<string>(map.Count);
        foreach (var kv in map)
            list.Add($"{kv.Key}={kv.Value}");
        return list;
    }
}
