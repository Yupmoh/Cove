using System;
using System.Threading;
using System.Threading.Tasks;
using Cove.Engine.Pty;
using Cove.Generated;
using Cove.Protocol;

namespace Cove.Engine;

public static class EngineCommandRouter
{
    public static async Task<ControlResponse?> RouteAsync(ControlRequest request, PaneRegistry? panes = null, Cove.Engine.Layout.LayoutService? layout = null, CancellationToken cancellationToken = default)
    {
        if (!CoveCommandRegistry.Handlers.TryGetValue(request.Uri, out var handler))
            return null;

        var typed = (Func<EngineDispatchContext, Task<ControlResponse>>)handler;
        return await typed(new EngineDispatchContext(request, panes, layout));
    }
}
