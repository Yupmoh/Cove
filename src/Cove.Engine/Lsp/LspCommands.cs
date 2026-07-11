using System.Text.Json;
using System.Text.Json.Serialization;
using Cove.Protocol;

namespace Cove.Engine.Lsp;

public static class LspCommands
{
    [CoveCommand("cove://commands/lsp.diagnostics")]
    public static async Task<ControlResponse> Diagnostics(EngineDispatchContext ctx)
    {
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(LspCommandsJsonContext.Default.LspDocumentParams) is not { } p)
            return ctx.Fail("invalid_params", "lsp diagnostics params required");
        if (ctx.LspService is not { } lsp)
            return ctx.Fail("not_ready", "lsp service not available");

        var unavailable = new LspDiagnosticsResult(false, null, []);
        if (LspService.DetectLanguage(p.FilePath) is not { } language)
        {
            lsp.WarnUnknownFileLanguage(p.FilePath);
            return ctx.Ok(unavailable, LspCommandsJsonContext.Default.LspDiagnosticsResult);
        }
        var server = await lsp.GetOrStartServerAsync(language, p.RootDir);
        if (server is null)
            return ctx.Ok(unavailable with { Language = language }, LspCommandsJsonContext.Default.LspDiagnosticsResult);

        try
        {
            var uri = "file://" + p.FilePath;
            var pushBaseline = lsp.GetPublishedDiagnosticsSequence(server, uri);
            await SyncDocumentAsync(lsp, server, p.FilePath, language, p.Content);
            IReadOnlyList<LspDiagnosticDto>? diagnostics = null;
            try
            {
                using var pullCts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                var diagParams = ParseJson($$$"""{"textDocument":{"uri":{{{EncodeUri(p.FilePath)}}}}}""");
                var result = await server.SendRequestAsync("textDocument/diagnostic", diagParams, pullCts.Token);
                if (result is { } r && r.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
                    diagnostics = MapDiagnosticArray(items);
            }
            catch (LspException ex)
            {
                lsp.WarnRequestFailed("textDocument/diagnostic (pull, falling back to push)", p.FilePath, ex);
            }
            catch (OperationCanceledException ex)
            {
                lsp.WarnRequestFailed("textDocument/diagnostic (pull timeout, falling back to push)", p.FilePath, ex);
            }
            if (diagnostics is null)
            {
                var pushed = await lsp.WaitForPublishedDiagnosticsAsync(server, uri, pushBaseline, TimeSpan.FromSeconds(20));
                diagnostics = pushed is { } arr ? MapDiagnosticArray(arr) : [];
            }
            return ctx.Ok(new LspDiagnosticsResult(true, language, diagnostics), LspCommandsJsonContext.Default.LspDiagnosticsResult);
        }
        catch (Exception ex)
        {
            lsp.WarnRequestFailed("textDocument/diagnostic", p.FilePath, ex);
            return ctx.Ok(unavailable with { Language = language }, LspCommandsJsonContext.Default.LspDiagnosticsResult);
        }
    }

    [CoveCommand("cove://commands/lsp.hover")]
    public static async Task<ControlResponse> Hover(EngineDispatchContext ctx)
    {
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(LspCommandsJsonContext.Default.LspPositionParams) is not { } p)
            return ctx.Fail("invalid_params", "lsp hover params required");
        if (ctx.LspService is not { } lsp)
            return ctx.Fail("not_ready", "lsp service not available");

        var unavailable = new LspHoverResult(false, null);
        if (LspService.DetectLanguage(p.FilePath) is not { } language)
        {
            lsp.WarnUnknownFileLanguage(p.FilePath);
            return ctx.Ok(unavailable, LspCommandsJsonContext.Default.LspHoverResult);
        }
        var server = await lsp.GetOrStartServerAsync(language, p.RootDir);
        if (server is null)
            return ctx.Ok(unavailable, LspCommandsJsonContext.Default.LspHoverResult);

