namespace Cove.Adapters;

public sealed class LocalDirAdapterFetcher : IAdapterFetcher
{
    private readonly string _sourceDir;

    public LocalDirAdapterFetcher(string sourceDir) => _sourceDir = sourceDir;

    public Task FetchIntoAsync(string destDir, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_sourceDir))
            throw new AdapterInstallException($"source directory does not exist: {_sourceDir}");

        CopyRecursive(_sourceDir, destDir, cancellationToken);
        return Task.CompletedTask;
    }

    private static void CopyRecursive(string source, string dest, CancellationToken ct)
    {
        Directory.CreateDirectory(dest);
        foreach (var dir in Directory.EnumerateDirectories(source))
        {
            ct.ThrowIfCancellationRequested();
            var name = Path.GetFileName(dir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (name is ".git" or "node_modules" || name.StartsWith(".installing-", StringComparison.Ordinal))
                continue;
            CopyRecursive(dir, Path.Combine(dest, name), ct);
        }
        foreach (var file in Directory.EnumerateFiles(source))
        {
            ct.ThrowIfCancellationRequested();
            File.Copy(file, Path.Combine(dest, Path.GetFileName(file)), overwrite: true);
        }
    }
}
