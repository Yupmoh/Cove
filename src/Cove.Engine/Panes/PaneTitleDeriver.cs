using Microsoft.Extensions.Logging;

namespace Cove.Engine.Panes;

public sealed class PaneTitleDeriver
{
    private readonly ILogger _logger;

    public PaneTitleDeriver(ILogger? logger = null)
    {
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
    }

    public string DeriveTitle(string? adapter, string? command, string? workingDirectory, string? customTitle)
    {
        if (!string.IsNullOrWhiteSpace(customTitle))
            return customTitle;

        if (!string.IsNullOrWhiteSpace(adapter))
        {
            var adapterTitle = FormatAdapterTitle(adapter);
            if (!string.IsNullOrEmpty(adapterTitle))
                return adapterTitle;
        }

        if (!string.IsNullOrWhiteSpace(command))
        {
            var cmdTitle = FormatCommandTitle(command);
            if (!string.IsNullOrEmpty(cmdTitle))
                return cmdTitle;
        }

        if (!string.IsNullOrWhiteSpace(workingDirectory))
        {
            var dirTitle = FormatDirectoryTitle(workingDirectory);
            if (!string.IsNullOrEmpty(dirTitle))
                return dirTitle;
        }

        _logger.LogWarning("pane-title: could not derive title from adapter={adapter} command={command} cwd={cwd}", adapter, command, workingDirectory);
        return "terminal";
    }

    private static string FormatAdapterTitle(string adapter)
    {
        var name = adapter.Trim();
        if (name.Length == 0) return "";

        if (name.Contains(':'))
            name = name.Split(':')[0];

        if (name.Contains('/'))
            name = name.Split('/').Last();

        if (name.Length > 32)
            name = name.Substring(0, 29) + "...";

        return name;
    }

    private static string FormatCommandTitle(string command)
    {
        var cmd = command.Trim();
        if (cmd.Length == 0) return "";

        if (cmd.Contains('/'))
            cmd = cmd.Split('/').Last();

        if (cmd.Contains('\\'))
            cmd = cmd.Split('\\').Last();

        if (cmd.StartsWith("python") || cmd.StartsWith("node") || cmd.StartsWith("ruby") || cmd.StartsWith("perl"))
        {
            var parts = cmd.Split(' ', 2);
            if (parts.Length > 1)
            {
                var script = parts[1].Trim();
                if (script.Contains('/'))
                    script = script.Split('/').Last();
                if (script.Contains('\\'))
                    script = script.Split('\\').Last();
                return script.Length > 0 ? script : parts[0];
            }
        }

        if (cmd.Length > 32)
            cmd = cmd.Substring(0, 29) + "...";

        return cmd;
    }

    private static string FormatDirectoryTitle(string workingDirectory)
    {
        var dir = workingDirectory;
        if (dir == "/" || dir == "\\")
            return dir;
        dir = dir.TrimEnd('/', '\\');
        if (dir.Length == 0) return "";

        var name = dir;
        if (name.Contains('/'))
            name = name.Split('/').Last();
        if (name.Contains('\\'))
            name = name.Split('\\').Last();

        if (name.Length == 0)
            name = "/";

        if (name.Length > 32)
            name = name.Substring(0, 29) + "...";

        return name;
    }
}
