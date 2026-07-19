namespace Cove.Platform;

public interface IPlatformFileSystem
{
    bool FileExists(string path);
    bool DirectoryExists(string path);
    void CreateDirectory(string path);
    void DeleteFile(string path);
    void DeleteDirectory(string path, bool recursive);
    void MoveDirectory(string source, string destination);
    IEnumerable<string> EnumerateFiles(string path, string pattern, SearchOption option);
    IEnumerable<string> EnumerateFileSystemEntries(string path);
    DateTimeOffset GetLastWriteTimeUtc(string path);
    long GetFileLength(string path) => new FileInfo(path).Length;
    string ReadAllText(string path);
    Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken);
    byte[] ReadAllBytes(string path) => File.ReadAllBytes(path);
    Task<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken)
        => File.ReadAllBytesAsync(path, cancellationToken);
    Stream OpenRead(string path) => File.OpenRead(path);
    void WriteAllText(string path, string content) => File.WriteAllText(path, content);
    Task WriteAllTextAsync(string path, string content, CancellationToken cancellationToken);
    void CopyFile(string source, string destination, bool overwrite);
}

public sealed class SystemPlatformFileSystem : IPlatformFileSystem
{
    public static SystemPlatformFileSystem Instance { get; } = new();

    public bool FileExists(string path) => File.Exists(path);
    public bool DirectoryExists(string path) => Directory.Exists(path);
    public void CreateDirectory(string path) => Directory.CreateDirectory(path);
    public void DeleteFile(string path) => File.Delete(path);
    public void DeleteDirectory(string path, bool recursive) => Directory.Delete(path, recursive);
    public void MoveDirectory(string source, string destination) => Directory.Move(source, destination);
    public IEnumerable<string> EnumerateFiles(string path, string pattern, SearchOption option)
        => Directory.EnumerateFiles(path, pattern, option);
    public IEnumerable<string> EnumerateFileSystemEntries(string path)
        => Directory.EnumerateFileSystemEntries(path);
    public DateTimeOffset GetLastWriteTimeUtc(string path)
        => File.GetLastWriteTimeUtc(path);
    public long GetFileLength(string path) => new FileInfo(path).Length;
    public string ReadAllText(string path) => File.ReadAllText(path);
    public Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken)
        => File.ReadAllTextAsync(path, cancellationToken);
    public byte[] ReadAllBytes(string path) => File.ReadAllBytes(path);
    public Task<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken)
        => File.ReadAllBytesAsync(path, cancellationToken);
    public Stream OpenRead(string path) => File.OpenRead(path);
    public void WriteAllText(string path, string content) => File.WriteAllText(path, content);
    public Task WriteAllTextAsync(string path, string content, CancellationToken cancellationToken)
        => File.WriteAllTextAsync(path, content, cancellationToken);
    public void CopyFile(string source, string destination, bool overwrite)
        => File.Copy(source, destination, overwrite);
}
