using System.Text;
using Cove.Tui.Vt;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Cove.Tui.Backend;

public sealed class SpectreRegionRenderer
{
    private readonly VtEmulator _vt;

    public SpectreRegionRenderer(VtEmulator vt)
    {
        _vt = vt;
    }

    public void Render(IRenderable renderable, int width, int height)
    {
        var sb = new StringBuilder();
        var sw = new StringWriter(sb);
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.Yes,
            ColorSystem = ColorSystemSupport.Legacy,
            Interactive = InteractionSupport.No,
            Out = new AnsiConsoleOutput(sw),
        });
        console.Profile.Width = width;
        console.Profile.Height = height;
        console.Write(renderable);
        _vt.Feed(sb.ToString());
    }
}
