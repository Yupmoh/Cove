namespace Cove.Platform;

public enum CoveChannel { Stable, Beta, Dev }

public sealed class CoveDataDir
{
    public CoveChannel Channel { get; }
    public string Root { get; }

    private CoveDataDir(CoveChannel channel, string root)
    {
        Channel = channel;
        Root = root;
    }

    public static CoveDataDir ForRoot(CoveChannel channel, string root)
    {
        return new CoveDataDir(channel, Path.GetFullPath(ExpandHome(root)));
    }

    public static CoveDataDir Resolve(CoveChannel channel)
    {
        var overrideDir = Environment.GetEnvironmentVariable("COVE_DATA_DIR");
        if (!string.IsNullOrWhiteSpace(overrideDir))
            return new CoveDataDir(channel, Path.GetFullPath(ExpandHome(overrideDir)));

        var home = OperatingSystem.IsWindows()
            ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
            : Environment.GetEnvironmentVariable("HOME")
              ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        var dirName = channel switch
        {
            CoveChannel.Stable => ".cove",
            CoveChannel.Beta => ".cove-beta",
            CoveChannel.Dev => ".cove-dev",
            _ => throw new ArgumentOutOfRangeException(nameof(channel), channel, "unknown channel")
        };
        return new CoveDataDir(channel, Path.Combine(home, dirName));
    }

    private static string ExpandHome(string path)
    {
        if (path == "~" || path.StartsWith("~/", StringComparison.Ordinal) ||
            path.StartsWith("~\\", StringComparison.Ordinal))
        {
            var home = OperatingSystem.IsWindows()
                ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                : Environment.GetEnvironmentVariable("HOME")
                  ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return path.Length == 1 ? home : Path.Combine(home, path[2..]);
        }
        return path;
    }

    private string ChannelName => Channel switch
    {
        CoveChannel.Stable => "stable",
        CoveChannel.Beta => "beta",
        CoveChannel.Dev => "dev",
        _ => throw new ArgumentOutOfRangeException()
    };

    public string IpcDir => Path.Combine(Root, "ipc");
    public string SocketPath => Path.Combine(Root, "ipc", $"{ChannelName}.sock");
    public string HookPortFile => Path.Combine(Root, "hook-port");
    public string LogsDir => Path.Combine(Root, "logs");
    public string BinDir => Path.Combine(Root, "bin");
    public string CacheDir => Path.Combine(Root, "cache");
    public string BaysDir => Path.Combine(Root, "bays");
    public string ThemesDir => Path.Combine(Root, "themes");
    public string LibraryDir => Path.Combine(Root, "library");
    public string RunCommandsDir => Path.Combine(Root, "run-commands");
    public string SkillsDir => Path.Combine(Root, "skills");
    public string AdaptersDir => Path.Combine(Root, "adapters");

    public string MemoryDir => Path.Combine(Root, "memory");
    public string FtsDir => Path.Combine(Root, "fts");
    public string NotesDir => Path.Combine(Root, "notes");
    public string ReviewsDir => Path.Combine(Root, "reviews");

    public string ConfigJson => Path.Combine(Root, "config.json");
    public string StateJson => Path.Combine(Root, "state.json");
    public string MetaJson => Path.Combine(Root, ".cove-meta.json");
    public string GitIgnore => Path.Combine(Root, ".gitignore");
}
