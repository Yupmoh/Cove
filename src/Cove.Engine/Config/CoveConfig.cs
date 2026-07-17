using System.Collections.Generic;
using System.Text.Json;

namespace Cove.Engine.Config;

internal static class ConfigValueCoercion
{
    public static int AsInt(JsonElement el, int fallback)
    {
        if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var i)) return i;
        if (el.ValueKind == JsonValueKind.String && int.TryParse(el.GetString(), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var si)) return si;
        return fallback;
    }
    public static double AsDouble(JsonElement el, double fallback)
    {
        if (el.ValueKind == JsonValueKind.Number && el.TryGetDouble(out var d)) return d;
        if (el.ValueKind == JsonValueKind.String && double.TryParse(el.GetString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var sd)) return sd;
        return fallback;
    }
    public static bool AsBool(JsonElement el, bool fallback)
    {
        if (el.ValueKind == JsonValueKind.False || el.ValueKind == JsonValueKind.True) return el.GetBoolean();
        if (el.ValueKind == JsonValueKind.String && bool.TryParse(el.GetString(), out var b)) return b;
        return fallback;
    }
    public static string AsString(JsonElement el, string fallback)
    {
        if (el.ValueKind == JsonValueKind.String) return el.GetString() ?? fallback;
        return fallback;
    }
}

public sealed class CoveConfig
{
    [Setting("Theme", "appearance", "select", "Active theme name", Options = new[] { "catppuccin-mocha", "cove-harbor", "cove-daybreak", "cove-midnight", "cove-shoal", "cove-beacon", "cove-chalk" })]
    public string Theme { get; set; } = "catppuccin-mocha";

    [Setting("Appearance", "appearance", "section", "Appearance settings")]
    public AppearanceSection Appearance { get; set; } = new();

    [Setting("Terminal", "terminal", "section", "Terminal settings")]
    public TerminalSection Terminal { get; set; } = new();
    [Setting("Markdown Editor", "terminal", "section", "Markdown editor settings")]
    public MarkdownEditorSection MarkdownEditor { get; set; } = new();
    [Setting("Updates", "updates", "section", "Update settings")]
    public UpdatesSection Updates { get; set; } = new();
    [Setting("Diagnostics", "diagnostics", "section", "Diagnostics settings")]
    public DiagnosticsSection Diagnostics { get; set; } = new();
    [Setting("Worktree", "bay", "section", "Worktree settings")]
    public WorktreeSection Worktree { get; set; } = new();
    [Setting("Keybindings", "keyboard", "section", "Keybinding overrides")]
    public KeybindingsSection Keybindings { get; set; } = new();
    [Setting("Language Servers", "tools", "section", "Registered language servers")]
    public LspSection Lsp { get; set; } = new();
    [Setting("Sessions", "bay", "section", "Session restoration settings")]
    public SessionSection Session { get; set; } = new();

    public Dictionary<string, JsonElement> Extra { get; } = new();

