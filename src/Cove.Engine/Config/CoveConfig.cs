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
internal sealed class CoveConfig
{
    public string Theme { get; set; } = "cove";

    public TerminalSection Terminal { get; set; } = new();
    public MarkdownEditorSection MarkdownEditor { get; set; } = new();
    public UpdatesSection Updates { get; set; } = new();
    public DiagnosticsSection Diagnostics { get; set; } = new();
    public WorktreeSection Worktree { get; set; } = new();
    public TelemetrySection Telemetry { get; set; } = new();
    public RemoteConfigSection RemoteConfig { get; set; } = new();
    public KeybindingsSection Keybindings { get; set; } = new();
    public PushToTalkSection PushToTalk { get; set; } = new();
    public SpeechSection Speech { get; set; } = new();
    public LspServersSection LspServers { get; set; } = new();
    public AdapterCommandsSection AdapterCommands { get; set; } = new();

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
                        cfg.Theme = prop.Value.GetString() ?? "cove";
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
                case "telemetry":
                    cfg.Telemetry = TelemetrySection.Read(prop.Value);
                    break;
                case "remoteConfig":
                    cfg.RemoteConfig = RemoteConfigSection.Read(prop.Value);
                    break;
                case "keybindings":
                    cfg.Keybindings = KeybindingsSection.Read(prop.Value);
                    break;
                case "pushToTalk":
                    cfg.PushToTalk = PushToTalkSection.Read(prop.Value);
                    break;
                case "speech":
                    cfg.Speech = SpeechSection.Read(prop.Value);
                    break;
                case "lspServers":
                    cfg.LspServers = LspServersSection.Read(prop.Value);
                    break;
                case "adapterCommands":
                    cfg.AdapterCommands = AdapterCommandsSection.Read(prop.Value);
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
                    case "fontLigatures": cfg.Terminal.FontLigatures = ConfigValueCoercion.AsBool(value, cfg.Terminal.FontLigatures); return true;
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
                    case "imagePasteFolder": cfg.MarkdownEditor.ImagePasteFolder = ConfigValueCoercion.AsString(value, cfg.MarkdownEditor.ImagePasteFolder); return true;
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
                    case "autoInstall": cfg.Updates.AutoInstall = ConfigValueCoercion.AsBool(value, cfg.Updates.AutoInstall); return true;
                    case "autoUpdateAdapters": cfg.Updates.AutoUpdateAdapters = ConfigValueCoercion.AsBool(value, cfg.Updates.AutoUpdateAdapters); return true;
                    case "channel": cfg.Updates.Channel = ConfigValueCoercion.AsString(value, cfg.Updates.Channel); return true;
                    case "checkIntervalHours": cfg.Updates.CheckIntervalHours = ConfigValueCoercion.AsInt(value, cfg.Updates.CheckIntervalHours); return true;
                }
                return false;
            case "diagnostics":
                switch (sub)
                {
                    case "enabled": cfg.Diagnostics.Enabled = ConfigValueCoercion.AsBool(value, cfg.Diagnostics.Enabled); return true;
                    case "captureLongTasks": cfg.Diagnostics.CaptureLongTasks = ConfigValueCoercion.AsBool(value, cfg.Diagnostics.CaptureLongTasks); return true;
                    case "captureRenderStats": cfg.Diagnostics.CaptureRenderStats = ConfigValueCoercion.AsBool(value, cfg.Diagnostics.CaptureRenderStats); return true;
                    case "captureIpcTimings": cfg.Diagnostics.CaptureIpcTimings = ConfigValueCoercion.AsBool(value, cfg.Diagnostics.CaptureIpcTimings); return true;
                    case "captureTerminalStats": cfg.Diagnostics.CaptureTerminalStats = ConfigValueCoercion.AsBool(value, cfg.Diagnostics.CaptureTerminalStats); return true;
                    case "captureMemoryStats": cfg.Diagnostics.CaptureMemoryStats = ConfigValueCoercion.AsBool(value, cfg.Diagnostics.CaptureMemoryStats); return true;
                    case "flushIntervalMs": cfg.Diagnostics.FlushIntervalMs = ConfigValueCoercion.AsInt(value, cfg.Diagnostics.FlushIntervalMs); return true;
                }
                return false;
            case "telemetry":
                switch (sub)
                {
                    case "enabled": cfg.Telemetry.Enabled = ConfigValueCoercion.AsBool(value, cfg.Telemetry.Enabled); return true;
                    case "analyticsOptIn": cfg.Telemetry.AnalyticsOptIn = ConfigValueCoercion.AsBool(value, cfg.Telemetry.AnalyticsOptIn); return true;
                    case "coreTelemetryDisclosed": cfg.Telemetry.CoreTelemetryDisclosed = ConfigValueCoercion.AsBool(value, cfg.Telemetry.CoreTelemetryDisclosed); return true;
                }
                return false;
            case "pushToTalk":
                switch (sub)
                {
                    case "enabled": cfg.PushToTalk.Enabled = ConfigValueCoercion.AsBool(value, cfg.PushToTalk.Enabled); return true;
                    case "keyCode": cfg.PushToTalk.KeyCode = ConfigValueCoercion.AsInt(value, cfg.PushToTalk.KeyCode); return true;
                    case "isModifier": cfg.PushToTalk.IsModifier = ConfigValueCoercion.AsBool(value, cfg.PushToTalk.IsModifier); return true;
                    case "requiredFlags": cfg.PushToTalk.RequiredFlags = ConfigValueCoercion.AsInt(value, cfg.PushToTalk.RequiredFlags); return true;
                    case "label": cfg.PushToTalk.Label = ConfigValueCoercion.AsString(value, cfg.PushToTalk.Label); return true;
                }
                return false;
            case "speech":
                switch (sub)
                {
                    case "gain": cfg.Speech.Gain = ConfigValueCoercion.AsDouble(value, cfg.Speech.Gain); return true;
                    case "inputDevice": cfg.Speech.InputDevice = value.ValueKind == JsonValueKind.Null ? null : ConfigValueCoercion.AsString(value, cfg.Speech.InputDevice ?? ""); return true;
                    case "onDeviceRecognition": cfg.Speech.OnDeviceRecognition = ConfigValueCoercion.AsBool(value, cfg.Speech.OnDeviceRecognition); return true;
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
        writer.WritePropertyName("telemetry");
        Telemetry.WriteTo(writer);
        writer.WritePropertyName("remoteConfig");
        RemoteConfig.WriteTo(writer);
        writer.WritePropertyName("keybindings");
        Keybindings.WriteTo(writer);
        writer.WritePropertyName("pushToTalk");
        PushToTalk.WriteTo(writer);
        writer.WritePropertyName("speech");
        Speech.WriteTo(writer);
        writer.WritePropertyName("lspServers");
        LspServers.WriteTo(writer);
        writer.WritePropertyName("adapterCommands");
        AdapterCommands.WriteTo(writer);
        foreach (var kv in Extra)
        {
            writer.WritePropertyName(kv.Key);
            kv.Value.WriteTo(writer);
        }
        writer.WriteEndObject();
    }
}