        try
        {
            await SyncDocumentAsync(lsp, server, p.FilePath, language, p.Content);
            using var requestCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var hoverParams = ParseJson($$$"""{"textDocument":{"uri":{{{EncodeUri(p.FilePath)}}}},"position":{"line":{{{p.Line}}},"character":{{{p.Col}}}}}""");
            var result = await server.SendRequestAsync("textDocument/hover", hoverParams, requestCts.Token);
            return ctx.Ok(new LspHoverResult(true, ExtractHoverContents(result)), LspCommandsJsonContext.Default.LspHoverResult);
        }
        catch (Exception ex)
        {
            lsp.WarnRequestFailed("textDocument/hover", p.FilePath, ex);
            return ctx.Ok(unavailable, LspCommandsJsonContext.Default.LspHoverResult);
        }
    }

    [CoveCommand("cove://commands/lsp.definition")]
    public static async Task<ControlResponse> Definition(EngineDispatchContext ctx)
    {
        if (ctx.Request.Params is not JsonElement el || el.Deserialize(LspCommandsJsonContext.Default.LspPositionParams) is not { } p)
            return ctx.Fail("invalid_params", "lsp definition params required");
        if (ctx.LspService is not { } lsp)
            return ctx.Fail("not_ready", "lsp service not available");

        var unavailable = new LspDefinitionResult(false, []);
        if (LspService.DetectLanguage(p.FilePath) is not { } language)
        {
            lsp.WarnUnknownFileLanguage(p.FilePath);
            return ctx.Ok(unavailable, LspCommandsJsonContext.Default.LspDefinitionResult);
        }
        var server = await lsp.GetOrStartServerAsync(language, p.RootDir);
        if (server is null)
            return ctx.Ok(unavailable, LspCommandsJsonContext.Default.LspDefinitionResult);

        try
        {
            await SyncDocumentAsync(lsp, server, p.FilePath, language, p.Content);
            using var requestCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var defParams = ParseJson($$$"""{"textDocument":{"uri":{{{EncodeUri(p.FilePath)}}}},"position":{"line":{{{p.Line}}},"character":{{{p.Col}}}}}""");
            var result = await server.SendRequestAsync("textDocument/definition", defParams, requestCts.Token);
            return ctx.Ok(new LspDefinitionResult(true, MapLocations(result)), LspCommandsJsonContext.Default.LspDefinitionResult);
        }
        catch (Exception ex)
        {
            lsp.WarnRequestFailed("textDocument/definition", p.FilePath, ex);
            return ctx.Ok(unavailable, LspCommandsJsonContext.Default.LspDefinitionResult);
        }
    }

    [CoveCommand("cove://commands/lsp.status")]
    public static Task<ControlResponse> Status(EngineDispatchContext ctx)
    {
        if (ctx.LspService is not { } lsp)
            return Task.FromResult(ctx.Fail("not_ready", "lsp service not available"));
        var servers = lsp.GetStatuses()
            .Select(s => new LspStatusEntry(s.Language, s.Command, s.Running, s.Available))
            .ToList();
        return Task.FromResult(ctx.Ok(new LspStatusResult(servers), LspCommandsJsonContext.Default.LspStatusResult));
    }

    private static async Task SyncDocumentAsync(LspService lsp, LspServer server, string filePath, string language, string content)
    {
        var version = lsp.NextDocumentVersion(filePath, out var alreadyOpen);
        var encodedContent = JsonSerializer.Serialize(content, LspCommandsJsonContext.Default.String);
        if (alreadyOpen)
        {
            var didChange = ParseJson($$$"""{"textDocument":{"uri":{{{EncodeUri(filePath)}}},"version":{{{version}}}},"contentChanges":[{"text":{{{encodedContent}}}}]}""");
            await server.SendNotificationAsync("textDocument/didChange", didChange);
        }
        else
        {
            var didOpen = ParseJson($$$"""{"textDocument":{"uri":{{{EncodeUri(filePath)}}},"languageId":"{{{language}}}","version":{{{version}}},"text":{{{encodedContent}}}}}""");
            await server.SendNotificationAsync("textDocument/didOpen", didOpen);
        }
    }

    private static IReadOnlyList<LspDiagnosticDto> MapDiagnosticArray(JsonElement items)
    {
        var diagnostics = new List<LspDiagnosticDto>();
        if (items.ValueKind != JsonValueKind.Array)
            return diagnostics;
        foreach (var item in items.EnumerateArray())
        {
            if (!item.TryGetProperty("range", out var range)) continue;
            var start = range.GetProperty("start");
            var end = range.GetProperty("end");
            var severity = item.TryGetProperty("severity", out var sev) && sev.TryGetInt32(out var sevNum)
                ? MapSeverity(sevNum)
                : "error";
            var message = item.TryGetProperty("message", out var msg) ? msg.GetString() ?? "" : "";
            var code = item.TryGetProperty("code", out var codeEl)
                ? codeEl.ValueKind == JsonValueKind.String ? codeEl.GetString() : codeEl.GetRawText()
                : null;
            diagnostics.Add(new LspDiagnosticDto(
                start.GetProperty("line").GetInt32(),
                start.GetProperty("character").GetInt32(),
                end.GetProperty("line").GetInt32(),
                end.GetProperty("character").GetInt32(),
                severity,
                message,
                code));
        }
        return diagnostics;
    }

    private static string MapSeverity(int lspSeverity) => lspSeverity switch
    {
        1 => "error",
        2 => "warning",
        3 => "info",
        4 => "hint",
        _ => "info",
    };

    private static string? ExtractHoverContents(JsonElement? result)
    {
        if (result is not { } r || r.ValueKind != JsonValueKind.Object || !r.TryGetProperty("contents", out var contents))
            return null;
        return contents.ValueKind switch
        {
            JsonValueKind.String => contents.GetString(),
            JsonValueKind.Object when contents.TryGetProperty("value", out var value) => value.GetString(),
            JsonValueKind.Array => string.Join("\n", contents.EnumerateArray().Select(c =>
                c.ValueKind == JsonValueKind.String ? c.GetString() ?? ""
                : c.TryGetProperty("value", out var v) ? v.GetString() ?? "" : "")),
            _ => contents.GetRawText(),
        };
    }

    private static IReadOnlyList<LspLocationDto> MapLocations(JsonElement? result)
    {
        var locations = new List<LspLocationDto>();
        if (result is not { } r)
            return locations;
        if (r.ValueKind == JsonValueKind.Array)
        {
            foreach (var loc in r.EnumerateArray())
                if (TryMapLocation(loc, out var dto))
                    locations.Add(dto);
        }
        else if (r.ValueKind == JsonValueKind.Object && TryMapLocation(r, out var single))
        {
            locations.Add(single);
        }
        return locations;
    }

    private static bool TryMapLocation(JsonElement loc, out LspLocationDto dto)
    {
        dto = new LspLocationDto("", 0, 0);
        var uriProp = loc.TryGetProperty("uri", out var u) ? u
            : loc.TryGetProperty("targetUri", out var tu) ? tu
            : default;
        if (uriProp.ValueKind != JsonValueKind.String) return false;
        var rangeProp = loc.TryGetProperty("range", out var rg) ? rg
            : loc.TryGetProperty("targetSelectionRange", out var tr) ? tr
            : default;
        if (rangeProp.ValueKind != JsonValueKind.Object) return false;
        var start = rangeProp.GetProperty("start");
        var uri = uriProp.GetString() ?? "";
        var filePath = uri.StartsWith("file://", StringComparison.Ordinal) ? uri["file://".Length..] : uri;
        dto = new LspLocationDto(filePath, start.GetProperty("line").GetInt32(), start.GetProperty("character").GetInt32());
        return true;
    }

    private static string EncodeUri(string filePath)
        => JsonSerializer.Serialize("file://" + filePath, LspCommandsJsonContext.Default.String);

    private static JsonElement ParseJson(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }
}