    public static CoveConfig ReadFrom(JsonDocument doc)
    {
        var cfg = new CoveConfig();
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            switch (prop.Name)
            {
                case "theme":
                    if (prop.Value.ValueKind == JsonValueKind.String)
                        cfg.Theme = prop.Value.GetString() ?? "catppuccin-mocha";
                    break;
                case "appearance":
                    cfg.Appearance = AppearanceSection.Read(prop.Value);
                    break;
                case "terminal":
                    cfg.Terminal = TerminalSection.Read(prop.Value);
                    break;
                case "markdown_editor":
                    cfg.MarkdownEditor = MarkdownEditorSection.Read(prop.Value);
                    break;
                case "updates":
                    cfg.Updates = UpdatesSection.Read(prop.Value);
                    break;
                case "diagnostics":
                    cfg.Diagnostics = DiagnosticsSection.Read(prop.Value);
                    break;
                case "worktree":
                    cfg.Worktree = WorktreeSection.Read(prop.Value);
                    break;
                case "keybindings":
                    cfg.Keybindings = KeybindingsSection.Read(prop.Value);
                    break;
                case "lsp":
                    cfg.Lsp = LspSection.Read(prop.Value);
                    break;
                case "session":
                    cfg.Session = SessionSection.Read(prop.Value);
                    break;
                default:
                    if (!TryRouteDottedKey(cfg, prop.Name, prop.Value))
                        cfg.Extra[prop.Name] = prop.Value.Clone();
                    break;
            }
        }
        return cfg;
    }

    private static bool TryRouteDottedKey(CoveConfig cfg, string key, JsonElement value)
    {
        var dot = key.IndexOf('.');
        if (dot <= 0) return false;
        var section = key.Substring(0, dot);
        var sub = key.Substring(dot + 1);
        switch (section)
        {
            case "terminal":
                switch (sub)
                {
                    case "fontFamily": cfg.Terminal.FontFamily = ConfigValueCoercion.AsString(value, cfg.Terminal.FontFamily); return true;
                    case "fontSize": cfg.Terminal.FontSize = ConfigValueCoercion.AsInt(value, cfg.Terminal.FontSize); return true;
                    case "lineHeight": cfg.Terminal.LineHeight = ConfigValueCoercion.AsDouble(value, cfg.Terminal.LineHeight); return true;
                    case "letterSpacing": cfg.Terminal.LetterSpacing = ConfigValueCoercion.AsDouble(value, cfg.Terminal.LetterSpacing); return true;
                    case "cursorStyle": cfg.Terminal.CursorStyle = ConfigValueCoercion.AsString(value, cfg.Terminal.CursorStyle); return true;
                    case "cursorBlink": cfg.Terminal.CursorBlink = ConfigValueCoercion.AsBool(value, cfg.Terminal.CursorBlink); return true;
                    case "scrollbackLines": cfg.Terminal.ScrollbackLines = ConfigValueCoercion.AsInt(value, cfg.Terminal.ScrollbackLines); return true;
                    case "padding": cfg.Terminal.Padding = ConfigValueCoercion.AsInt(value, cfg.Terminal.Padding); return true;
                    case "backgroundOpacity": cfg.Terminal.BackgroundOpacity = ConfigValueCoercion.AsDouble(value, cfg.Terminal.BackgroundOpacity); return true;
                }
                return false;
            case "markdown_editor":
                switch (sub)
                {
                    case "defaultFont": cfg.MarkdownEditor.DefaultFont = ConfigValueCoercion.AsString(value, cfg.MarkdownEditor.DefaultFont); return true;
                    case "fontSize": cfg.MarkdownEditor.FontSize = ConfigValueCoercion.AsInt(value, cfg.MarkdownEditor.FontSize); return true;
                    case "textAlign": cfg.MarkdownEditor.TextAlign = ConfigValueCoercion.AsString(value, cfg.MarkdownEditor.TextAlign); return true;
                    case "bookView": cfg.MarkdownEditor.BookView = ConfigValueCoercion.AsBool(value, cfg.MarkdownEditor.BookView); return true;
                    case "bookViewWidth": cfg.MarkdownEditor.BookViewWidth = ConfigValueCoercion.AsString(value, cfg.MarkdownEditor.BookViewWidth); return true;
                    case "bookViewMargin": cfg.MarkdownEditor.BookViewMargin = ConfigValueCoercion.AsString(value, cfg.MarkdownEditor.BookViewMargin); return true;
                    case "defaultViewMode": cfg.MarkdownEditor.DefaultViewMode = ConfigValueCoercion.AsString(value, cfg.MarkdownEditor.DefaultViewMode); return true;
                }
                return false;
            case "worktree":
                switch (sub)
                {
                    case "defaultLocationPattern": cfg.Worktree.DefaultLocationPattern = ConfigValueCoercion.AsString(value, cfg.Worktree.DefaultLocationPattern); return true;
                }
                return false;
            case "updates":
                switch (sub)
                {
                    case "checkOnLaunch": cfg.Updates.CheckOnLaunch = ConfigValueCoercion.AsBool(value, cfg.Updates.CheckOnLaunch); return true;
                }
                return false;
            case "diagnostics":
                switch (sub)
                {
                    case "enabled": cfg.Diagnostics.Enabled = ConfigValueCoercion.AsBool(value, cfg.Diagnostics.Enabled); return true;
                    case "captureTerminalStats": cfg.Diagnostics.CaptureTerminalStats = ConfigValueCoercion.AsBool(value, cfg.Diagnostics.CaptureTerminalStats); return true;
                    case "captureMemoryStats": cfg.Diagnostics.CaptureMemoryStats = ConfigValueCoercion.AsBool(value, cfg.Diagnostics.CaptureMemoryStats); return true;
                    case "flushIntervalMs": cfg.Diagnostics.FlushIntervalMs = ConfigValueCoercion.AsInt(value, cfg.Diagnostics.FlushIntervalMs); return true;
                }
                return false;
            case "session":
                switch (sub)
                {
                    case "restoreOnLaunch": cfg.Session.RestoreOnLaunch = ConfigValueCoercion.AsBool(value, cfg.Session.RestoreOnLaunch); return true;
                }
                return false;
            default:
                return false;
        }
    }

    public void WriteTo(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteString("theme", Theme);
        writer.WritePropertyName("appearance");
        Appearance.WriteTo(writer);
        writer.WritePropertyName("terminal");
        Terminal.WriteTo(writer);
        writer.WritePropertyName("markdown_editor");
        MarkdownEditor.WriteTo(writer);
        writer.WritePropertyName("updates");
        Updates.WriteTo(writer);
        writer.WritePropertyName("diagnostics");
        Diagnostics.WriteTo(writer);
        writer.WritePropertyName("worktree");
        Worktree.WriteTo(writer);
        writer.WritePropertyName("keybindings");
        Keybindings.WriteTo(writer);
        writer.WritePropertyName("lsp");
        Lsp.WriteTo(writer);
        writer.WritePropertyName("session");
        Session.WriteTo(writer);
        foreach (var kv in Extra)
        {
            writer.WritePropertyName(kv.Key);
            kv.Value.WriteTo(writer);
        }
        writer.WriteEndObject();
    }
}