internal sealed class TerminalSection
{
    public string FontFamily { get; set; } = "Menlo, monospace";
    public int FontSize { get; set; } = 11;
    public double LineHeight { get; set; } = 1.2;
    public double LetterSpacing { get; set; } = 0.0;
    public bool FontLigatures { get; set; } = false;
    public string CursorStyle { get; set; } = "block";
    public bool CursorBlink { get; set; } = true;
    public int ScrollbackLines { get; set; } = 10000;
    public int Padding { get; set; } = 8;
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
                case "fontLigatures": s.FontLigatures = ConfigValueCoercion.AsBool(prop.Value, s.FontLigatures); break;
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
        writer.WriteBoolean("fontLigatures", FontLigatures);
        writer.WriteString("cursorStyle", CursorStyle);
        writer.WriteBoolean("cursorBlink", CursorBlink);
        writer.WriteNumber("scrollbackLines", ScrollbackLines);
        writer.WriteNumber("padding", Padding);
        writer.WriteNumber("backgroundOpacity", BackgroundOpacity);
        writer.WriteEndObject();
    }
}

internal sealed class MarkdownEditorSection
{
    public string DefaultFont { get; set; } = "Default";
    public int FontSize { get; set; } = 14;
    public string TextAlign { get; set; } = "left";
    public bool BookView { get; set; } = false;
    public string BookViewWidth { get; set; } = "5.5in";
    public string BookViewMargin { get; set; } = "0.5in";
    public string DefaultViewMode { get; set; } = "rte";
    public string ImagePasteFolder { get; set; } = "media";

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
                case "imagePasteFolder": if (prop.Value.ValueKind == JsonValueKind.String) s.ImagePasteFolder = prop.Value.GetString() ?? s.ImagePasteFolder; break;
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
        writer.WriteString("imagePasteFolder", ImagePasteFolder);
        writer.WriteEndObject();
    }
}

