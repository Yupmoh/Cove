using Cove.Engine.Bays;
using Cove.Engine.Layout;

namespace Cove.Engine.Protocol;

public sealed class ScopeResolver
{
    private readonly BayManager? _bays;

    public ScopeResolver(BayManager? bays = null) => _bays = bays;

    public (string? BayId, string? ShoreId) ResolveNookLocation(string? nookId)
    {
        if (nookId is null || _bays is null)
            return (null, null);
        foreach (var ws in _bays.ListBays())
        {
            var actor = _bays.Get(ws.Id);
            if (actor is null) continue;
            var model = actor.State;
            foreach (var shore in model.Shores)
                foreach (var leaf in MosaicOps.Leaves(shore.LayoutTree))
                    if (leaf.NookId == nookId)
                        return (ws.Id, shore.Id);
        }
        return (null, null);
    }
}
