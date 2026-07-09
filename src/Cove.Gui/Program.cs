using Cove.Gui;
using Microsoft.Extensions.DependencyInjection;
using Ryn.Core;
using Ryn.Ipc;
using Ryn.Plugins.Badge;
using Ryn.Plugins.GlobalShortcut;
using Ryn.Plugins.MenuBar;
using Ryn.Plugins.WebViewPane;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        const string channel = "dev";
        const string version = "0.1.0";
        var page = Environment.GetEnvironmentVariable("COVE_GUI_PAGE");
        var startPath = string.IsNullOrEmpty(page) ? "" : "/" + page;

        Func<CancellationToken, Task<Stream>> dial = ct => GuiEngineLauncher.ConnectOrSpawnAsync(channel, ct);
        var webRoot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
        var server = new LoopbackServer(webRoot, dial, version, channel);
        server.Start();

        var link = new EngineLink(dial, version, channel);

        RynApplication.CreateBuilder()
            .ConfigureOptions(o =>
            {
                o.Url = new Uri($"http://localhost:{LoopbackServer.DefaultPort}{startPath}");
                o.Title = "Cove";
                o.Width = 1100;
                o.Height = 720;
                o.TitleBarStyle = TitleBarStyle.Hidden;
                o.DevTools = Environment.GetEnvironmentVariable("COVE_DEVTOOLS") == "1";
            })
            .ConfigureServices(s =>
            {
                s.AddSingleton(link);
                s.AddSingleton<EngineEventForwarder>();
                s.AddSingleton<CoveGuiCommands>();
                s.AddRynCommands();
                s.AddAppCommands();
                s.AddCoveGuiCommands();
                s.AddRynMenuBar();
                s.AddRynBadge();
                s.AddRynGlobalShortcut();
                s.AddRynWebViewPane();
                s.AddPerfResultsCommand();
            })
            .Build()
            .Run();
    }
}
