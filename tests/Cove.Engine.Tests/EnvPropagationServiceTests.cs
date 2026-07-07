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
        private readonly Dictionary<string, List<ISignalablePane>> _byCommand;

        public FakePropagationTarget(Dictionary<string, List<ISignalablePane>> byCommand) => _byCommand = byCommand;

        public IReadOnlyList<ISignalablePane> GetPanesForBinary(string binary)
        {
            QueriedBinaries.Add(binary);
            return _byCommand.TryGetValue(binary, out var panes) ? panes : Array.Empty<ISignalablePane>();
        }
    }

    private sealed class FakeSignalablePane : ISignalablePane
    {
        public string PaneId { get; }
        public List<int> Signals { get; } = new();
        public bool ThrowOnSignal { get; set; }

        public FakeSignalablePane(string paneId) => PaneId = paneId;

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
    public void Save_FiresEnvSaved_PropagatesUsr1ToMatchingPanes()
    {
        if (OperatingSystem.IsWindows()) return;
        var dir = NewDir();
        try
        {
            var pane1 = new FakeSignalablePane("pane-1");
            var pane2 = new FakeSignalablePane("pane-2");
            var target = new FakePropagationTarget(new Dictionary<string, List<ISignalablePane>>
            {
                ["claude"] = new() { pane1, pane2 }
            });
            var store = new AdapterEnvStore(dir);
            using var svc = new EnvPropagationService(store, target, binaryResolver: _ => "claude", logger: null);

            store.Save("claude-code", new List<AdapterEnvVar> { new("A", "1") });

            Assert.Single(pane1.Signals);
            Assert.Single(pane2.Signals);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void Save_SignalException_DoesNotBreakSavePath()
    {
        var dir = NewDir();
        try
        {
            var throwingPane = new FakeSignalablePane("pane-1") { ThrowOnSignal = true };
            var target = new FakePropagationTarget(new Dictionary<string, List<ISignalablePane>>
            {
                ["agent"] = new() { throwingPane }
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
    public void Save_NoMatchingPanes_DoesNotThrow()
    {
        var dir = NewDir();
        try
        {
            var target = new FakePropagationTarget(new Dictionary<string, List<ISignalablePane>>());
            var store = new AdapterEnvStore(dir);
            using var svc = new EnvPropagationService(store, target, binaryResolver: _ => "missing-cli", logger: null);

            var ex = Record.Exception(() => store.Save("unknown-adapter", new List<AdapterEnvVar> { new("A", "1") }));
            Assert.Null(ex);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }
}
