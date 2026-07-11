using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Cove.Persistence;
using Microsoft.Extensions.Logging;

namespace Cove.Engine.Config;

public sealed class ConfigService : System.IDisposable
{
    private readonly string _path;
    private readonly ILogger _logger;
    private readonly object _lock = new();
    private CoveConfig _config = new();
    private FileSystemWatcher? _watcher;
    private bool _writable = true;
    private bool _disposed;

    public event Action<string>? SettingsChanged;

    public ConfigService(string dataDir, ILogger logger)
    {
        _path = Path.Combine(dataDir, "config.json");
        _logger = logger;
        Load();
    }

    public bool IsWritable()
    {
        lock (_lock)
            return _writable;
    }

    public string GetTheme() { lock (_lock) return _config.Theme; }
    public string GetTerminalFontFamily() { lock (_lock) return _config.Terminal.FontFamily; }
    public int GetTerminalFontSize() { lock (_lock) return _config.Terminal.FontSize; }
    public bool GetTerminalFontLigatures() { lock (_lock) return _config.Terminal.FontLigatures; }
    public string GetUpdatesChannel() { lock (_lock) return _config.Updates.Channel; }
    public bool GetTelemetryEnabled() { lock (_lock) return _config.Telemetry.Enabled; }
    public bool GetDiagnosticsEnabled() { lock (_lock) return _config.Diagnostics.Enabled; }
    public string GetWorktreeDefaultLocationPattern() { lock (_lock) return _config.Worktree.DefaultLocationPattern; }
    public string GetMarkdownEditorDefaultFont() { lock (_lock) return _config.MarkdownEditor.DefaultFont; }
    public IReadOnlyDictionary<string, JsonElement> GetKeybindings() { lock (_lock) return _config.Keybindings.Bindings; }
    public IReadOnlyList<string> GetRemoteConfigDismissedBannerIds() { lock (_lock) return _config.RemoteConfig.DismissedBannerIds; }
    public bool GetPushToTalkEnabled() { lock (_lock) return _config.PushToTalk.Enabled; }
    public double GetSpeechGain() { lock (_lock) return _config.Speech.Gain; }
    public IReadOnlyDictionary<string, JsonElement> GetLspServers() { lock (_lock) return _config.LspServers.Servers; }
    public IReadOnlyList<LspConfigServerEntry> GetLspServerEntries() { lock (_lock) return _config.Lsp.Servers; }
    public IReadOnlyDictionary<string, JsonElement> GetAdapterCommands() { lock (_lock) return _config.AdapterCommands.Commands; }
    public bool GetSessionRestoreOnLaunch() { lock (_lock) return _config.Session.RestoreOnLaunch; }

    public void SetTheme(string value) { if (!TrySet(() => _config.Theme = value, "theme")) return; }
    public void SetTerminalFontSize(int value) { if (!TrySet(() => _config.Terminal.FontSize = value, "terminal.fontSize")) return; }
    public void SetTerminalFontLigatures(bool value) { if (!TrySet(() => _config.Terminal.FontLigatures = value, "terminal.fontLigatures")) return; }

    private bool TrySet(System.Action mutate, string key)
    {
        lock (_lock)
        {
            if (!_writable)
            {
                _logger.ConfigWriteBlocked(_path);
                return false;
            }
            mutate();
            Save();
        }
        SettingsChanged?.Invoke(key);
        return true;
    }

    public string? Get(string key)
    {
        lock (_lock)
        {
            var (found, value) = GetTyped(key);
            if (!found) return null;
            return value switch
            {
                string s => s,
                int i => i.ToString(),
                double d => d.ToString(System.Globalization.CultureInfo.InvariantCulture),
                bool b => b ? "true" : "false",
                JsonElement je => je.ValueKind == JsonValueKind.String ? je.GetString() : je.GetRawText(),
                _ => value?.ToString(),
            };
        }
    }

