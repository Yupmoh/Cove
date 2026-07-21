using System.Globalization;

namespace Cove.Gui;

public static class GuiLaunchOptions
{
    public static int ResolveLoopbackPort(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return LoopbackServer.DefaultPort;

        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var port)
            || port < 1024
            || port > 65535)
            throw new InvalidOperationException($"COVE_GUI_PORT must be an integer from 1024 through 65535; received '{value}'");

        return port;
    }
}
