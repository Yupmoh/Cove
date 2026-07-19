using System.Text;
using Cove.Platform.Pty.Unix;
using Cove.Testing;
using Xunit;

namespace Cove.Pty.Harness;

public sealed class UnixFdChannelTests
{
    [PlatformFact(TestOperatingSystem.Unix)]
    public void SendWithFd_TransfersAFunctionalDescriptor()
    {
        var (a, b) = UnixFdChannel.CreateSocketPair();
        var (payloadIn, payloadOut) = UnixFdChannel.CreateSocketPair();
        try
        {
            UnixFdChannel.Send(a, Encoding.UTF8.GetBytes("take-this"), payloadIn);

            Span<byte> buffer = stackalloc byte[64];
            var n = UnixFdChannel.Receive(b, buffer, out var receivedFd);
            Assert.Equal("take-this", Encoding.UTF8.GetString(buffer[..n]));
            Assert.True(receivedFd >= 0);
            Assert.NotEqual(payloadIn, receivedFd);

            UnixFdChannel.Write(receivedFd, Encoding.UTF8.GetBytes("through-the-copy"));
            Span<byte> probe = stackalloc byte[64];
            var m = UnixFdChannel.Read(payloadOut, probe);
            Assert.Equal("through-the-copy", Encoding.UTF8.GetString(probe[..m]));
            UnixFdChannel.CloseFd(receivedFd);
        }
        finally
        {
            UnixFdChannel.CloseFd(a);
            UnixFdChannel.CloseFd(b);
            UnixFdChannel.CloseFd(payloadIn);
            UnixFdChannel.CloseFd(payloadOut);
        }
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    public void Send_WithoutFd_YieldsMinusOneOnReceive()
    {
        var (a, b) = UnixFdChannel.CreateSocketPair();
        try
        {
            UnixFdChannel.Send(a, Encoding.UTF8.GetBytes("plain"));

            Span<byte> buffer = stackalloc byte[16];
            var n = UnixFdChannel.Receive(b, buffer, out var fd);
            Assert.Equal("plain", Encoding.UTF8.GetString(buffer[..n]));
            Assert.Equal(-1, fd);
        }
        finally
        {
            UnixFdChannel.CloseFd(a);
            UnixFdChannel.CloseFd(b);
        }
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    public void Receive_OnClosedPeer_ReturnsZero()
    {
        var (a, b) = UnixFdChannel.CreateSocketPair();
        UnixFdChannel.CloseFd(a);
        try
        {
            Span<byte> buffer = stackalloc byte[16];
            var n = UnixFdChannel.Receive(b, buffer, out var fd);
            Assert.Equal(0, n);
            Assert.Equal(-1, fd);
        }
        finally
        {
            UnixFdChannel.CloseFd(b);
        }
    }
}
