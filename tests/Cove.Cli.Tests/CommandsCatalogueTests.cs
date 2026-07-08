using Cove.Cli;
using Cove.Generated;
using Xunit;

namespace Cove.Cli.Tests;

public sealed class CommandsCatalogueTests
{
    [Fact]
    public void Catalogue_IsNotEmpty()
    {
        Assert.NotEmpty(CoveCommandRegistry.Catalogue);
    }

    [Fact]
    public void Catalogue_EveryEntryHasCommandAndSource()
    {
        foreach (var entry in CoveCommandRegistry.Catalogue)
        {
            Assert.False(string.IsNullOrEmpty(entry.Command));
            Assert.False(string.IsNullOrEmpty(entry.Source));
        }
    }

    [Fact]
    public void Catalogue_SourceIsCliOrCore()
    {
        foreach (var entry in CoveCommandRegistry.Catalogue)
            Assert.Contains(entry.Source, new[] { "cli", "core", "extension" });
    }

    [Fact]
    public void Catalogue_CoreEntriesAreCoveUri()
    {
        foreach (var entry in CoveCommandRegistry.Catalogue)
            if (entry.Source == "core")
                Assert.StartsWith("cove://", entry.Command);
    }

    [Fact]
    public void Catalogue_CliEntriesAreSpaceSeparated()
    {
        foreach (var entry in CoveCommandRegistry.Catalogue)
            if (entry.Source == "cli")
                Assert.False(entry.Command.StartsWith("cove://"));
    }

    [Fact]
    public void Catalogue_CountMatchesKeys()
    {
        Assert.Equal(CoveCommandRegistry.Keys.Count, CoveCommandRegistry.Catalogue.Count);
    }

    [Fact]
    public void Catalogue_AttachCliVerbPresent()
    {
        Assert.Contains(CoveCommandRegistry.Catalogue, e => e.Command == "attach" && e.Source == "cli");
    }
}