public sealed record LspDocumentParams(string FilePath, string Content, string RootDir);
public sealed record LspPositionParams(string FilePath, string Content, string RootDir, int Line, int Col);
public sealed record LspDiagnosticDto(int StartLine, int StartCol, int EndLine, int EndCol, string Severity, string Message, string? Code);
public sealed record LspDiagnosticsResult(bool Available, string? Language, IReadOnlyList<LspDiagnosticDto> Diagnostics);
public sealed record LspHoverResult(bool Available, string? Contents);
public sealed record LspLocationDto(string FilePath, int Line, int Col);
public sealed record LspDefinitionResult(bool Available, IReadOnlyList<LspLocationDto> Locations);
public sealed record LspStatusEntry(string Language, string Command, bool Running, bool Available);
public sealed record LspStatusResult(IReadOnlyList<LspStatusEntry> Servers);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(LspDocumentParams))]
[JsonSerializable(typeof(LspPositionParams))]
[JsonSerializable(typeof(LspDiagnosticDto))]
[JsonSerializable(typeof(LspDiagnosticsResult))]
[JsonSerializable(typeof(LspHoverResult))]
[JsonSerializable(typeof(LspLocationDto))]
[JsonSerializable(typeof(LspDefinitionResult))]
[JsonSerializable(typeof(LspStatusEntry))]
[JsonSerializable(typeof(LspStatusResult))]
[JsonSerializable(typeof(string))]
public sealed partial class LspCommandsJsonContext : JsonSerializerContext { }