    private (bool found, object? value) GetTyped(string key)
    {
        return key switch
        {
            "theme" => (true, _config.Theme),
            "appearance.uiScale" => (true, _config.Appearance.UiScale),
            "appearance.layoutGap" => (true, _config.Appearance.LayoutGap),
            "appearance.iconSet" => (true, _config.Appearance.IconSet),
            "appearance.wallpaper" => (true, _config.Appearance.Wallpaper),
            "appearance.accent" => (true, _config.Appearance.Accent),
            "appearance.paneLight" => (true, _config.Appearance.PaneLight),
            "terminal.fontFamily" => (true, _config.Terminal.FontFamily),
            "terminal.fontSize" => (true, _config.Terminal.FontSize),
            "terminal.lineHeight" => (true, _config.Terminal.LineHeight),
            "terminal.letterSpacing" => (true, _config.Terminal.LetterSpacing),
            "terminal.fontLigatures" => (true, _config.Terminal.FontLigatures),
            "terminal.cursorStyle" => (true, _config.Terminal.CursorStyle),
            "terminal.cursorBlink" => (true, _config.Terminal.CursorBlink),
            "terminal.scrollbackLines" => (true, _config.Terminal.ScrollbackLines),
            "terminal.padding" => (true, _config.Terminal.Padding),
            "terminal.backgroundOpacity" => (true, _config.Terminal.BackgroundOpacity),
            "markdown_editor.defaultFont" => (true, _config.MarkdownEditor.DefaultFont),
            "markdown_editor.fontSize" => (true, _config.MarkdownEditor.FontSize),
            "markdown_editor.textAlign" => (true, _config.MarkdownEditor.TextAlign),
            "markdown_editor.bookView" => (true, _config.MarkdownEditor.BookView),
            "markdown_editor.bookViewWidth" => (true, _config.MarkdownEditor.BookViewWidth),
            "markdown_editor.bookViewMargin" => (true, _config.MarkdownEditor.BookViewMargin),
            "markdown_editor.defaultViewMode" => (true, _config.MarkdownEditor.DefaultViewMode),
            "markdown_editor.imagePasteFolder" => (true, _config.MarkdownEditor.ImagePasteFolder),
            "updates.checkOnLaunch" => (true, _config.Updates.CheckOnLaunch),
            "updates.autoInstall" => (true, _config.Updates.AutoInstall),
            "updates.autoUpdateAdapters" => (true, _config.Updates.AutoUpdateAdapters),
            "updates.channel" => (true, _config.Updates.Channel),
            "updates.checkIntervalHours" => (true, _config.Updates.CheckIntervalHours),
            "diagnostics.enabled" => (true, _config.Diagnostics.Enabled),
            "telemetry.enabled" => (true, _config.Telemetry.Enabled),
            "telemetry.analyticsOptIn" => (true, _config.Telemetry.AnalyticsOptIn),
            "pushToTalk.enabled" => (true, _config.PushToTalk.Enabled),
            "pushToTalk.isModifier" => (true, _config.PushToTalk.IsModifier),
            "pushToTalk.keyCode" => (true, _config.PushToTalk.KeyCode),
            "pushToTalk.requiredFlags" => (true, _config.PushToTalk.RequiredFlags),
            "pushToTalk.label" => (true, _config.PushToTalk.Label),
            "speech.gain" => (true, _config.Speech.Gain),
            "speech.inputDevice" => (true, _config.Speech.InputDevice ?? ""),
            "speech.onDeviceRecognition" => (true, _config.Speech.OnDeviceRecognition),
            "diagnostics.captureLongTasks" => (true, _config.Diagnostics.CaptureLongTasks),
            "diagnostics.captureRenderStats" => (true, _config.Diagnostics.CaptureRenderStats),
            "diagnostics.captureIpcTimings" => (true, _config.Diagnostics.CaptureIpcTimings),
            "diagnostics.captureTerminalStats" => (true, _config.Diagnostics.CaptureTerminalStats),
            "diagnostics.captureMemoryStats" => (true, _config.Diagnostics.CaptureMemoryStats),
            "diagnostics.flushIntervalMs" => (true, _config.Diagnostics.FlushIntervalMs),
            "worktree.defaultLocationPattern" => (true, _config.Worktree.DefaultLocationPattern),
            "telemetry.coreTelemetryDisclosed" => (true, _config.Telemetry.CoreTelemetryDisclosed),
            "session.restoreOnLaunch" => (true, _config.Session.RestoreOnLaunch),
            _ => _config.Extra.TryGetValue(key, out var extra) ? (true, (object?)extra) : (false, null),
        };
    }

