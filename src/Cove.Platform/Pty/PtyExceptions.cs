using System;

namespace Cove.Platform.Pty;

public sealed class PtySpawnException : Exception
{
    public PtySpawnException(string message) : base(message) { }
    public PtySpawnException(string message, Exception inner) : base(message, inner) { }
}

public sealed class PtyIoException : Exception
{
    public int Errno { get; }
    public PtyIoException(string message, int errno) : base(message) => Errno = errno;
}
