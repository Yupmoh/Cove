using Cove.Engine.Pty;

namespace Cove.Engine.Adapters;

internal sealed class PaneRegistryEnvTarget : IEnvPropagationTarget
{
    private readonly PaneRegistry _panes;

    public PaneRegistryEnvTarget(PaneRegistry panes) => _panes = panes;

    public IReadOnlyList<ISignalablePane> GetPanesForBinary(string binary)
    {
        var matches = new List<ISignalablePane>();
        foreach (var desc in _panes.Descriptors())
        {
            if (string.Equals(desc.Command, binary, StringComparison.OrdinalIgnoreCase))
            {
                if (_panes.TryGet(desc.PaneId, out var session))
                    matches.Add(new PaneSignalAdapter(session));
            }
        }
        return matches;
    }

    private sealed class PaneSignalAdapter : ISignalablePane
    {
        private readonly PaneSession _session;
        public string PaneId => _session.PaneId;

        public PaneSignalAdapter(PaneSession session) => _session = session;

        public bool Signal(int signum) => _session.Session.Signal(signum);
    }
}
