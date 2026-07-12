using Microsoft.Extensions.Logging;

namespace Cove.Engine.Daemon;

public static class CliBinLink
{
    public static string LinkPath(string dataDir)
    {
        var binDir = System.IO.Path.Combine(dataDir, "bin");
        return System.IO.Path.Combine(binDir, OperatingSystem.IsWindows() ? "cove.exe" : "cove");
    }

    public static string Ensure(string dataDir, string? processPath, ILogger logger)
    {
        var linkPath = LinkPath(dataDir);
        var binDir = System.IO.Path.GetDirectoryName(linkPath)!;
        if (string.IsNullOrEmpty(processPath) || !System.IO.File.Exists(processPath))
        {
            logger.LogWarning("cli bin link skipped: process path {Path} not found", processPath ?? "<null>");
            return linkPath;
        }
        try
        {
            System.IO.Directory.CreateDirectory(binDir);
            var info = new System.IO.FileInfo(linkPath);
            if (info.Exists)
            {
                if (string.Equals(info.LinkTarget, processPath, StringComparison.Ordinal))
                    return linkPath;
                info.Delete();
            }
            if (OperatingSystem.IsWindows())
            {
                System.IO.File.Copy(processPath, linkPath, overwrite: true);
            }
            else
            {
                System.IO.File.CreateSymbolicLink(linkPath, processPath);
            }
            logger.LogInformation("cli bin link ensured at {Link} -> {Target}", linkPath, processPath);
        }
        catch (System.Exception ex)
        {
            logger.LogWarning(ex, "cli bin link creation failed for {Link}", linkPath);
        }
        return linkPath;
    }
}
