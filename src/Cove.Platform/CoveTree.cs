namespace Cove.Platform;

using Cove.Persistence;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

public static class CoveTree
{
    private static readonly string[] SkeletonDirs =
    {
        "ipc", "logs", "bin", "cache", "bays",
        "themes", "library", "run-commands", "skills"
    };

    public static void Ensure(CoveDataDir dataDir, ILogger? logger = null)
    {
        ILogger log = logger ?? NullLogger.Instance;
        log.CoveTreeEnsureBegin(dataDir.Root);
        CreateDir(dataDir.Root, log);
        foreach (var name in SkeletonDirs)
            CreateDir(Path.Combine(dataDir.Root, name), log);

        if (!File.Exists(dataDir.GitIgnore))
        {
            AtomicJsonStore.WriteRawText(dataDir.GitIgnore, CoveGitIgnore.Content);
            log.CoveTreeGitIgnoreWritten(dataDir.GitIgnore);
        }

        if (!File.Exists(dataDir.MetaJson))
            DataDirMetaStore.WriteInitial(dataDir, log);
    }

    private static void CreateDir(string path, ILogger logger)
    {
        bool existed = Directory.Exists(path);
        if (OperatingSystem.IsWindows())
        {
            Directory.CreateDirectory(path);
        }
        else
        {
            Directory.CreateDirectory(path);
            File.SetUnixFileMode(path,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
        if (!existed)
            logger.CoveTreeDirCreated(path);
    }
}
