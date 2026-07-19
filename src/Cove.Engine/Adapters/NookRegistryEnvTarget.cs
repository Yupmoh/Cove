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
                matches.Add(new NookSignalAdapter(_nooks, desc.NookId));
        }
        return matches;
    }

    private sealed class NookSignalAdapter : ISignalableNook
    {
        private readonly NookRegistry _registry;
        public string NookId { get; }

        public NookSignalAdapter(NookRegistry registry, string nookId)
        {
            _registry = registry;
            NookId = nookId;
        }

        public bool Signal(int signum) => _registry.Signal(NookId, signum);
    }
}
