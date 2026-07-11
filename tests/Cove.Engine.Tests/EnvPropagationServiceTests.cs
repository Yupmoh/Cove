using Cove.Adapters;
using Cove.Engine.Adapters;
using Cove.Protocol;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class EnvPropagationServiceTests
{
    private sealed class FakePropagationTarget : IEnvPropagationTarget
    {
        public List<string> QueriedBinaries { get; } = new();
        private readonly Dictionary<string, List<ISignalableNook>> _byCommand;

        public FakePropagationTarget(Dictionary<string, List<ISignalableNook>> byCommand) => _byCommand = byCommand;

        public IReadOnlyList<ISignalableNook> GetNooksForBinary(string binary)
        {
            QueriedBinaries.Add(binary);
            return _byCommand.TryGetValue(binary, out var nooks) ? nooks : Array.Empty<ISignalableNook>();
        }
    }

    private sealed class FakeSignalableNook : ISignalableNook
    {
        public string NookId { get; }
        public List<int> Signals { get; } = new();
        public bool ThrowOnSignal { get; set; }

        public FakeSignalableNook(string nookId) => NookId = nookId;

        public bool Signal(int signum)
        {
            if (ThrowOnSignal)
                throw new NotImplementedException("ConPTY signal wired in M0 T7.");
            Signals.Add(signum);
            return true;
        }
    }

    private static string NewDir() => Path.Combine(Path.GetTempPath(), "cove-envprop-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Save_FiresEnvSaved_PropagatesUsr1ToMatchingNooks()
    {
        if (OperatingSystem.IsWindows()) return;
        var dir = NewDir();
        try
        {
            var nook1 = new FakeSignalableNook("nook-1");
            var nook2 = new FakeSignalableNook("nook-2");
            var target = new FakePropagationTarget(new Dictionary<string, List<ISignalableNook>>
            {
                ["claude"] = new() { nook1, nook2 }
            });
            var store = new AdapterEnvStore(dir);
            using var svc = new EnvPropagationService(store, target, binaryResolver: _ => "claude", logger: null);

            store.Save("claude-code", new List<AdapterEnvVar> { new("A", "1") });

            Assert.Single(nook1.Signals);
            Assert.Single(nook2.Signals);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void Save_SignalException_DoesNotBreakSavePath()
    {
        var dir = NewDir();
        try
        {
            var throwingNook = new FakeSignalableNook("nook-1") { ThrowOnSignal = true };
            var target = new FakePropagationTarget(new Dictionary<string, List<ISignalableNook>>
            {
                ["agent"] = new() { throwingNook }
            });
            var store = new AdapterEnvStore(dir);
            using var svc = new EnvPropagationService(store, target, binaryResolver: _ => "agent", logger: null);

            var ex = Record.Exception(() => store.Save("codex", new List<AdapterEnvVar> { new("A", "1") }));
            Assert.Null(ex);
            var loaded = store.Load("codex");
            Assert.Single(loaded);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void Save_NoMatchingNooks_DoesNotThrow()
    {
        var dir = NewDir();
        try
        {
            var target = new FakePropagationTarget(new Dictionary<string, List<ISignalableNook>>());
            var store = new AdapterEnvStore(dir);
            using var svc = new EnvPropagationService(store, target, binaryResolver: _ => "missing-cli", logger: null);

            var ex = Record.Exception(() => store.Save("unknown-adapter", new List<AdapterEnvVar> { new("A", "1") }));
            Assert.Null(ex);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }
}
