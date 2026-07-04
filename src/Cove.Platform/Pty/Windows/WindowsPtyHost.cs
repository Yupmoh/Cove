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
        _logger.LogError("ConPTY spawn is not yet wired (scheduled: M0 T7 Windows CI).");
        throw new NotImplementedException("ConPTY spawn is wired and validated in M0 T7 (Windows CI).");
    }
}
