using Cove.Platform;

namespace Cove.Adapters;

public sealed class LocalDirAdapterFetcher : IAdapterFetcher
{
    private readonly string _sourceDir;
    private readonly IPlatformFileSystem _fileSystem;

    public LocalDirAdapterFetcher(string sourceDir, IPlatformFileSystem? fileSystem = null)
    {
        _sourceDir = sourceDir;
        _fileSystem = fileSystem ?? SystemPlatformFileSystem.Instance;
    }

    public Task FetchIntoAsync(string destDir, CancellationToken cancellationToken = default)
    {
        if (!_fileSystem.DirectoryExists(_sourceDir))
            throw new AdapterInstallException($"source directory does not exist: {_sourceDir}");

        CopyRecursive(_sourceDir, destDir, cancellationToken);
        return Task.CompletedTask;
    }

    private void CopyRecursive(string source, string destination, CancellationToken cancellationToken)
    {
        _fileSystem.CreateDirectory(destination);
        foreach (var entry in _fileSystem.EnumerateFileSystemEntries(source))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var name = Path.GetFileName(entry.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (_fileSystem.DirectoryExists(entry))
            {
                if (name is ".git" or "node_modules" || name.StartsWith(".installing-", StringComparison.Ordinal))
                    continue;
                CopyRecursive(entry, Path.Combine(destination, name), cancellationToken);
            }
            else
            {
                _fileSystem.CopyFile(entry, Path.Combine(destination, name), overwrite: true);
            }
        }
    }
}
