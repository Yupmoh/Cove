using System.Globalization;

namespace Cove.Gui;

public static class GuiLaunchOptions
{
    public const int StablePort = 7421;

    public static int ResolveLoopbackPort(string? value, string channel)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Equals(channel, "stable", StringComparison.Ordinal) ? StablePort : LoopbackServer.DefaultPort;

        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var port)
            || port < 1024
            || port > 65535)
            throw new InvalidOperationException($"COVE_GUI_PORT must be an integer from 1024 through 65535; received '{value}'");

        return port;
    }
}
