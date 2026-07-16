namespace Cove.Platform.Pty.Unix;


public static class UnixFd
{
    public static int Duplicate(int fd)
    {
        var copy = CovePtyNative.Dup(fd);
        if (copy < 0)
            throw new PtyIoException($"fd dup failed (fd {fd}, errno {-copy}).", -copy);
        return copy;
    }
}
public static class UnixFdChannel
{
    public static (int A, int B) CreateSocketPair()
    {
        var rc = CovePtyNative.SocketPair(out var a, out var b);
        if (rc != 0)
            throw new PtyIoException($"socketpair failed (errno {-rc}).", -rc);
        return (a, b);
    }

    public static void Send(int socketFd, ReadOnlySpan<byte> payload, int fd = -1)
    {
        var n = CovePtyNative.SendWithFd(socketFd, payload, payload.Length, fd);
        if (n < 0)
            throw new PtyIoException($"sendmsg failed (errno {-n}).", (int)-n);
        if (n != payload.Length)
            throw new PtyIoException($"sendmsg wrote {n} of {payload.Length} bytes.", 0);
    }

    public static int Receive(int socketFd, Span<byte> buffer, out int fd)
    {
        var n = CovePtyNative.RecvWithFd(socketFd, buffer, buffer.Length, out fd);
        if (n < 0)
            throw new PtyIoException($"recvmsg failed (errno {-n}).", (int)-n);
        return (int)n;
    }

    public static void Write(int fd, ReadOnlySpan<byte> data)
    {
        var n = CovePtyNative.Write(fd, data, data.Length);
        if (n < 0)
            throw new PtyIoException($"fd write failed (errno {-n}).", (int)-n);
    }

    public static int Read(int fd, Span<byte> buffer)
    {
        var n = CovePtyNative.Read(fd, buffer, buffer.Length);
        if (n < 0)
            throw new PtyIoException($"fd read failed (errno {-n}).", (int)-n);
        return (int)n;
    }

    public static void CloseFd(int fd)
    {
        if (fd >= 0)
            CovePtyNative.Close(fd);
    }
}
