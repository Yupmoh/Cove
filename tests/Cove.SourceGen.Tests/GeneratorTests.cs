using System.Collections;
using Cove.SourceGen;
using Xunit;

namespace Cove.SourceGen.Tests;

public sealed class GeneratorTests
{
    private const string AttributeSource = """
        namespace Cove.Protocol;
        [System.AttributeUsage(System.AttributeTargets.Method)]
        public sealed class CoveCommandAttribute : System.Attribute
        {
            public CoveCommandAttribute(string key) => Key = key;
            public string Key { get; }
            public string? Description { get; set; }
            public string? Source { get; set; }
        }
        """;

    private const string ContractSource = """
        namespace Cove.Cli
        {
            public sealed class CommandContext(int value)
            {
                public int Value { get; } = value;
            }
        }

        namespace Cove.Engine
        {
            public sealed class EngineDispatchContext(int value)
            {
                public int Value { get; } = value;
            }
        }

        namespace Cove.Protocol
        {
            public sealed class ControlResponse(int value)
            {
                public int Value { get; } = value;
            }
        }
        """;

    [Fact]
    public async Task EmitsExactHandlerRegistryForAttributedMethods()
    {
        const string source = """
            using Cove.Protocol;

            namespace Cove.Cli
            {
                internal static class CliCommands
                {
                    [CoveCommand("version")]
                    public static System.Threading.Tasks.Task<int> Version(
                        CommandContext command) =>
                        System.Threading.Tasks.Task.FromResult(command.Value + 1);

                    [CoveCommand("nook list")]
                    public static System.Threading.Tasks.Task<int> NookList(
                        CommandContext command) =>
                        System.Threading.Tasks.Task.FromResult(command.Value + 2);
                }
            }

            namespace Cove.Engine
            {
                internal static class EngineCommands
                {
                    [CoveCommand("cove://commands/ping")]
                    public static System.Threading.Tasks.Task<ControlResponse> Ping(
                        EngineDispatchContext context) =>
                        System.Threading.Tasks.Task.FromResult(
                            new ControlResponse(context.Value + 3));
                }
            }
            """;

        var (output, diagnostics) = GeneratorTestHarness.Run(
            new CoveCommandGenerator(),
            ("CoveCommandAttribute.cs", AttributeSource),
            ("DispatchContracts.cs", ContractSource),
            ("CliCommands.cs", source));

        Assert.Empty(diagnostics);
        var generated = Assert.Single(output.SyntaxTrees, tree => tree.FilePath.EndsWith("CoveCommandRegistry.g.cs"));
        Assert.NotEmpty(generated.GetRoot().DescendantNodes());
        var assembly = GeneratorTestHarness.EmitAndLoad(output);
        var registry = assembly.GetType("Cove.Generated.CoveCommandRegistry");
        Assert.NotNull(registry);
        var handlers = Assert.IsAssignableFrom<IReadOnlyDictionary<string, Delegate>>(
            registry.GetField("Handlers")!.GetValue(null));

        Assert.Equal(
            new[]
            {
                "cove://commands/ping",
                "nook list",
                "version"
            },
            handlers.Keys.Order(StringComparer.Ordinal));
        Assert.Equal("NookList", handlers["nook list"].Method.Name);
        Assert.Equal("Version", handlers["version"].Method.Name);

        var cliContextType = assembly.GetType(
            "Cove.Cli.CommandContext")!;
        var cliContext = Activator.CreateInstance(
            cliContextType,
            40)!;
        var cliResult = await (Task<int>)handlers["version"]
            .DynamicInvoke(cliContext)!;
        Assert.Equal(41, cliResult);

        var engineContextType = assembly.GetType(
            "Cove.Engine.EngineDispatchContext")!;
        var engineContext = Activator.CreateInstance(
            engineContextType,
            40)!;
        var coreTask = (Task)handlers["cove://commands/ping"]
            .DynamicInvoke(engineContext)!;
        await coreTask;
        var coreResult = coreTask.GetType()
            .GetProperty("Result")!
            .GetValue(coreTask)!;
        Assert.Equal(
            43,
            coreResult.GetType()
                .GetProperty("Value")!
                .GetValue(coreResult));
    }

    [Fact]
    public void ThirdVerbAppearsInExactCatalogueWithoutGeneratorChanges()
    {
        const string source = """
            using Cove.Protocol;
            namespace Cove.Cli;
            internal static class CliCommands
            {
                [CoveCommand("version")]
                public static System.Threading.Tasks.Task<int> Version(
                    CommandContext command) =>
                    System.Threading.Tasks.Task.FromResult(0);

                [CoveCommand("nook list")]
                public static System.Threading.Tasks.Task<int> NookList(
                    CommandContext command) =>
                    System.Threading.Tasks.Task.FromResult(0);

                [CoveCommand("theme list", Description = "Lists themes", Source = "core")]
                public static System.Threading.Tasks.Task<int> ThemeList(
                    CommandContext command) =>
                    System.Threading.Tasks.Task.FromResult(0);
            }
            """;

        var (output, diagnostics) = GeneratorTestHarness.Run(
            new CoveCommandGenerator(),
            ("CoveCommandAttribute.cs", AttributeSource),
            ("DispatchContracts.cs", ContractSource),
            ("CliCommands.cs", source));

        Assert.Empty(diagnostics);
        var assembly = GeneratorTestHarness.EmitAndLoad(output);
        var registry = assembly.GetType("Cove.Generated.CoveCommandRegistry");
        Assert.NotNull(registry);
        var keys = Assert.IsAssignableFrom<IReadOnlyList<string>>(
            registry.GetField("Keys")!.GetValue(null));
        Assert.Equal(new[] { "nook list", "theme list", "version" }, keys);

        var catalogue = Assert.IsAssignableFrom<IEnumerable>(registry.GetField("Catalogue")!.GetValue(null))
            .Cast<object>()
            .ToArray();
        Assert.Equal(3, catalogue.Length);
        var theme = Assert.Single(catalogue, entry =>
            (string)entry.GetType().GetProperty("Command")!.GetValue(entry)! == "theme list");
        Assert.Equal("Lists themes", theme.GetType().GetProperty("Description")!.GetValue(theme));
        Assert.Equal("core", theme.GetType().GetProperty("Source")!.GetValue(theme));
    }
}