public sealed class AppearanceSection
{
    [Setting("UI Scale", "appearance", "number", "Interface scale multiplier (0.8-1.5)")]
    public double UiScale { get; set; } = 1.0;
    [Setting("Layout Gap", "appearance", "number", "Gap between nooks in pixels")]
    public int LayoutGap { get; set; } = 4;
    [Setting("Icon Set", "appearance", "select", "Icon set style", Options = new[] { "default", "outline", "filled" })]
    public string IconSet { get; set; } = "default";
    [Setting("Wallpaper", "appearance", "text", "Wallpaper image path or URL")]
    public string Wallpaper { get; set; } = "";
    [Setting("Accent Override", "appearance", "text", "Override accent color hex (empty for theme default)")]
    public string Accent { get; set; } = "";
    [Setting("Nook Light", "appearance", "toggle", "Lighten inactive nooks")]
    public bool NookLight { get; set; } = false;

    public static AppearanceSection Read(JsonElement el)
    {
        var s = new AppearanceSection();
        if (el.ValueKind != JsonValueKind.Object) return s;
        foreach (var prop in el.EnumerateObject())
        {
            switch (prop.Name)
            {
                case "uiScale": s.UiScale = ConfigValueCoercion.AsDouble(prop.Value, s.UiScale); break;
                case "layoutGap": s.LayoutGap = ConfigValueCoercion.AsInt(prop.Value, s.LayoutGap); break;
                case "iconSet": if (prop.Value.ValueKind == JsonValueKind.String) s.IconSet = prop.Value.GetString() ?? s.IconSet; break;
                case "wallpaper": if (prop.Value.ValueKind == JsonValueKind.String) s.Wallpaper = prop.Value.GetString() ?? s.Wallpaper; break;
                case "accent": if (prop.Value.ValueKind == JsonValueKind.String) s.Accent = prop.Value.GetString() ?? s.Accent; break;
                case "nookLight": s.NookLight = ConfigValueCoercion.AsBool(prop.Value, s.NookLight); break;
            }
        }
        return s;
    }

    public void WriteTo(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteNumber("uiScale", UiScale);
        writer.WriteNumber("layoutGap", LayoutGap);
        writer.WriteString("iconSet", IconSet);
        writer.WriteString("wallpaper", Wallpaper);
        writer.WriteString("accent", Accent);
        writer.WriteBoolean("nookLight", NookLight);
        writer.WriteEndObject();
    }
}

