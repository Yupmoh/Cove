namespace Cove.Platform.Pty;

public static class PtyConstants
{
    public const int AbiVersion = 1;
    public const int ReadBufferBytes = 65536;
    public const int MaxWriteBytes = 8 * 1024 * 1024;
    public const int DefaultRingCapacityBytes = 8 * 1024 * 1024;
    public const int MinRingCapacityBytes = 4096;
    public const int MinCols = 1;
    public const int MaxCols = 500;
    public const int MinRows = 1;
    public const int MaxRows = 500;
    public const int DefaultCols = 80;
    public const int DefaultRows = 24;
    public const int SigKill = 9;
    public const int SigTerm = 15;
    public const int Esrch = 3;

    public static int SigUsr1 => OperatingSystem.IsMacOS() ? 30 : 10;
    public const string DefaultTerm = "xterm-256color";
    public const string NativeLibrary = "cove_pty";
}
