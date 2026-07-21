using Xunit;

namespace Cove.Architecture.Tests;

public sealed class GuiVersionContractTests
{
    [Fact]
    public void Program_UsesGeneratedMinVerVersion()
    {
        var source = File.ReadAllText(Path.Combine(RepositoryRoot, "src", "Cove.Gui", "Program.cs"));

        Assert.Contains("var version = CoveBuild.InformationalVersion;", source);
        Assert.DoesNotContain("const string version", source);
    }

    private static string RepositoryRoot
    {
        get
        {
            var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
            Assert.True(Directory.Exists(Path.Combine(root, "src")));
            return root;
        }
    }
}
