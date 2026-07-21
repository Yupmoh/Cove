using System.Diagnostics;
using Xunit;

namespace Cove.Architecture.Tests;

public sealed class MacOsPackagingContractTests
{
    [Fact]
    public void PackageScript_OwnsOnlyAnArm64StagingTarget()
    {
        var script = Read("scripts/package-macos-app.sh");

        Assert.Contains("RID=\"${1:-}\"", script);
        Assert.Contains("osx-arm64", script);
        Assert.DoesNotContain("osx-x64", script);
        Assert.Contains("mktemp -d", script);
        Assert.Contains("OUTPUT_ROOT", script);
        Assert.Contains("repository root", script);
        Assert.DoesNotContain("rm -rf \"$APP\"", script);
        Assert.DoesNotContain("command -v rg", script);
    }

    [Fact]
    public void PackageScript_PublishesGuiAndEngineAsNativeAot()
    {
        var script = Read("scripts/package-macos-app.sh");

        Assert.Equal(2, Count(script, "-p:PublishAot=true"));
        Assert.Equal(2, Count(script, "-p:TreatWarningsAsErrors=true"));
        Assert.Contains("src/Cove.Gui/Cove.Gui.csproj", script);
        Assert.Contains("src/Cove.Cli/Cove.Cli.csproj", script);
        Assert.Contains("cove-engine", script);
        Assert.DoesNotContain("PublishAot=false", script);
    }

    [Fact]
    public void PackageScript_ValidatesEveryMachOAndNativeDependency()
    {
        var script = Read("scripts/package-macos-app.sh");

        Assert.Contains("libcove_pty.dylib", script);
        Assert.Contains("-Wl,-no_uuid", script);
        Assert.Contains("@rpath/libcove_pty.dylib", script);
        Assert.Contains("libe_sqlite3.dylib", script);
        Assert.Contains("lipo -archs", script);
        Assert.Contains("-thin arm64", script);
        Assert.Contains("Mach-O", script);
        Assert.Contains("arm64", script);
        Assert.Contains("validate_macho", script);
    }

    [Fact]
    public void PackageScript_SignsArchivesAndChecksumsDeterministically()
    {
        var script = Read("scripts/package-macos-app.sh");

        Assert.Contains("CFBundleIdentifier", script);
        Assert.Contains("com.yupmoh.cove", script);
        Assert.Contains("codesign --force --sign - --timestamp=none", script);
        Assert.Contains("codesign --verify --deep --strict", script);
        Assert.Contains("COPYFILE_DISABLE=1", script);
        Assert.Contains("zip -X", script);
        Assert.Contains("shasum -a 256", script);
        Assert.Contains("shasum -a 256 -c", script);
        Assert.Contains("$ARCHIVE_NAME.sha256", script);
    }

    [Fact]
    public void SmokeScript_IsIsolatedBoundedAndNeverStopsExistingGui()
    {
        var script = Read("scripts/smoke-macos-app.sh");

        Assert.Contains("mktemp -d", script);
        Assert.Contains("COVE_DATA_DIR", script);
        Assert.Contains("COVE_ENGINE", script);
        Assert.Contains("COVE_GUI_PORT", script);
        Assert.Contains("-u COVE_NOOK_ID", script);
        Assert.Contains("-u COVE_NOOK_TOKEN", script);
        Assert.Contains("workspace context", script);
        Assert.Contains("status", script);
        Assert.Contains("lsof", script);
        Assert.Contains("trap cleanup", script);
        Assert.Contains("MACOS SMOKE OK", script);
        Assert.Contains("GUI_PORT", script);
        Assert.DoesNotContain("kill \"$PORT_PID\"", script);
        Assert.DoesNotContain("scripts/cove-dev.sh", script);
        Assert.DoesNotContain("TCP:7420", script);
        Assert.DoesNotContain("$HOME/.cove", script);
        Assert.DoesNotContain("~/.cove", script);
    }

    [Fact]
    public void InvalidInputs_FailWithoutDeletingOutputSentinel()
    {
        var root = RepositoryRoot;
        var output = Path.Combine(root, "artifacts", "macos-contract-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);
        var sentinel = Path.Combine(output, "sentinel");
        File.WriteAllText(sentinel, "owned");

        try
        {
            AssertFailure("osx-x64", output, "0.4.0-local");
            AssertFailure("osx-arm64", output, "bad/version");
            AssertFailure("osx-arm64", root, "0.4.0-local");
            Assert.True(File.Exists(sentinel));
            Assert.Equal("owned", File.ReadAllText(sentinel));
        }
        finally
        {
            Directory.Delete(output, true);
        }
    }

    [Fact]
    public void Readme_ExplainsPrivateArm64DistributionBoundary()
    {
        var readme = Read("README.md");

        Assert.Contains("Apple Silicon", readme);
        Assert.Contains("ad-hoc", readme);
        Assert.Contains("not notarized", readme);
        Assert.Contains("SHA-256", readme);
    }

    private static void AssertFailure(string rid, string output, string version)
    {
        using var process = Process.Start(new ProcessStartInfo("bash")
        {
            WorkingDirectory = RepositoryRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            ArgumentList =
            {
                Path.Combine(RepositoryRoot, "scripts", "package-macos-app.sh"),
                rid,
                output
            },
            Environment = { ["COVE_VERSION"] = version }
        });
        Assert.NotNull(process);
        if (!process.WaitForExit(5000))
        {
            process.Kill(true);
            process.WaitForExit();
            Assert.Fail("invalid package invocation did not fail within five seconds");
        }
        Assert.NotEqual(0, process.ExitCode);
    }

    private static int Count(string value, string needle)
    {
        var count = 0;
        var offset = 0;
        while ((offset = value.IndexOf(needle, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += needle.Length;
        }
        return count;
    }

    private static string Read(string relativePath)
        => File.ReadAllText(Path.Combine(RepositoryRoot, relativePath));

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
