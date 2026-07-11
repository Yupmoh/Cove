using Cove.Engine.Pty;

namespace Cove.Engine.Adapters;

internal sealed class NookRegistryEnvTarget : IEnvPropagationTarget
{
    private readonly NookRegistry _nooks;

    public NookRegistryEnvTarget(NookRegistry nooks) => _nooks = nooks;

    public IReadOnlyList<ISignalableNook> GetNooksForBinary(string binary)
    {
        var matches = new List<ISignalableNook>();
        foreach (var desc in _nooks.Descriptors())
        {
            if (string.Equals(desc.Command, binary, StringComparison.OrdinalIgnoreCase))
            {
                if (_nooks.TryGet(desc.NookId, out var session))
                    matches.Add(new NookSignalAdapter(session));
            }
        }
        return matches;
    }

    private sealed class NookSignalAdapter : ISignalableNook
    {
        private readonly NookSession _session;
        public string NookId => _session.NookId;

        public NookSignalAdapter(NookSession session) => _session = session;

        public bool Signal(int signum) => _session.Session.Signal(signum);
    }
}
