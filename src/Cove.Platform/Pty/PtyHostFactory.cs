using System;
using Cove.Platform.Pty.Unix;
using Cove.Platform.Pty.Windows;
using Microsoft.Extensions.Logging;

namespace Cove.Platform.Pty;

public static class PtyHostFactory
{
    public static IPtyHost Create(ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        if (OperatingSystem.IsWindows())
            return new WindowsPtyHost(logger);
        if (OperatingSystem.IsMacOS() || OperatingSystem.IsLinux())
            return new UnixPtyHost(logger);
        throw new PlatformNotSupportedException(
            "Cove PTY host supports macOS, Linux, and Windows only.");
    }
}