public sealed class TerminalSection
{
    [Setting("Font Family", "terminal", "text", "Terminal font family")]
    public string FontFamily { get; set; } = "Menlo, monospace";
    [Setting("Font Size", "terminal", "number", "Terminal font size in pixels")]
    public int FontSize { get; set; } = 11;
    [Setting("Line Height", "terminal", "number", "Terminal line height")]
    public double LineHeight { get; set; } = 1.2;
    [Setting("Letter Spacing", "terminal", "number", "Terminal letter spacing")]
    public double LetterSpacing { get; set; } = 0.0;
    [Setting("Cursor Style", "terminal", "select", "Cursor style", Options = new[] { "block", "bar", "underline" })]
    public string CursorStyle { get; set; } = "block";
    [Setting("Cursor Blink", "terminal", "toggle", "Cursor blink")]
    public bool CursorBlink { get; set; } = true;
    [Setting("Scrollback Lines", "terminal", "number", "Scrollback line count")]
    public int ScrollbackLines { get; set; } = 10000;
    [Setting("Padding", "terminal", "number", "Terminal padding in pixels")]
    public int Padding { get; set; } = 8;
    [Setting("Background Opacity", "terminal", "number", "Background opacity 0-1")]
    public double BackgroundOpacity { get; set; } = 1.0;

    public static TerminalSection Read(JsonElement el)
    {
        var s = new TerminalSection();
        if (el.ValueKind != JsonValueKind.Object) return s;
        foreach (var prop in el.EnumerateObject())
        {
            switch (prop.Name)
            {
                case "fontFamily": if (prop.Value.ValueKind == JsonValueKind.String) s.FontFamily = prop.Value.GetString() ?? s.FontFamily; break;
                case "fontSize": s.FontSize = ConfigValueCoercion.AsInt(prop.Value, s.FontSize); break;
                case "lineHeight": s.LineHeight = ConfigValueCoercion.AsDouble(prop.Value, s.LineHeight); break;
                case "letterSpacing": s.LetterSpacing = ConfigValueCoercion.AsDouble(prop.Value, s.LetterSpacing); break;
                case "cursorStyle": if (prop.Value.ValueKind == JsonValueKind.String) s.CursorStyle = prop.Value.GetString() ?? s.CursorStyle; break;
                case "cursorBlink": s.CursorBlink = ConfigValueCoercion.AsBool(prop.Value, s.CursorBlink); break;
                case "scrollbackLines": s.ScrollbackLines = ConfigValueCoercion.AsInt(prop.Value, s.ScrollbackLines); break;
                case "padding": s.Padding = ConfigValueCoercion.AsInt(prop.Value, s.Padding); break;
                case "backgroundOpacity": s.BackgroundOpacity = ConfigValueCoercion.AsDouble(prop.Value, s.BackgroundOpacity); break;
            }
        }
        return s;
    }

    public void WriteTo(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteString("fontFamily", FontFamily);
        writer.WriteNumber("fontSize", FontSize);
        writer.WriteNumber("lineHeight", LineHeight);
        writer.WriteNumber("letterSpacing", LetterSpacing);
        writer.WriteString("cursorStyle", CursorStyle);
        writer.WriteBoolean("cursorBlink", CursorBlink);
        writer.WriteNumber("scrollbackLines", ScrollbackLines);
        writer.WriteNumber("padding", Padding);
        writer.WriteNumber("backgroundOpacity", BackgroundOpacity);
        writer.WriteEndObject();
    }
}

public sealed class MarkdownEditorSection
{
    [Setting("Default Font", "terminal", "text", "Default markdown editor font")]
    public string DefaultFont { get; set; } = "Default";
    [Setting("Font Size", "terminal", "number", "Markdown editor font size")]
    public int FontSize { get; set; } = 14;
    [Setting("Text Align", "terminal", "select", "Text alignment", Options = new[] { "left", "center", "right" })]
    public string TextAlign { get; set; } = "left";
    [Setting("Book View", "terminal", "toggle", "Enable book view")]
    public bool BookView { get; set; } = false;
    [Setting("Book View Width", "terminal", "text", "Book view width")]
    public string BookViewWidth { get; set; } = "5.5in";
    [Setting("Book View Margin", "terminal", "text", "Book view margin")]
    public string BookViewMargin { get; set; } = "0.5in";
    [Setting("Default View Mode", "terminal", "select", "Default view mode", Options = new[] { "rte", "source" })]
    public string DefaultViewMode { get; set; } = "rte";

