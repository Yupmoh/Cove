using Microsoft.Extensions.DependencyInjection;
using Ryn.Core;
using Ryn.Plugins.WebViewPane;

namespace Cove.Gui;

internal sealed class WebViewShutdownPlugin : IRynPlugin
{
    private readonly IServiceProvider _services;

    public WebViewShutdownPlugin(IServiceProvider services) => _services = services;

    public string Name => "coveWebviewShutdown";

    public ValueTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        var window = _services.GetService<IRynWindow>();
        var panes = _services.GetService<WebViewPaneService>();
        if (window is not null && panes is not null)
            window.Closing += (_, _) => panes.CloseAll();
        return ValueTask.CompletedTask;
    }
}
