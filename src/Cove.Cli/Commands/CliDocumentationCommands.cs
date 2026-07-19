using Cove.Protocol;

namespace Cove.Cli;

internal static class CliDocumentationCommands
{
    [CoveCommand("docs generate")]
    public static Task<int> DocsGenerate(CommandContext ctx)
    {
        var args = ctx.Args;
        var outPath = args.Length > 0 ? args[0] : "docs/cli-reference.md";
        var dir = System.IO.Path.GetDirectoryName(outPath);
        if (!string.IsNullOrEmpty(dir))
            ctx.Files.CreateDirectory(dir);
        CliReferenceDoc.WriteTo(outPath, ctx.Files);
        ctx.Stdout.WriteLine($"wrote {outPath}");

        var configPath = System.IO.Path.ChangeExtension(outPath, null);
        configPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(configPath)!, "config-reference.md");
        ctx.Files.WriteAllText(
            configPath,
            Cove.Engine.Config.ConfigSchemaGenerator.GenerateReferenceDoc());
        ctx.Stdout.WriteLine($"wrote {configPath}");

        return Task.FromResult(0);
    }

    [CoveCommand("commands")]
    public static async Task<int> Commands(CommandContext ctx)
    {
        var cliCatalogue = Cove.Generated.CoveCommandRegistry.Catalogue;
        var engineCatalogue = Cove.Engine.EngineCommandCatalogue.Entries;
        var allCommands = new System.Collections.Generic.List<(string Command, string? Description, string Source)>();
        foreach (var e in cliCatalogue)
            allCommands.Add((e.Command, e.Description, e.Source));
        foreach (var e in engineCatalogue)
            allCommands.Add((e.Command, e.Description, e.Source));

        return await ctx.RouteCoreWithParamsAsync(
            "cove://commands/extension.list",
            null,
            data =>
            {
                if (data is not { ValueKind: System.Text.Json.JsonValueKind.Array } extensionCommands)
                {
                    ctx.Stderr.WriteLine("error: invalid_response");
                    return 1;
                }

                foreach (var extensionCommand in extensionCommands.EnumerateArray())
                {
                    if (extensionCommand.ValueKind != System.Text.Json.JsonValueKind.Object
                        || !extensionCommand.TryGetProperty("command", out var commandElement)
                        || commandElement.ValueKind != System.Text.Json.JsonValueKind.String
                        || !extensionCommand.TryGetProperty("source", out var sourceElement)
                        || sourceElement.ValueKind != System.Text.Json.JsonValueKind.String)
                    {
                        ctx.Stderr.WriteLine("error: invalid_response");
                        return 1;
                    }
                    string? description = null;
                    if (extensionCommand.TryGetProperty("description", out var descriptionElement)
                        && descriptionElement.ValueKind == System.Text.Json.JsonValueKind.String)
                    {
                        description = descriptionElement.GetString();
                    }
                    allCommands.Add((
                        commandElement.GetString()!,
                        description,
                        sourceElement.GetString()!));
                }

                if (ctx.IsJson)
                {
                    using var buffer = new System.IO.MemoryStream();
                    using (var writer = new System.Text.Json.Utf8JsonWriter(buffer))
                    {
                        writer.WriteStartArray();
                        foreach (var entry in allCommands)
                        {
                            writer.WriteStartObject();
                            writer.WriteString("command", entry.Command);
                            if (entry.Description is not null)
                                writer.WriteString("description", entry.Description);
                            writer.WriteString("source", entry.Source);
                            writer.WriteEndObject();
                        }
                        writer.WriteEndArray();
                        writer.Flush();
                    }
                    ctx.Stdout.WriteLine(System.Text.Encoding.UTF8.GetString(buffer.ToArray()));
                }
                else
                {
                    ctx.Stdout.WriteLine("Commands:");
                    foreach (var entry in allCommands.OrderBy(c => c.Source).ThenBy(c => c.Command))
                        ctx.Stdout.WriteLine($"  [{entry.Source}] {entry.Command}");
                    ctx.Stdout.WriteLine($"Total: {allCommands.Count}");
                }
                return 0;
            });
    }
}
