using Cove.Gui;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ryn.Core;
using Ryn.Ipc;
using Ryn.Plugins.Badge;
using Ryn.Plugins.Dialog;
using Ryn.Plugins.GlobalShortcut;
using Ryn.Plugins.MenuBar;
using Ryn.Plugins.Notification;
using Ryn.Plugins.Updater;
using Ryn.Plugins.WebViewPane;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        var channelEnv = Environment.GetEnvironmentVariable("COVE_CHANNEL");
        var channel = string.IsNullOrEmpty(channelEnv) ? "stable" : channelEnv;
        const string version = "0.4.0";
        var page = Environment.GetEnvironmentVariable("COVE_GUI_PAGE");
        var startPath = string.IsNullOrEmpty(page) ? "" : "/" + page;

        using var loggerFactory = GuiLogging.CreateFactory();
        GuiEngineLauncher.Logger = loggerFactory.CreateLogger("Cove.Gui.GuiEngineLauncher");
        var startupLog = loggerFactory.CreateLogger("Cove.Gui.Program");
        var url = $"http://localhost:{LoopbackServer.DefaultPort}{startPath}";
        startupLog.AppStarting(channel, version, url, Environment.GetEnvironmentVariable("COVE_ENGINE") ?? "bundled");

        Func<CancellationToken, Task<Stream>> dial = ct => GuiEngineLauncher.ConnectOrSpawnAsync(channel, ct);
        var webRoot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
        var capability = System.Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        var server = new LoopbackServer(webRoot, dial, version, channel, startupLog, capability: capability);
        server.Start();
        var capSeparator = url.Contains('?') ? "&" : "?";
        var authorizedUrl = $"{url}{capSeparator}__cap={capability}";

        var link = new EngineLink(dial, version, channel);
        link.SetLogger(loggerFactory.CreateLogger<EngineLink>());

        var app = RynApplication.CreateBuilder()
            .ConfigureOptions(o =>
            {
                o.Url = new Uri(authorizedUrl);
                o.Title = "Cove";
                o.Width = 1440;
                o.Height = 880;
                o.TitleBarStyle = TitleBarStyle.Overlay;
                o.TrafficLightPosition = new TrafficLightPosition(20, 15);
                o.Backdrop = BackdropMaterial.Blur;
                o.DevTools = Environment.GetEnvironmentVariable("COVE_DEVTOOLS") == "1";
                var iconPath = Path.Combine(AppContext.BaseDirectory, "assets", "app-icon.png");
                if (File.Exists(iconPath)) o.IconPath = iconPath;
            })
            .ConfigureServices(s =>
            {
                s.AddSingleton<ILoggerFactory>(loggerFactory);
                s.AddSingleton(link);
                s.AddSingleton<EngineEventForwarder>();
                s.AddSingleton<DictationHost>();
                s.AddSingleton<CoveGuiCommands>();
                s.AddRynCommands();
                s.AddCoveGuiCommands();
                s.AddRynMenuBar();
                s.AddRynBadge();
                s.AddRynGlobalShortcut();
                s.AddRynWebViewPane();
                s.AddRynNotification();
                s.AddRynDialog();
                s.AddRynUpdater(o =>
                {
                    o.GitHubOwner = "Yupmoh";
                    o.GitHubRepo = "Cove";
                    o.CurrentVersion = version;
                    o.PublicKey = UpdateKeys.PublicKey;
                });
                s.AddPerfResultsCommand();
            })
            .Build();

        _ = app.Services.GetRequiredService<EngineEventForwarder>();
        try
        {
            app.Run();
        }
        finally
        {
            try
            {
                link.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
            finally
            {
                server.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
        }
    }
}
