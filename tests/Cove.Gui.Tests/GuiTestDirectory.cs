using Cove.Testing;

namespace Cove.Gui.Tests;

internal sealed class GuiTestDirectory : IDisposable
{
    private GuiTestDirectory(string path) => Path = path;

    public string Path { get; }

    public static GuiTestDirectory Create(string prefix = "cove-gui-") =>
        new(TestDirectory.Create(prefix));

    public string WriteFile(string relativePath, string content)
    {
        var path = System.IO.Path.Combine(Path, relativePath);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    public string WriteFile(string relativePath, byte[] content)
    {
        var path = System.IO.Path.Combine(Path, relativePath);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, content);
        return path;
    }

    public void Dispose() => TestDirectory.Delete(Path);
}
