using Cove.Gui;
using Microsoft.Extensions.DependencyInjection;
using Ryn.Core;
using Ryn.Ipc;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        const string channel = "dev";
        const string version = "0.1.0";
        var page = Environment.GetEnvironmentVariable("COVE_GUI_PAGE");
        var startPath = string.IsNullOrEmpty(page) ? "" : "/" + page;

        Func<CancellationToken, Task<Stream>> dial = ct => EndpointDialer.DialAsync(channel, ct);
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
                o.DevTools = true;
            })
            .ConfigureServices(s =>
            {
                s.AddSingleton(link);
                s.AddSingleton<CoveGuiCommands>();
                s.AddRynCommands();
                s.AddAppCommands();
            })
            .Build()
            .Run();
    }
}