    public void Set(string key, string value)
    {
        if (!IsWritable())
        {
            _logger.ConfigWriteBlocked(_path);
            return;
        }
        lock (_lock)
        {
            switch (key)
            {
                case "theme": _config.Theme = value; break;
                case "appearance.uiScale": _config.Appearance.UiScale = AutoDetectDouble(value); break;
                case "appearance.layoutGap": _config.Appearance.LayoutGap = AutoDetectInt(value); break;
                case "appearance.iconSet": _config.Appearance.IconSet = value; break;
                case "appearance.wallpaper": _config.Appearance.Wallpaper = value; break;
                case "appearance.accent": _config.Appearance.Accent = value; break;
                case "appearance.paneLight": _config.Appearance.PaneLight = AutoDetectBool(value); break;
                case "terminal.fontFamily": _config.Terminal.FontFamily = value; break;
                case "terminal.fontSize": _config.Terminal.FontSize = AutoDetectInt(value); break;
                case "terminal.lineHeight": _config.Terminal.LineHeight = AutoDetectDouble(value); break;
                case "terminal.letterSpacing": _config.Terminal.LetterSpacing = AutoDetectDouble(value); break;
                case "terminal.fontLigatures": _config.Terminal.FontLigatures = AutoDetectBool(value); break;
                case "terminal.cursorStyle": _config.Terminal.CursorStyle = value; break;
                case "terminal.cursorBlink": _config.Terminal.CursorBlink = AutoDetectBool(value); break;
                case "terminal.scrollbackLines": _config.Terminal.ScrollbackLines = AutoDetectInt(value); break;
                case "terminal.padding": _config.Terminal.Padding = AutoDetectInt(value); break;
                case "terminal.backgroundOpacity": _config.Terminal.BackgroundOpacity = AutoDetectDouble(value); break;
                case "markdown_editor.defaultFont": _config.MarkdownEditor.DefaultFont = value; break;
                case "markdown_editor.fontSize": _config.MarkdownEditor.FontSize = AutoDetectInt(value); break;
                case "markdown_editor.textAlign": _config.MarkdownEditor.TextAlign = value; break;
                case "markdown_editor.bookView": _config.MarkdownEditor.BookView = AutoDetectBool(value); break;
                case "markdown_editor.bookViewWidth": _config.MarkdownEditor.BookViewWidth = value; break;
                case "markdown_editor.bookViewMargin": _config.MarkdownEditor.BookViewMargin = value; break;
                case "markdown_editor.defaultViewMode": _config.MarkdownEditor.DefaultViewMode = value; break;
                case "markdown_editor.imagePasteFolder": _config.MarkdownEditor.ImagePasteFolder = value; break;
                case "updates.checkOnLaunch": _config.Updates.CheckOnLaunch = AutoDetectBool(value); break;
                case "updates.autoInstall": _config.Updates.AutoInstall = AutoDetectBool(value); break;
                case "updates.autoUpdateAdapters": _config.Updates.AutoUpdateAdapters = AutoDetectBool(value); break;
                case "updates.channel": _config.Updates.Channel = value; break;
                case "updates.checkIntervalHours": _config.Updates.CheckIntervalHours = AutoDetectInt(value); break;
                case "diagnostics.enabled": _config.Diagnostics.Enabled = AutoDetectBool(value); break;
                case "telemetry.enabled": _config.Telemetry.Enabled = AutoDetectBool(value); break;
                case "telemetry.analyticsOptIn": _config.Telemetry.AnalyticsOptIn = AutoDetectBool(value); break;
                case "pushToTalk.enabled": _config.PushToTalk.Enabled = AutoDetectBool(value); break;
                case "pushToTalk.isModifier": _config.PushToTalk.IsModifier = AutoDetectBool(value); break;
                case "pushToTalk.keyCode": _config.PushToTalk.KeyCode = AutoDetectInt(value); break;
                case "pushToTalk.requiredFlags": _config.PushToTalk.RequiredFlags = AutoDetectInt(value); break;
                case "pushToTalk.label": _config.PushToTalk.Label = value; break;
                case "speech.gain": _config.Speech.Gain = AutoDetectDouble(value); break;
                case "speech.inputDevice": _config.Speech.InputDevice = string.IsNullOrEmpty(value) ? null : value; break;
                case "speech.onDeviceRecognition": _config.Speech.OnDeviceRecognition = AutoDetectBool(value); break;
                case "diagnostics.captureLongTasks": _config.Diagnostics.CaptureLongTasks = AutoDetectBool(value); break;
                case "diagnostics.captureRenderStats": _config.Diagnostics.CaptureRenderStats = AutoDetectBool(value); break;
                case "diagnostics.captureIpcTimings": _config.Diagnostics.CaptureIpcTimings = AutoDetectBool(value); break;
                case "diagnostics.captureTerminalStats": _config.Diagnostics.CaptureTerminalStats = AutoDetectBool(value); break;
                case "diagnostics.captureMemoryStats": _config.Diagnostics.CaptureMemoryStats = AutoDetectBool(value); break;
                case "diagnostics.flushIntervalMs": _config.Diagnostics.FlushIntervalMs = AutoDetectInt(value); break;
                case "worktree.defaultLocationPattern": _config.Worktree.DefaultLocationPattern = value; break;
                case "telemetry.coreTelemetryDisclosed": _config.Telemetry.CoreTelemetryDisclosed = AutoDetectBool(value); break;
                case "session.restoreOnLaunch": _config.Session.RestoreOnLaunch = AutoDetectBool(value); break;
                default:
                    _config.Extra[key] = AutoDetectJson(value);
                    break;
            }
            Save();
        }
        SettingsChanged?.Invoke(key);
    }