    public static MarkdownEditorSection Read(JsonElement el)
    {
        var s = new MarkdownEditorSection();
        if (el.ValueKind != JsonValueKind.Object) return s;
        foreach (var prop in el.EnumerateObject())
        {
            switch (prop.Name)
            {
                case "defaultFont": if (prop.Value.ValueKind == JsonValueKind.String) s.DefaultFont = prop.Value.GetString() ?? s.DefaultFont; break;
                case "fontSize": s.FontSize = ConfigValueCoercion.AsInt(prop.Value, s.FontSize); break;
                case "textAlign": if (prop.Value.ValueKind == JsonValueKind.String) s.TextAlign = prop.Value.GetString() ?? s.TextAlign; break;
                case "bookView": s.BookView = ConfigValueCoercion.AsBool(prop.Value, s.BookView); break;
                case "bookViewWidth": if (prop.Value.ValueKind == JsonValueKind.String) s.BookViewWidth = prop.Value.GetString() ?? s.BookViewWidth; break;
                case "bookViewMargin": if (prop.Value.ValueKind == JsonValueKind.String) s.BookViewMargin = prop.Value.GetString() ?? s.BookViewMargin; break;
                case "defaultViewMode": if (prop.Value.ValueKind == JsonValueKind.String) s.DefaultViewMode = prop.Value.GetString() ?? s.DefaultViewMode; break;
            }
        }
        return s;
    }

    public void WriteTo(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteString("defaultFont", DefaultFont);
        writer.WriteNumber("fontSize", FontSize);
        writer.WriteString("textAlign", TextAlign);
        writer.WriteBoolean("bookView", BookView);
        writer.WriteString("bookViewWidth", BookViewWidth);
        writer.WriteString("bookViewMargin", BookViewMargin);
        writer.WriteString("defaultViewMode", DefaultViewMode);
        writer.WriteEndObject();
    }
}

public sealed class UpdatesSection
{
    [Setting("Check On Launch", "updates", "toggle", "Check for updates on launch")]
    public bool CheckOnLaunch { get; set; } = true;

    public static UpdatesSection Read(JsonElement el)
    {
        var s = new UpdatesSection();
        if (el.ValueKind != JsonValueKind.Object) return s;
        foreach (var prop in el.EnumerateObject())
        {
            if (prop.Name == "checkOnLaunch") s.CheckOnLaunch = ConfigValueCoercion.AsBool(prop.Value, s.CheckOnLaunch);
        }
        return s;
    }

    public void WriteTo(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteBoolean("checkOnLaunch", CheckOnLaunch);
        writer.WriteEndObject();
    }
}

public sealed class DiagnosticsSection
{
    [Setting("Diagnostics Enabled", "diagnostics", "toggle", "Enable diagnostics")]
    public bool Enabled { get; set; } = false;
    [Setting("Capture Terminal Stats", "diagnostics", "toggle", "Capture terminal statistics")]
    public bool CaptureTerminalStats { get; set; } = true;
    [Setting("Capture Memory Stats", "diagnostics", "toggle", "Capture memory statistics")]
    public bool CaptureMemoryStats { get; set; } = true;
    [Setting("Flush Interval", "diagnostics", "number", "Flush interval in milliseconds")]
    public int FlushIntervalMs { get; set; } = 2000;

    public static DiagnosticsSection Read(JsonElement el)
    {
        var s = new DiagnosticsSection();
        if (el.ValueKind != JsonValueKind.Object) return s;
        foreach (var prop in el.EnumerateObject())
        {
            switch (prop.Name)
            {
                case "enabled": s.Enabled = ConfigValueCoercion.AsBool(prop.Value, s.Enabled); break;
                case "captureTerminalStats": s.CaptureTerminalStats = ConfigValueCoercion.AsBool(prop.Value, s.CaptureTerminalStats); break;
                case "captureMemoryStats": s.CaptureMemoryStats = ConfigValueCoercion.AsBool(prop.Value, s.CaptureMemoryStats); break;
                case "flushIntervalMs": s.FlushIntervalMs = ConfigValueCoercion.AsInt(prop.Value, s.FlushIntervalMs); break;
            }
        }
        return s;
    }

    public void WriteTo(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteBoolean("enabled", Enabled);
        writer.WriteBoolean("captureTerminalStats", CaptureTerminalStats);
        writer.WriteBoolean("captureMemoryStats", CaptureMemoryStats);
        writer.WriteNumber("flushIntervalMs", FlushIntervalMs);
        writer.WriteEndObject();
    }
}

public sealed class WorktreeSection
{
    [Setting("Default Location Pattern", "bay", "text", "Worktree default location pattern")]
    public string DefaultLocationPattern { get; set; } = "../{repo}-worktrees/{branch}";

