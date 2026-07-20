using Cove.Engine.Pty;
using Cove.Platform;
using Cove.Platform.Pty;
using Cove.Protocol;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class NookSpawnCommandTests
{
    private sealed class CapturingHost : IPtyHost
    {
        public bool IsSupported => true;
        public List<PtySpawnRequest> Requests { get; } = [];

        public IPtySession Spawn(PtySpawnRequest request)
        {
            Requests.Add(request);
            return new FakeSession(Requests.Count);
        }

        public IPtySession AdoptSession(int masterFd, int pid) => throw new NotSupportedException();
    }

    private sealed class FakeSession(long sessionId) : IPtySession
    {
        public long SessionId { get; } = sessionId;
        public bool HasExited { get; private set; }
        public int ExitCode => 0;

        public int Read(Span<byte> buffer) => 0;
        public void Write(ReadOnlySpan<byte> data) { }
        public void Resize(int cols, int rows) { }
        public bool Signal(int signum) => true;
        public int WaitForExit() => ExitCode;
        public void Dispose() => HasExited = true;
        public void Kill() => HasExited = true;
    }

    private sealed class FakeShellResolver(string shell) : IShellResolver
    {
        public int Calls { get; private set; }

        public string ResolveDefaultShell()
        {
            Calls++;
            return shell;
        }
    }

    [Fact]
    public void Spawn_AbsentCommandUsesResolvedShellWithoutAddingInvocationFlags()
    {
        var host = new CapturingHost();
        var resolver = new FakeShellResolver("/test/default-shell");
        using var registry = new NookRegistry(host, NullLogger.Instance, shellResolver: resolver);

        registry.Spawn(new SpawnParams(null, ["--login"], "/tmp"));

        var request = Assert.Single(host.Requests);
        Assert.Equal("/test/default-shell", request.Command);
        Assert.Equal(["--login"], request.Args);
        Assert.Equal(1, resolver.Calls);
    }

    [Fact]
    public void Spawn_EmptyCommandUsesResolvedShell()
    {
        var host = new CapturingHost();
        var resolver = new FakeShellResolver("powershell.exe");
        using var registry = new NookRegistry(host, NullLogger.Instance, shellResolver: resolver);

        registry.Spawn(new SpawnParams("", [], "/tmp"));

        var request = Assert.Single(host.Requests);
        Assert.Equal("powershell.exe", request.Command);
        Assert.Equal(1, resolver.Calls);
    }

    [Theory]
    [InlineData("powershell.exe", "-NoLogo", "-Command")]
    [InlineData("pwsh", "-NoLogo", "-Command")]
    [InlineData("cmd.exe", "/c", null)]
    [InlineData("/bin/zsh", "-ilc", null)]
    public void Spawn_ShellCommandUsesOnlyTheShellSpecificInvocationFlags(
        string shell,
        string firstFlag,
        string? secondFlag)
    {
        var host = new CapturingHost();
        var resolver = new FakeShellResolver(shell);
        using var registry = new NookRegistry(host, NullLogger.Instance, shellResolver: resolver);
        const string commandLine = "printf '%s' \"$HOME\"";

        registry.Spawn(new SpawnParams(null, [], "/tmp", ShellCommand: commandLine));

        var request = Assert.Single(host.Requests);
        Assert.Equal(shell, request.Command);
        Assert.Equal(
            secondFlag is null
                ? [firstFlag, commandLine]
                : [firstFlag, secondFlag, commandLine],
            request.Args);
        Assert.Equal(1, resolver.Calls);
    }

    [Fact]
    public void Spawn_ExplicitCommandAndArgumentsRemainExactAndBypassShellResolution()
    {
        var host = new CapturingHost();
        var resolver = new FakeShellResolver("/test/default-shell");
        using var registry = new NookRegistry(host, NullLogger.Instance, shellResolver: resolver);
        const string command = "/opt/custom shell";
        string[] args = ["", "two words", "--literal=$HOME", "quote\"value"];

        registry.Spawn(new SpawnParams(command, args, "/tmp"));

        var request = Assert.Single(host.Requests);
        Assert.Equal(command, request.Command);
        Assert.Equal(args, request.Args);
        Assert.Equal(0, resolver.Calls);
    }
}
