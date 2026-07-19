namespace Cove.Platform;

public interface IExecutableMode
{
    void MakeUserExecutable(string path);
}

public sealed class SystemExecutableMode : IExecutableMode
{
    public void MakeUserExecutable(string path)
    {
        if (OperatingSystem.IsWindows())
            return;
        var mode = File.GetUnixFileMode(path);
        File.SetUnixFileMode(path, mode | UnixFileMode.UserExecute);
    }
}