internal sealed class UpdatesSection
{
    public bool CheckOnLaunch { get; set; } = true;
    public bool AutoInstall { get; set; } = false;
    public bool AutoUpdateAdapters { get; set; } = true;
    public string Channel { get; set; } = "stable";
    public int CheckIntervalHours { get; set; } = 24;

    public static UpdatesSection Read(JsonElement el)
    {
        var s = new UpdatesSection();
        if (el.ValueKind != JsonValueKind.Object) return s;
        foreach (var prop in el.EnumerateObject())
        {
            switch (prop.Name)
            {
                case "checkOnLaunch": s.CheckOnLaunch = ConfigValueCoercion.AsBool(prop.Value, s.CheckOnLaunch); break;
                case "autoInstall": s.AutoInstall = ConfigValueCoercion.AsBool(prop.Value, s.AutoInstall); break;
                case "autoUpdateAdapters": s.AutoUpdateAdapters = ConfigValueCoercion.AsBool(prop.Value, s.AutoUpdateAdapters); break;
                case "channel": if (prop.Value.ValueKind == JsonValueKind.String) s.Channel = prop.Value.GetString() ?? s.Channel; break;
                case "checkIntervalHours": s.CheckIntervalHours = ConfigValueCoercion.AsInt(prop.Value, s.CheckIntervalHours); break;
            }
        }
        return s;
    }

    public void WriteTo(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteBoolean("checkOnLaunch", CheckOnLaunch);
        writer.WriteBoolean("autoInstall", AutoInstall);
        writer.WriteBoolean("autoUpdateAdapters", AutoUpdateAdapters);
        writer.WriteString("channel", Channel);
        writer.WriteNumber("checkIntervalHours", CheckIntervalHours);
        writer.WriteEndObject();
    }
}

internal sealed class DiagnosticsSection
{
    public bool Enabled { get; set; } = false;
    public bool CaptureLongTasks { get; set; } = true;
    public bool CaptureRenderStats { get; set; } = true;
    public bool CaptureIpcTimings { get; set; } = true;
    public bool CaptureTerminalStats { get; set; } = true;
    public bool CaptureMemoryStats { get; set; } = true;
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
                case "captureLongTasks": s.CaptureLongTasks = ConfigValueCoercion.AsBool(prop.Value, s.CaptureLongTasks); break;
                case "captureRenderStats": s.CaptureRenderStats = ConfigValueCoercion.AsBool(prop.Value, s.CaptureRenderStats); break;
                case "captureIpcTimings": s.CaptureIpcTimings = ConfigValueCoercion.AsBool(prop.Value, s.CaptureIpcTimings); break;
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
        writer.WriteBoolean("captureLongTasks", CaptureLongTasks);
        writer.WriteBoolean("captureRenderStats", CaptureRenderStats);
        writer.WriteBoolean("captureIpcTimings", CaptureIpcTimings);
        writer.WriteBoolean("captureTerminalStats", CaptureTerminalStats);
        writer.WriteBoolean("captureMemoryStats", CaptureMemoryStats);
        writer.WriteNumber("flushIntervalMs", FlushIntervalMs);
        writer.WriteEndObject();
    }
}

