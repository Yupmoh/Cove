namespace Cove.Platform;

using Cove.Persistence;

public static class CoveTree
{
    private static readonly string[] SkeletonDirs =
    {
        "ipc", "logs", "bin", "cache", "bays",
        "themes", "library", "run-commands", "skills"
    };

    public static void Ensure(CoveDataDir dataDir)
    {
        CreateDir(dataDir.Root);
        foreach (var name in SkeletonDirs)
            CreateDir(Path.Combine(dataDir.Root, name));

        if (!File.Exists(dataDir.GitIgnore))
            AtomicJsonStore.WriteRawText(dataDir.GitIgnore, CoveGitIgnore.Content);

        if (!File.Exists(dataDir.MetaJson))
            DataDirMetaStore.WriteInitial(dataDir);
    }

    private static void CreateDir(string path)
    {
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
    }
}
