using System.Xml.Linq;
using Xunit;

namespace Cove.Architecture.Tests;

public sealed class ArchitectureRulesTests
{
    private static readonly string[] ReflectionTokens =
    [
        "using System.Reflection",
        "GetCustomAttribute",
        "Assembly.GetEntryAssembly"
    ];

    private static readonly string[] NativeInteropProjects =
    [
        "Cove.Engine",
        "Cove.Gui",
        "Cove.Tui"
    ];

    [Fact]
    public void PackageReferences_DoNotSpecifyInlineVersions()
    {
        var violations = EnumerateProductionFiles("*.csproj")
            .Where(path => XDocument.Parse(File.ReadAllText(path))
                .Descendants()
                .Where(element => element.Name.LocalName == "PackageReference")
                .Any(element => element.Attributes().Any(attribute => attribute.Name.LocalName == "Version")))
            .Select(RelativePath)
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void ProductionSources_DoNotUseReflection()
    {
        var violations = FindTokenViolations(
            EnumerateProductionFiles("*.cs"),
            ReflectionTokens);

        Assert.Empty(violations);
    }

    [Fact]
    public void EngineAndGui_DoNotWriteDirectlyToConsole()
    {
        var files = EnumerateProjectFiles("Cove.Engine", "*.cs")
            .Concat(EnumerateProjectFiles("Cove.Gui", "*.cs"));
        var violations = FindTokenViolations(
            files,
            ["Console.WriteLine", "Console.Error.WriteLine"]);

        Assert.Empty(violations);
    }

    [Fact]
    public void EngineGuiAndTui_DoNotDeclareNativeImports()
    {
        var files = NativeInteropProjects
            .SelectMany(project => EnumerateProjectFiles(project, "*.cs"));
        var violations = FindTokenViolations(
            files,
            ["[LibraryImport", "[DllImport"]);

        Assert.Empty(violations);
    }

    [Fact]
    public void ProjectReferences_FollowLayeringRules()
    {
        var protocolReferences = GetProjectReferences("Cove.Protocol");
        var forbiddenProtocolReferences = new[]
        {
            "Cove.Engine",
            "Cove.Gui",
            "Cove.Cli",
            "Cove.Tui",
            "Cove.Adapters"
        };
        var violations = protocolReferences
            .Intersect(forbiddenProtocolReferences, StringComparer.Ordinal)
            .Select(reference => $"src/Cove.Protocol/Cove.Protocol.csproj -> {reference}")
            .Concat(GetProjectReferences("Cove.Platform")
                .Where(reference => reference == "Cove.Engine")
                .Select(reference => $"src/Cove.Platform/Cove.Platform.csproj -> {reference}"))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(violations);
    }

    private static string[] FindTokenViolations(
        IEnumerable<string> files,
        IReadOnlyList<string> tokens)
    {
        return files
            .SelectMany(path =>
            {
                var source = File.ReadAllText(path);
                return tokens
                    .Where(source.Contains)
                    .Select(token => $"{RelativePath(path)}: {token}");
            })
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static string[] GetProjectReferences(string projectName)
    {
        var projectPath = Path.Combine(SourceRoot, projectName, $"{projectName}.csproj");
        return XDocument.Parse(File.ReadAllText(projectPath))
            .Descendants()
            .Where(element => element.Name.LocalName == "ProjectReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => Path.GetFileNameWithoutExtension(value!))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static IEnumerable<string> EnumerateProductionFiles(string pattern)
    {
        return Directory.EnumerateFiles(SourceRoot, pattern, SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(path))
            .Order(StringComparer.Ordinal);
    }

    private static IEnumerable<string> EnumerateProjectFiles(string projectName, string pattern)
    {
        var projectRoot = Path.Combine(SourceRoot, projectName);
        return Directory.EnumerateFiles(projectRoot, pattern, SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(path))
            .Order(StringComparer.Ordinal);
    }

    private static bool IsBuildOutput(string path)
    {
        var relativePath = Path.GetRelativePath(SourceRoot, path);
        return relativePath.Split(Path.DirectorySeparatorChar)
            .Any(segment => segment is "bin" or "obj");
    }

    private static string RelativePath(string path)
    {
        return Path.GetRelativePath(RepositoryRoot, path)
            .Replace(Path.DirectorySeparatorChar, '/');
    }

    private static string SourceRoot => Path.Combine(RepositoryRoot, "src");

    private static string RepositoryRoot
    {
        get
        {
            var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
            Assert.True(Directory.Exists(Path.Combine(root, "src")), $"Repository src directory not found from {AppContext.BaseDirectory}");
            return root;
        }
    }
}
