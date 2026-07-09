using System;
using Microsoft.Extensions.Logging;

namespace Cove.Platform.Pty.Windows;

public sealed class WindowsPtyHost : IPtyHost
{
    private readonly ILogger _logger;

    public WindowsPtyHost(ILogger logger) => _logger = logger;

    public bool IsSupported => OperatingSystem.IsWindows();

    public IPtySession Spawn(PtySpawnRequest request)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("WindowsPtyHost runs on Windows only.");
        _logger.LogError("ConPTY spawn is not implemented; Windows PTY host is a stub (tracked: M9-P19 cross-platform hardening).");
        throw new NotImplementedException("ConPTY spawn is not implemented; Windows PTY host is a stub (tracked: M9-P19 cross-platform hardening).");
    }
}