    public static WorktreeSection Read(JsonElement el)
    {
        var s = new WorktreeSection();
        if (el.ValueKind != JsonValueKind.Object) return s;
        foreach (var prop in el.EnumerateObject())
        {
            switch (prop.Name)
            {
                case "defaultLocationPattern": if (prop.Value.ValueKind == JsonValueKind.String) s.DefaultLocationPattern = prop.Value.GetString() ?? s.DefaultLocationPattern; break;
            }
        }
        return s;
    }

    public void WriteTo(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteString("defaultLocationPattern", DefaultLocationPattern);
        writer.WriteEndObject();
    }
}
public sealed class KeybindingsSection
{
    [Setting("Keybindings", "keyboard", "text", "Custom keybinding overrides")]
    public Dictionary<string, JsonElement> Bindings { get; set; } = new();

    public static KeybindingsSection Read(JsonElement el)
    {
        var s = new KeybindingsSection();
        if (el.ValueKind != JsonValueKind.Object) return s;
        foreach (var prop in el.EnumerateObject())
            s.Bindings[prop.Name] = prop.Value.Clone();
        return s;
    }

    public void WriteTo(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        foreach (var kv in Bindings)
        {
            writer.WritePropertyName(kv.Key);
            kv.Value.WriteTo(writer);
        }
        writer.WriteEndObject();
    }
}
public sealed class LspConfigServerEntry
{
    public List<string> Languages { get; set; } = new();
    public string Command { get; set; } = "";
    public List<string> Args { get; set; } = new();
}

public sealed class LspSection
{
    [Setting("Servers", "tools", "text", "Language server entries ({languages, command, args}); entries override built-ins for the same language")]
    public List<LspConfigServerEntry> Servers { get; set; } = new();

    public static LspSection Read(JsonElement el)
    {
        var s = new LspSection();
        if (el.ValueKind != JsonValueKind.Object) return s;
        foreach (var prop in el.EnumerateObject())
        {
            if (prop.Name != "servers" || prop.Value.ValueKind != JsonValueKind.Array) continue;
            foreach (var item in prop.Value.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) continue;
                var entry = new LspConfigServerEntry();
                foreach (var field in item.EnumerateObject())
                {
                    switch (field.Name)
                    {
                        case "languages":
                            if (field.Value.ValueKind == JsonValueKind.Array)
                                foreach (var lang in field.Value.EnumerateArray())
                                    if (lang.ValueKind == JsonValueKind.String)
                                        entry.Languages.Add(lang.GetString() ?? "");
                            break;
                        case "command":
                            entry.Command = ConfigValueCoercion.AsString(field.Value, entry.Command);
                            break;
                        case "args":
                            if (field.Value.ValueKind == JsonValueKind.Array)
                                foreach (var arg in field.Value.EnumerateArray())
                                    if (arg.ValueKind == JsonValueKind.String)
                                        entry.Args.Add(arg.GetString() ?? "");
                            break;
                    }
                }
                if (entry.Command.Length > 0 && entry.Languages.Count > 0)
                    s.Servers.Add(entry);
            }
        }
        return s;
    }

    public void WriteTo(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteStartArray("servers");
        foreach (var entry in Servers)
        {
            writer.WriteStartObject();
            writer.WriteStartArray("languages");
            foreach (var lang in entry.Languages)
                writer.WriteStringValue(lang);
            writer.WriteEndArray();
            writer.WriteString("command", entry.Command);
            writer.WriteStartArray("args");
            foreach (var arg in entry.Args)
                writer.WriteStringValue(arg);
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        writer.WriteEndObject();
    }
}

public sealed class SessionSection
{
    [Setting("Restore On Launch", "bay", "toggle", "Respawn terminal and agent nooks when the daemon restarts")]
    public bool RestoreOnLaunch { get; set; } = true;

    public static SessionSection Read(JsonElement el)
    {
        var s = new SessionSection();
        if (el.ValueKind != JsonValueKind.Object) return s;
        foreach (var prop in el.EnumerateObject())
        {
            switch (prop.Name)
            {
                case "restoreOnLaunch": s.RestoreOnLaunch = ConfigValueCoercion.AsBool(prop.Value, s.RestoreOnLaunch); break;
            }
        }
        return s;
    }

    public void WriteTo(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteBoolean("restoreOnLaunch", RestoreOnLaunch);
        writer.WriteEndObject();
    }
}