internal sealed class WorktreeSection
{
    public string DefaultLocationPattern { get; set; } = "../{repo}-worktrees/{branch}";
    public List<string> PostCreateCommands { get; set; } = new();

    public static WorktreeSection Read(JsonElement el)
    {
        var s = new WorktreeSection();
        if (el.ValueKind != JsonValueKind.Object) return s;
        foreach (var prop in el.EnumerateObject())
        {
            switch (prop.Name)
            {
                case "defaultLocationPattern": if (prop.Value.ValueKind == JsonValueKind.String) s.DefaultLocationPattern = prop.Value.GetString() ?? s.DefaultLocationPattern; break;
                case "postCreateCommands":
                    if (prop.Value.ValueKind == JsonValueKind.Array)
                    {
                        s.PostCreateCommands = new List<string>();
                        foreach (var item in prop.Value.EnumerateArray())
                            if (item.ValueKind == JsonValueKind.String)
                                s.PostCreateCommands.Add(item.GetString() ?? "");
                    }
                    break;
            }
        }
        return s;
    }

    public void WriteTo(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteString("defaultLocationPattern", DefaultLocationPattern);
        writer.WriteStartArray("postCreateCommands");
        foreach (var cmd in PostCreateCommands)
            writer.WriteStringValue(cmd);
        writer.WriteEndArray();
        writer.WriteEndObject();
    }
}

internal sealed class TelemetrySection
{
    public bool AnalyticsOptIn { get; set; } = false;
    public bool CoreTelemetryDisclosed { get; set; } = false;
    public bool Enabled { get; set; } = true;

    public static TelemetrySection Read(JsonElement el)
    {
        var s = new TelemetrySection();
        if (el.ValueKind != JsonValueKind.Object) return s;
        foreach (var prop in el.EnumerateObject())
        {
            switch (prop.Name)
            {
                case "analyticsOptIn": s.AnalyticsOptIn = ConfigValueCoercion.AsBool(prop.Value, s.AnalyticsOptIn); break;
                case "coreTelemetryDisclosed": s.CoreTelemetryDisclosed = ConfigValueCoercion.AsBool(prop.Value, s.CoreTelemetryDisclosed); break;
                case "enabled": s.Enabled = ConfigValueCoercion.AsBool(prop.Value, s.Enabled); break;
            }
        }
        return s;
    }

    public void WriteTo(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteBoolean("analyticsOptIn", AnalyticsOptIn);
        writer.WriteBoolean("coreTelemetryDisclosed", CoreTelemetryDisclosed);
        writer.WriteBoolean("enabled", Enabled);
        writer.WriteEndObject();
    }
}

internal sealed class RemoteConfigSection
{
    public List<string> DismissedBannerIds { get; set; } = new();

    public static RemoteConfigSection Read(JsonElement el)
    {
        var s = new RemoteConfigSection();
        if (el.ValueKind != JsonValueKind.Object) return s;
        foreach (var prop in el.EnumerateObject())
        {
            if (prop.Name == "dismissedBannerIds" && prop.Value.ValueKind == JsonValueKind.Array)
            {
                s.DismissedBannerIds = new List<string>();
                foreach (var item in prop.Value.EnumerateArray())
                    if (item.ValueKind == JsonValueKind.String)
                        s.DismissedBannerIds.Add(item.GetString() ?? "");
            }
        }
        return s;
    }

    public void WriteTo(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteStartArray("dismissedBannerIds");
        foreach (var id in DismissedBannerIds)
            writer.WriteStringValue(id);
        writer.WriteEndArray();
        writer.WriteEndObject();
    }
}

