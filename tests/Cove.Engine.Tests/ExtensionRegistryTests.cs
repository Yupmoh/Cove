using System.IO;
using Cove.Adapters;
using Cove.Engine.Protocol;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Cove.Testing;

namespace Cove.Engine.Tests;

public sealed class ExtensionRegistryTests
{
    private static string NewDir() => Path.Combine(Path.GetTempPath(), "cove-ext-" + Guid.NewGuid().ToString("N"));

    private static string WriteAdapter(string root, string name, params (string method, string script)[] methods)
    {
        var dir = Path.Combine(root, name);
        Directory.CreateDirectory(dir);
        var methodsJson = string.Join(",", methods.Select(m => $"\"{m.method}\":{{\"script\":\"{m.script}\"}}"));
        var manifestJson = $"{{\"sdkVersion\":2,\"name\":\"{name}\",\"displayName\":\"{name}\",\"description\":\"test adapter\",\"accent\":\"#000000\",\"binary\":\"{name}\",\"version\":\"1.0.0\",\"methods\":{{{methodsJson}}}}}";
        File.WriteAllText(Path.Combine(dir, "adapter.json"), manifestJson);
        foreach (var (method, script) in methods)
            File.WriteAllText(Path.Combine(dir, script), script);
        return dir;
    }

    [Fact]
    public void List_EmptyWhenNoAdapters()
    {
        var root = NewDir();
        try
        {
            var manifests = new AdapterManifestStore(root, NullLogger.Instance);
            var registry = new ExtensionRegistry(manifests);
            Assert.Empty(registry.List());
        }
        finally { Cove.Testing.TestDirectory.Delete(root); }
    }

    [Fact]
    public void Index_AdapterMethodsBecomeExtensionCommands()
    {
        var root = NewDir();
        try
        {
            WriteAdapter(root, "test-adapter", ("check_auth", "check_auth.sh"), ("statusline", "statusline.sh"));
            var manifests = new AdapterManifestStore(root, NullLogger.Instance);
            var registry = new ExtensionRegistry(manifests);
            registry.Index();
            var commands = registry.List();
            Assert.Contains(commands, c => c.Command == "extension.test-adapter.check_auth");
            Assert.Contains(commands, c => c.Command == "extension.test-adapter.statusline");
        }
        finally { Cove.Testing.TestDirectory.Delete(root); }
    }

    [Fact]
    public void List_CommandsHaveSourceExtension()
    {
        var root = NewDir();
        try
        {
            WriteAdapter(root, "my-adapter", ("check_auth", "check_auth.sh"));
            var manifests = new AdapterManifestStore(root, NullLogger.Instance);
            var registry = new ExtensionRegistry(manifests);
            registry.Index();
            foreach (var cmd in registry.List())
                Assert.Equal("extension", cmd.Source);
        }
        finally { Cove.Testing.TestDirectory.Delete(root); }
    }

    [Fact]
    public void Resolve_ReturnsMethodAndAdapter()
    {
        var root = NewDir();
        try
        {
            WriteAdapter(root, "claude-code", ("check_auth", "check_auth.sh"));
            var manifests = new AdapterManifestStore(root, NullLogger.Instance);
            var registry = new ExtensionRegistry(manifests);
            registry.Index();
            var resolved = registry.Resolve("extension.claude-code.check_auth");
            Assert.NotNull(resolved);
            Assert.Equal("claude-code", resolved!.Adapter);
            Assert.Equal("check_auth", resolved.Method);
        }
        finally { Cove.Testing.TestDirectory.Delete(root); }
    }

    [Fact]
    public void Resolve_UnknownReturnsNull()
    {
        var root = NewDir();
        try
        {
            var manifests = new AdapterManifestStore(root, NullLogger.Instance);
            var registry = new ExtensionRegistry(manifests);
            registry.Index();
            Assert.Null(registry.Resolve("extension.bogus.method"));
        }
        finally { Cove.Testing.TestDirectory.Delete(root); }
    }

    [Fact]
    public void Resolve_AmbiguousPrefixReturnsNull()
    {
        var root = NewDir();
        try
        {
            WriteAdapter(root, "a", ("check_auth", "check_auth.sh"));
            WriteAdapter(root, "b", ("check_auth", "check_auth.sh"));
            var manifests = new AdapterManifestStore(root, NullLogger.Instance);
            var registry = new ExtensionRegistry(manifests);
            registry.Index();
            Assert.Null(registry.Resolve("extension.check_auth"));
        }
        finally { Cove.Testing.TestDirectory.Delete(root); }
    }

    [Fact]
    public void Index_ReloadsNewAdapters()
    {
        var root = NewDir();
        try
        {
            var manifests = new AdapterManifestStore(root, NullLogger.Instance);
            var registry = new ExtensionRegistry(manifests);
            registry.Index();
            Assert.Empty(registry.List());

            WriteAdapter(root, "new-adapter", ("check_auth", "check_auth.sh"));
            registry.Index();
            Assert.NotEmpty(registry.List());
        }
        finally { Cove.Testing.TestDirectory.Delete(root); }
    }


    [PlatformFact(TestOperatingSystem.Unix)]
    public async Task ExtensionRun_DispatchesThroughVerbHandlerAndReturnsOutput()
    {
        var root = NewDir();
        try
        {
            WriteAdapter(root, "echo-adapter", ("echo", "echo.sh"));
            File.WriteAllText(Path.Combine(root, "echo-adapter", "echo.sh"), "#!/bin/bash\necho hello-from-adapter\n");
            var manifests = new AdapterManifestStore(root, NullLogger.Instance);
            var registry = new ExtensionRegistry(manifests);
            var prm = System.Text.Json.JsonDocument.Parse("{\"command\":\"extension.echo-adapter.echo\"}").RootElement.Clone();
            var request = new Cove.Protocol.ControlRequest("1", "cove://commands/extension.run", prm);
            var response = await Cove.Engine.EngineCommandRouter.RouteAsync(request, extensions: registry, manifestStore: manifests);
            Assert.NotNull(response);
            Assert.True(response!.Ok);
            Assert.Contains("hello-from-adapter", response.Data!.Value.GetProperty("output").GetString());
        }
        finally { Cove.Testing.TestDirectory.Delete(root); }
    }
}
