using Microsoft.Extensions.Logging;

namespace Cove.Platform;

public interface IFileDurability
{
    void SetOwnerOnly(string path, ILogger? logger = null);
    void FlushDirectory(string path, ILogger? logger = null);
}

public static class FileDurability
{
    public static IFileDurability System { get; } = new SystemFileDurability();

    private sealed class SystemFileDurability : IFileDurability
    {
        public void SetOwnerOnly(string path, ILogger? logger = null)
        {
            if (OperatingSystem.IsWindows())
                return;

            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        public void FlushDirectory(string path, ILogger? logger = null)
        {
            if (OperatingSystem.IsWindows())
                return;

            NativePosix.FlushDirectory(path, logger);
        }
    }
}
