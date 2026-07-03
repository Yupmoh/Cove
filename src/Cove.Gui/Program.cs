using Cove.Gui;
using Ryn.Core;
using Ryn.Ipc;

public static class Program
{
    [System.STAThread]
    public static void Main(string[] args)
    {
        var app = RynApplication.CreateBuilder()
            .ConfigureOptions(opts =>
            {
                if (args.Contains("--vite"))
                    opts.Url = new Uri("http://localhost:5173");
                else
                    opts.ContentDirectory = Path.Combine(AppContext.BaseDirectory, "wwwroot");
            })
            .ConfigureServices(services =>
            {
                services.AddRynCommands();
                services.AddAppCommands();
            })
            .Build();

        app.Run();
    }
}
