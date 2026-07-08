using System.IO;
using System.Reflection;
using Cove.Generated;
using Xunit;

namespace Cove.Cli.Tests;

public sealed class ApiContractGoldenTests
{
    private static string GoldenDir
    {
        get
        {
            var dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
            for (var i = 0; i < 6; i++)
            {
                var candidate = Path.Combine(dir, "goldens");
                if (Directory.Exists(candidate)) return candidate;
                dir = Path.GetDirectoryName(dir)!;
            }
            return Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "goldens");
        }
    }

    private static string RepoRoot
    {
        get
        {
            var dir = GoldenDir;
            for (var i = 0; i < 6; i++)
            {
                if (Directory.Exists(Path.Combine(dir, "docs")) && Directory.Exists(Path.Combine(dir, "src")))
                    return dir;
                dir = Path.GetDirectoryName(dir)!;
            }
            return GoldenDir;
        }
    }

    [Fact]
    public void CommandsCatalogue_MatchesGoldenSnapshot()
    {
        var goldenPath = Path.Combine(GoldenDir, "commands-catalogue.json");
        var expected = File.Exists(goldenPath) ? File.ReadAllText(goldenPath) : "";
        var actual = SerializeCatalogue();
        if (expected != actual)
        {
            Directory.CreateDirectory(GoldenDir);
            File.WriteAllText(Path.Combine(GoldenDir, "commands-catalogue.json.actual"), actual);
            Assert.Fail($"commands catalogue drifted from golden at {goldenPath}. Actual written to .actual — copy it over to update.");
        }
    }

    [Fact]
    public void GoldenSnapshot_IsNotEmpty()
    {
        var goldenPath = Path.Combine(GoldenDir, "commands-catalogue.json");
        Assert.True(File.Exists(goldenPath), "golden snapshot missing — run the test once to generate .actual then copy it");
        var content = File.ReadAllText(goldenPath);
        Assert.True(content.Length > 10);
    }

    [Fact]
    public void CliReferenceDoc_IsFresh()
    {
        var docPath = Path.Combine(RepoRoot, "docs", "cli-reference.md");
        Assert.True(File.Exists(docPath), "docs/cli-reference.md not found in repo");
        var expected = File.ReadAllText(docPath).TrimEnd();
        var actual = CliReferenceDoc.Generate().TrimEnd();
        if (expected != actual)
            Assert.Fail("docs/cli-reference.md is stale. Regenerate: cove docs generate docs/cli-reference.md");
    }

    private static string SerializeCatalogue()
    {
        using var buffer = new System.IO.MemoryStream();
        using (var writer = new System.Text.Json.Utf8JsonWriter(buffer, new System.Text.Json.JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartArray();
            foreach (var entry in CoveCommandRegistry.Catalogue)
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
        return System.Text.Encoding.UTF8.GetString(buffer.ToArray());
    }
}
