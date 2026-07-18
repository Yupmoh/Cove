using Cove.Engine.Bays;
using Cove.Engine.Layout;

namespace Cove.Engine.Protocol;

public sealed class ScopeResolver
{
    private readonly LayoutService? _layout;

    public ScopeResolver(LayoutService? layout = null) => _layout = layout;

    public (string? BayId, string? ShoreId) ResolveNookLocation(string? nookId)
        => _layout is null ? (null, null) : _layout.ResolveNookLocation(nookId);
}