internal sealed class KeybindingsSection
{
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

internal sealed class PushToTalkSection
{
    public bool Enabled { get; set; } = true;
    public int KeyCode { get; set; } = 61;
    public bool IsModifier { get; set; } = true;
    public int RequiredFlags { get; set; } = 0;
    public string Label { get; set; } = "Right Option";

    public static PushToTalkSection Read(JsonElement el)
    {
        var s = new PushToTalkSection();
        if (el.ValueKind != JsonValueKind.Object) return s;
        foreach (var prop in el.EnumerateObject())
        {
            switch (prop.Name)
            {
                case "enabled": s.Enabled = ConfigValueCoercion.AsBool(prop.Value, s.Enabled); break;
                case "keyCode": s.KeyCode = ConfigValueCoercion.AsInt(prop.Value, s.KeyCode); break;
                case "isModifier": s.IsModifier = ConfigValueCoercion.AsBool(prop.Value, s.IsModifier); break;
                case "requiredFlags": s.RequiredFlags = ConfigValueCoercion.AsInt(prop.Value, s.RequiredFlags); break;
                case "label": if (prop.Value.ValueKind == JsonValueKind.String) s.Label = prop.Value.GetString() ?? s.Label; break;
            }
        }
        return s;
    }

    public void WriteTo(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteBoolean("enabled", Enabled);
        writer.WriteNumber("keyCode", KeyCode);
        writer.WriteBoolean("isModifier", IsModifier);
        writer.WriteNumber("requiredFlags", RequiredFlags);
        writer.WriteString("label", Label);
        writer.WriteEndObject();
    }
}

internal sealed class SpeechSection
{
    public double Gain { get; set; } = 1.0;
    public string? InputDevice { get; set; } = null;
    public bool OnDeviceRecognition { get; set; } = true;

    public static SpeechSection Read(JsonElement el)
    {
        var s = new SpeechSection();
        if (el.ValueKind != JsonValueKind.Object) return s;
        foreach (var prop in el.EnumerateObject())
        {
            switch (prop.Name)
            {
                case "gain": s.Gain = ConfigValueCoercion.AsDouble(prop.Value, s.Gain); break;
                case "inputDevice": s.InputDevice = prop.Value.ValueKind == JsonValueKind.Null ? null : (prop.Value.ValueKind == JsonValueKind.String ? prop.Value.GetString() : s.InputDevice); break;
                case "onDeviceRecognition": s.OnDeviceRecognition = ConfigValueCoercion.AsBool(prop.Value, s.OnDeviceRecognition); break;
            }
        }
        return s;
    }

    public void WriteTo(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteNumber("gain", Gain);
        if (InputDevice is null)
            writer.WriteNull("inputDevice");
        else
            writer.WriteString("inputDevice", InputDevice);
        writer.WriteBoolean("onDeviceRecognition", OnDeviceRecognition);
        writer.WriteEndObject();
    }
}

internal sealed class LspServersSection
{
    public Dictionary<string, JsonElement> Servers { get; set; } = new();

    public static LspServersSection Read(JsonElement el)
    {
        var s = new LspServersSection();
        if (el.ValueKind != JsonValueKind.Object) return s;
        foreach (var prop in el.EnumerateObject())
            s.Servers[prop.Name] = prop.Value.Clone();
        return s;
    }

    public void WriteTo(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        foreach (var kv in Servers)
        {
            writer.WritePropertyName(kv.Key);
            kv.Value.WriteTo(writer);
        }
        writer.WriteEndObject();
    }
}

internal sealed class AdapterCommandsSection
{
    public Dictionary<string, JsonElement> Commands { get; set; } = new();

    public static AdapterCommandsSection Read(JsonElement el)
    {
        var s = new AdapterCommandsSection();
        if (el.ValueKind != JsonValueKind.Object) return s;
        foreach (var prop in el.EnumerateObject())
            s.Commands[prop.Name] = prop.Value.Clone();
        return s;
    }

    public void WriteTo(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        foreach (var kv in Commands)
        {
            writer.WritePropertyName(kv.Key);
            kv.Value.WriteTo(writer);
        }
        writer.WriteEndObject();
    }
}
