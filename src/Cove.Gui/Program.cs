using Cove.Gui;
using Cove.Platform;
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
        var version = CoveBuild.InformationalVersion;
        var page = Environment.GetEnvironmentVariable("COVE_GUI_PAGE");
        var startPath = string.IsNullOrEmpty(page) ? "" : "/" + page;
        var requestedGuiPort = GuiLaunchOptions.ResolveLoopbackPort(Environment.GetEnvironmentVariable("COVE_GUI_PORT"), channel);

        using var loggerFactory = GuiLogging.CreateFactory();
        var fileSystem = SystemPlatformFileSystem.Instance;
        GuiEngineLauncher.Logger = loggerFactory.CreateLogger("Cove.Gui.GuiEngineLauncher");
        var startupLog = loggerFactory.CreateLogger("Cove.Gui.Program");

        Func<CancellationToken, Task<Stream>> dial = ct => GuiEngineLauncher.ConnectOrSpawnAsync(channel, version, ct);
        var webRoot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
        var capability = System.Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        var mediaLeases = new MediaLeaseRegistry(
            TimeSpan.FromHours(8),
            fileSystem: fileSystem);
        var server = new LoopbackServer(webRoot, dial, version, channel, startupLog, port: requestedGuiPort, capability: capability, mediaLeases: mediaLeases, fileSystem: fileSystem);
        server.Start();
        var url = $"http://localhost:{server.Port}{startPath}";
        startupLog.AppStarting(channel, version, url, Environment.GetEnvironmentVariable("COVE_ENGINE") ?? "bundled");
        var capSeparator = url.Contains('?') ? "&" : "?";
        var authorizedUrl = $"{url}{capSeparator}__cap={capability}";

        var link = new EngineLink(dial, version, channel);
        link.SetLogger(loggerFactory.CreateLogger<EngineLink>());
        var checkpointLink = link.CreateBackgroundLink();
        checkpointLink.SetLogger(loggerFactory.CreateLogger("Cove.Gui.EngineLink.Checkpoint"));

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
                if (fileSystem.FileExists(iconPath)) o.IconPath = iconPath;
            })
            .ConfigureServices(s =>
            {
                s.AddSingleton<ILoggerFactory>(loggerFactory);
                s.AddSingleton(link);
                s.AddSingleton(mediaLeases);
                s.AddSingleton<EngineEventForwarder>();
                s.AddSingleton(provider => new DictationHost(
                    provider.GetRequiredService<ILogger<DictationHost>>(),
                    link.RequestAsync));
                s.AddSingleton(provider => new CoveGuiCommands(
                    link,
                    checkpointLink,
                    provider.GetRequiredService<ILogger<CoveGuiCommands>>(),
                    provider.GetRequiredService<DictationHost>(),
                    mediaLeases));
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
            })
            .Build();

        _ = app.Services.GetRequiredService<EngineEventForwarder>();
        var dictation = app.Services.GetRequiredService<DictationHost>();
        try
        {
            app.Run();
        }
        finally
        {
            try
            {
                dictation.DisposeAsync()
                    .AsTask()
                    .GetAwaiter()
                    .GetResult();
            }
            catch (Exception ex)
            {
                startupLog.GuiResourceDisposalFailed(
                    "dictation",
                    ex.Message);
            }
            try
            {
                checkpointLink.DisposeAsync()
                    .AsTask()
                    .GetAwaiter()
                    .GetResult();
            }
            catch (Exception ex)
            {
                startupLog.GuiResourceDisposalFailed(
                    "checkpoint-engine-link",
                    ex.Message);
            }
            try
            {
                link.DisposeAsync()
                    .AsTask()
                    .GetAwaiter()
                    .GetResult();
            }
            catch (Exception ex)
            {
                startupLog.GuiResourceDisposalFailed(
                    "engine-link",
                    ex.Message);
            }
            try
            {
                server.DisposeAsync()
                    .AsTask()
                    .GetAwaiter()
                    .GetResult();
            }
            catch (Exception ex)
            {
                startupLog.GuiResourceDisposalFailed(
                    "loopback-server",
                    ex.Message);
            }
        }
    }
}