    public string? GetKeybindingsJson()
    {
        lock (_lock)
        {
            if (_config.Keybindings.Bindings.Count == 0) return null;
            var sb = new System.Text.StringBuilder();
            sb.Append('{');
            var first = true;
            foreach (var kv in _config.Keybindings.Bindings)
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append('"').Append(kv.Key).Append("\":").Append(kv.Value.GetRawText());
            }
            sb.Append('}');
            return sb.ToString();
        }
    }

    public void SetKeybindings(string json)
    {
        if (!IsWritable())
        {
            _logger.ConfigWriteBlocked(_path);
            return;
        }
        lock (_lock)
        {
            _config.Keybindings.Bindings.Clear();
            try
            {
                using var doc = JsonDocument.Parse(json);
                foreach (var prop in doc.RootElement.EnumerateObject())
                    _config.Keybindings.Bindings[prop.Name] = prop.Value.Clone();
            }
            catch (JsonException ex)
            {
                _logger.ConfigParseFailed(_path, ex.Message);
                return;
            }
            Save();
        }
        SettingsChanged?.Invoke("keybindings");
    }

    private static int AutoDetectInt(string value) => int.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var i) ? i : 0;
    private static double AutoDetectDouble(string value) => double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : 0.0;
    private static bool AutoDetectBool(string value) => bool.TryParse(value, out var b) && b;
    private static JsonElement AutoDetectJson(string value)
    {
        var buf = new System.IO.MemoryStream();
        using (var writer = new Utf8JsonWriter(buf))
        {
            if (value == "true") writer.WriteBooleanValue(true);
            else if (value == "false") writer.WriteBooleanValue(false);
            else if (int.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var i)) writer.WriteNumberValue(i);
            else if (double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d)) writer.WriteNumberValue(d);
            else writer.WriteStringValue(value);
            writer.Flush();
        }
        var json = System.Text.Encoding.UTF8.GetString(buf.ToArray());
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    public void StartWatching()
    {
        if (_watcher is not null) return;
        var dir = Path.GetDirectoryName(_path);
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;
        _watcher = new FileSystemWatcher(dir)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
            EnableRaisingEvents = true,
        };
        _watcher.Changed += OnFileChanged;
        _watcher.Created += OnFileChanged;
        _watcher.Renamed += OnFileChanged;
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        if (!string.Equals(Path.GetFileName(e.FullPath), Path.GetFileName(_path), StringComparison.Ordinal))
            return;
        var changed = ReloadAndCollectChanges();
        foreach (var key in changed)
            SettingsChanged?.Invoke(key);
    }

    public void Reload()
    {
        var changed = ReloadAndCollectChanges();
        foreach (var key in changed)
            SettingsChanged?.Invoke(key);
    }

    private List<string> ReloadAndCollectChanges()
    {
        lock (_lock)
        {
            var before = Flatten();
            Load();
            var after = Flatten();
            var changed = new List<string>();
            foreach (var kv in after)
                if (!before.TryGetValue(kv.Key, out var prev) || prev != kv.Value)
                    changed.Add(kv.Key);
            foreach (var key in before.Keys)
                if (!after.ContainsKey(key))
                    changed.Add(key);
            return changed;
        }
    }

    private Dictionary<string, string> Flatten()
    {
        var d = new Dictionary<string, string>();
        d["theme"] = _config.Theme;
        d["terminal.fontFamily"] = _config.Terminal.FontFamily;
        d["terminal.fontSize"] = _config.Terminal.FontSize.ToString();
        d["terminal.lineHeight"] = _config.Terminal.LineHeight.ToString(System.Globalization.CultureInfo.InvariantCulture);
        d["terminal.letterSpacing"] = _config.Terminal.LetterSpacing.ToString(System.Globalization.CultureInfo.InvariantCulture);
        d["terminal.fontLigatures"] = _config.Terminal.FontLigatures.ToString();
        d["terminal.cursorStyle"] = _config.Terminal.CursorStyle;
        d["terminal.cursorBlink"] = _config.Terminal.CursorBlink.ToString();
        d["terminal.scrollbackLines"] = _config.Terminal.ScrollbackLines.ToString();
        d["terminal.padding"] = _config.Terminal.Padding.ToString();
        d["terminal.backgroundOpacity"] = _config.Terminal.BackgroundOpacity.ToString(System.Globalization.CultureInfo.InvariantCulture);
        d["markdown_editor.defaultFont"] = _config.MarkdownEditor.DefaultFont;
        d["markdown_editor.fontSize"] = _config.MarkdownEditor.FontSize.ToString();
        d["markdown_editor.textAlign"] = _config.MarkdownEditor.TextAlign;
        d["markdown_editor.bookView"] = _config.MarkdownEditor.BookView.ToString();
        d["markdown_editor.bookViewWidth"] = _config.MarkdownEditor.BookViewWidth;
        d["markdown_editor.bookViewMargin"] = _config.MarkdownEditor.BookViewMargin;
        d["markdown_editor.defaultViewMode"] = _config.MarkdownEditor.DefaultViewMode;
        d["markdown_editor.imagePasteFolder"] = _config.MarkdownEditor.ImagePasteFolder;
        d["updates.checkOnLaunch"] = _config.Updates.CheckOnLaunch.ToString();
        d["updates.autoInstall"] = _config.Updates.AutoInstall.ToString();
        d["updates.autoUpdateAdapters"] = _config.Updates.AutoUpdateAdapters.ToString();
        d["updates.channel"] = _config.Updates.Channel;
        d["updates.checkIntervalHours"] = _config.Updates.CheckIntervalHours.ToString();
        d["diagnostics.enabled"] = _config.Diagnostics.Enabled.ToString();
        d["diagnostics.captureLongTasks"] = _config.Diagnostics.CaptureLongTasks.ToString();
        d["diagnostics.captureRenderStats"] = _config.Diagnostics.CaptureRenderStats.ToString();
        d["diagnostics.captureIpcTimings"] = _config.Diagnostics.CaptureIpcTimings.ToString();
        d["diagnostics.captureTerminalStats"] = _config.Diagnostics.CaptureTerminalStats.ToString();
        d["diagnostics.captureMemoryStats"] = _config.Diagnostics.CaptureMemoryStats.ToString();
        d["diagnostics.flushIntervalMs"] = _config.Diagnostics.FlushIntervalMs.ToString();
        d["worktree.defaultLocationPattern"] = _config.Worktree.DefaultLocationPattern;
        d["worktree.postCreateCommands"] = string.Join(",", _config.Worktree.PostCreateCommands);
        d["telemetry.analyticsOptIn"] = _config.Telemetry.AnalyticsOptIn.ToString();
        d["telemetry.coreTelemetryDisclosed"] = _config.Telemetry.CoreTelemetryDisclosed.ToString();
        d["telemetry.enabled"] = _config.Telemetry.Enabled.ToString();
        d["remoteConfig.dismissedBannerIds"] = string.Join(",", _config.RemoteConfig.DismissedBannerIds);
        d["pushToTalk.enabled"] = _config.PushToTalk.Enabled.ToString();
        d["pushToTalk.keyCode"] = _config.PushToTalk.KeyCode.ToString();
        d["pushToTalk.isModifier"] = _config.PushToTalk.IsModifier.ToString();
        d["pushToTalk.requiredFlags"] = _config.PushToTalk.RequiredFlags.ToString();
        d["pushToTalk.label"] = _config.PushToTalk.Label;
        d["speech.gain"] = _config.Speech.Gain.ToString(System.Globalization.CultureInfo.InvariantCulture);
        d["speech.inputDevice"] = _config.Speech.InputDevice ?? "";
        d["speech.onDeviceRecognition"] = _config.Speech.OnDeviceRecognition.ToString();
        d["session.restoreOnLaunch"] = _config.Session.RestoreOnLaunch.ToString();
        foreach (var kv in _config.Keybindings.Bindings)
            d["keybindings." + kv.Key] = kv.Value.GetRawText();
        foreach (var kv in _config.LspServers.Servers)
            d["lspServers." + kv.Key] = kv.Value.GetRawText();
        for (var i = 0; i < _config.Lsp.Servers.Count; i++)
        {
            var entry = _config.Lsp.Servers[i];
            d["lsp.servers." + i] = string.Join(",", entry.Languages) + " " + entry.Command + " " + string.Join(",", entry.Args);
        }
        foreach (var kv in _config.AdapterCommands.Commands)
            d["adapterCommands." + kv.Key] = kv.Value.GetRawText();
        foreach (var kv in _config.Extra)
            d["extra." + kv.Key] = kv.Value.GetRawText();
        return d;
    }

    private void Load()
    {
        if (!File.Exists(_path))
        {
            _config = new CoveConfig();
            _writable = true;
            return;
        }
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                var json = File.ReadAllText(_path);
                using var doc = JsonDocument.Parse(json);
                _config = CoveConfig.ReadFrom(doc);
                _writable = true;
                return;
            }
            catch (JsonException ex)
            {
                _logger.ConfigParseFailed(_path, ex.Message);
                _config = new CoveConfig();
                _writable = false;
                return;
            }
            catch (System.InvalidOperationException ex)
            {
                _logger.ConfigParseFailed(_path, ex.Message);
                _config = new CoveConfig();
                _writable = false;
                return;
            }
            catch (IOException)
            {
                System.Threading.Thread.Sleep(20);
            }
        }
        _logger.ConfigReadFailed(_path);
        _writable = false;
    }

    private void Save()
    {
        if (!_writable)
        {
            _logger.ConfigWriteBlocked(_path);
            return;
        }
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        using var buf = new System.IO.MemoryStream();
        var writerOptions = new JsonWriterOptions { Indented = true };
        var writer = new Utf8JsonWriter(buf, writerOptions);
        _config.WriteTo(writer);
        writer.Flush();
        AtomicJsonStore.WriteRawText(_path, System.Text.Encoding.UTF8.GetString(buf.ToArray()));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_watcher is not null)
        {
            _watcher.Changed -= OnFileChanged;
            _watcher.Created -= OnFileChanged;
            _watcher.Renamed -= OnFileChanged;
            _watcher.Dispose();
        }
    }
}
