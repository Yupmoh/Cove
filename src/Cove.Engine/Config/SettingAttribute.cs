namespace Cove.Engine.Config;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class SettingAttribute(string label, string tab, string control = "text", string? description = null) : Attribute
{
    public string Label { get; } = label;
    public string Tab { get; } = tab;
    public string Control { get; } = control;
    public string? Description { get; } = description;
}

public sealed record ConfigSchemaEntry(string Key, string Label, string Tab, string Control, string? Description, string Type);

public static class ConfigSchemaGenerator
{
    public static IReadOnlyList<ConfigSchemaEntry> Generate()
    {
        var entries = new List<ConfigSchemaEntry>();

        entries.Add(new ConfigSchemaEntry("theme", "Theme", "appearance", "select", "Active theme name", "string"));

        entries.Add(new ConfigSchemaEntry("terminal.fontFamily", "Font Family", "terminal", "text", "Terminal font family", "string"));
        entries.Add(new ConfigSchemaEntry("terminal.fontSize", "Font Size", "terminal", "number", "Terminal font size in pixels", "int"));
        entries.Add(new ConfigSchemaEntry("terminal.fontLigatures", "Font Ligatures", "terminal", "toggle", "Enable font ligatures", "bool"));
        entries.Add(new ConfigSchemaEntry("terminal.lineHeight", "Line Height", "terminal", "number", "Terminal line height", "double"));
        entries.Add(new ConfigSchemaEntry("terminal.cursorStyle", "Cursor Style", "terminal", "select", "Cursor style", "string"));
        entries.Add(new ConfigSchemaEntry("terminal.cursorBlink", "Cursor Blink", "terminal", "toggle", "Cursor blink", "bool"));
        entries.Add(new ConfigSchemaEntry("terminal.scrollbackLines", "Scrollback Lines", "terminal", "number", "Scrollback line count", "int"));
        entries.Add(new ConfigSchemaEntry("terminal.padding", "Padding", "terminal", "number", "Terminal padding in pixels", "int"));
        entries.Add(new ConfigSchemaEntry("terminal.backgroundOpacity", "Background Opacity", "terminal", "number", "Background opacity 0-1", "double"));

        entries.Add(new ConfigSchemaEntry("markdownEditor.defaultFont", "Default Font", "terminal", "text", "Default markdown editor font", "string"));

        entries.Add(new ConfigSchemaEntry("updates.channel", "Update Channel", "updates", "select", "Update channel", "string"));
        entries.Add(new ConfigSchemaEntry("updates.checkOnLaunch", "Check On Launch", "updates", "toggle", "Check for updates on launch", "bool"));
        entries.Add(new ConfigSchemaEntry("updates.checkIntervalHours", "Check Interval", "updates", "number", "Check interval in hours", "int"));

        entries.Add(new ConfigSchemaEntry("diagnostics.enabled", "Diagnostics Enabled", "diagnostics", "toggle", "Enable diagnostics", "bool"));

        entries.Add(new ConfigSchemaEntry("worktree.defaultLocationPattern", "Default Location Pattern", "workspace", "text", "Worktree default location pattern", "string"));

        entries.Add(new ConfigSchemaEntry("telemetry.enabled", "Telemetry Enabled", "privacy", "toggle", "Enable anonymous telemetry", "bool"));

        entries.Add(new ConfigSchemaEntry("remoteConfig.dismissedBannerIds", "Dismissed Banners", "privacy", "text", "Dismissed remote config banner IDs", "array"));

        entries.Add(new ConfigSchemaEntry("keybindings.bindings", "Keybindings", "keyboard", "text", "Custom keybinding overrides", "object"));

        entries.Add(new ConfigSchemaEntry("pushToTalk.enabled", "Push To Talk", "audio", "toggle", "Enable push to talk", "bool"));

        entries.Add(new ConfigSchemaEntry("speech.gain", "Speech Gain", "audio", "number", "Speech gain", "double"));

        return entries;
    }

    public static string GenerateReferenceDoc()
    {
        var entries = Generate();
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("# Configuration Reference");
        sb.AppendLine();
        sb.AppendLine("All configuration keys for Cove, grouped by settings tab.");
        sb.AppendLine();

        var byTab = entries.GroupBy(e => e.Tab).OrderBy(g => g.Key);
        foreach (var group in byTab)
        {
            sb.AppendLine($"## {group.Key}");
            sb.AppendLine();
            sb.AppendLine("| Key | Label | Type | Control | Description |");
            sb.AppendLine("|-----|-------|------|---------|-------------|");
            foreach (var entry in group.OrderBy(e => e.Key))
            {
                sb.AppendLine($"| `{entry.Key}` | {entry.Label} | {entry.Type} | {entry.Control} | {entry.Description ?? ""} |");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
