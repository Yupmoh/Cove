using System.Text.Json;
using Cove.Adapters;
using Microsoft.Extensions.Logging;

namespace Cove.Engine.Launch;

public interface ILauncherOptionsResolver
{
    Task<LauncherOptionsResult?> LoadAsync(
        string adapterName,
        CancellationToken cancellationToken = default);
}

public sealed class LauncherOptionsResolver(
    ILaunchAdapterLookup adapters,
    ILaunchProcessAcquirer processes,
    LauncherOptionsParser parser,
    ILogger? logger = null) : ILauncherOptionsResolver
{
    public async Task<LauncherOptionsResult?> LoadAsync(
        string adapterName,
        CancellationToken cancellationToken = default)
    {
        var adapter = adapters.Find(adapterName);
        if (adapter is null
            || !adapter.Manifest.Methods.TryGetValue(
                "launcher_options",
                out var method))
        {
            return null;
        }

        JsonElement output;
        if (!string.IsNullOrEmpty(method.Static))
        {
            var staticPath = Path.Combine(
                adapter.Directory,
                method.Static);
            if (!File.Exists(staticPath))
            {
                logger?.LauncherOptionsStaticMissing(
                    adapterName,
                    method.Static);
                return null;
            }

            try
            {
                using var document = JsonDocument.Parse(
                    File.ReadAllText(staticPath));
                output = document.RootElement.Clone();
            }
            catch (JsonException exception)
            {
                logger?.LauncherOptionsParseFailed(
                    adapterName,
                    exception.Message);
                return null;
            }
        }
        else if (!string.IsNullOrEmpty(method.Script))
        {
            var result = await processes.RunMethodAsync(
                adapter,
                "launcher_options",
                method.Script,
                Array.Empty<string>(),
                cancellationToken).ConfigureAwait(false);
            if (!result.Ok || result.Json is not { } json)
            {
                logger?.LauncherOptionsScriptFailed(
                    adapterName,
                    result.ExitCode,
                    result.Stderr);
                return null;
            }

            output = json;
        }
        else
        {
            return null;
        }

        return parser.Parse(output);
    }
}

public sealed class LauncherOptionsParser
{
    public LauncherOptionsResult? Parse(JsonElement json)
    {
        if (!json.TryGetProperty("options", out var optionsProperty)
            || optionsProperty.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var options = new List<LauncherOption>();
        foreach (var item in optionsProperty.EnumerateArray())
        {
            if (!item.TryGetProperty("key", out var keyProperty)
                || !item.TryGetProperty("label", out var labelProperty)
                || !item.TryGetProperty("type", out var typeProperty))
            {
                continue;
            }

            string? defaultValueRaw = null;
            if (item.TryGetProperty(
                    "default",
                    out var defaultProperty)
                && defaultProperty.ValueKind != JsonValueKind.Null)
            {
                defaultValueRaw =
                    defaultProperty.ValueKind == JsonValueKind.String
                        ? defaultProperty.GetString()
                        : defaultProperty.GetRawText();
            }

            List<LauncherOptionChoice>? choices = null;
            if (item.TryGetProperty(
                    "choices",
                    out var choicesProperty)
                && choicesProperty.ValueKind == JsonValueKind.Array)
            {
                choices = [];
                foreach (var choice in choicesProperty.EnumerateArray())
                {
                    if (choice.ValueKind == JsonValueKind.String)
                    {
                        choices.Add(
                            new LauncherOptionChoice(
                                choice.GetString()!,
                                null));
                    }
                    else if (choice.TryGetProperty(
                                 "value",
                                 out var valueProperty))
                    {
                        choices.Add(
                            new LauncherOptionChoice(
                                valueProperty.GetString()!,
                                choice.TryGetProperty(
                                    "label",
                                    out var choiceLabelProperty)
                                        ? choiceLabelProperty.GetString()
                                        : null));
                    }
                }
            }

            options.Add(
                new LauncherOption(
                    keyProperty.GetString()!,
                    labelProperty.GetString()!,
                    typeProperty.GetString()!,
                    defaultValueRaw,
                    choices));
        }

        var suggested = new List<LauncherSuggestedFlag>();
        if (json.TryGetProperty(
                "suggestedFlags",
                out var suggestedFlagsProperty)
            && suggestedFlagsProperty.ValueKind == JsonValueKind.Array)
        {
            foreach (var item
                     in suggestedFlagsProperty.EnumerateArray())
            {
                if (!item.TryGetProperty(
                        "flag",
                        out var flagProperty))
                {
                    continue;
                }

                var description =
                    item.TryGetProperty(
                        "description",
                        out var descriptionProperty)
                        ? descriptionProperty.GetString()
                        : null;
                List<string>? values = null;
                if (item.TryGetProperty(
                        "values",
                        out var valuesProperty)
                    && valuesProperty.ValueKind == JsonValueKind.Array)
                {
                    values = [];
                    foreach (var value
                             in valuesProperty.EnumerateArray())
                    {
                        var stringValue = value.GetString();
                        if (stringValue is not null)
                            values.Add(stringValue);
                    }
                }

                suggested.Add(
                    new LauncherSuggestedFlag(
                        flagProperty.GetString()!,
                        description,
                        values));
            }
        }

        return new LauncherOptionsResult(options, suggested);
    }
}
