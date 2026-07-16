using System.Diagnostics;
using Cove.Adapters;
using Cove.Engine.Agents;
using Cove.Engine.Hooks;
using Cove.Engine.Pty;
using Cove.Platform.Pty;
using Cove.Protocol;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Pty.Harness;

public sealed class ScreenStateScannerTests
{
    private static AdapterManifest HooklessManifest() => new()
    {
        SdkVersion = 2,
        Name = "opencode",
        DisplayName = "opencode",
        Description = "test",
        Accent = "#94e2d5",
        Binary = "opencode",
        Version = "1.0.0",
        Methods = new Dictionary<string, AdapterMethod>(),
        ScreenState = new ScreenStateDeclaration
        {
            Rules = [new ScreenStateRule { Pattern = "(?i)allow this tool.{0,20}\\(y/n\\)", Status = "needs-permission" }],
        },
    };

    [Fact]
    public void FirstScan_DetectsPromptAlreadyOnScreen()
    {
        if (OperatingSystem.IsWindows()) return;
        var logger = NullLogger.Instance;
        var host = PtyHostFactory.Create(logger);
        using var registry = new NookRegistry(host, logger);
        var info = registry.Spawn(new SpawnParams(
            "/bin/sh", new[] { "-c", "printf 'Allow this tool? (y/n) '; sleep 60" }, "/tmp", Adapter: "opencode"));

        var sw = Stopwatch.StartNew();
        while (sw.Elapsed.TotalSeconds < 10)
        {
            if (registry.TryGetScreenSample(info.NookId, 0, 4096) is { } s && s.Head > 0)
                break;
            Thread.Sleep(20);
        }

        var router = new HookEventRouter();
        var manifest = HooklessManifest();
        using var scanner = new ScreenStateScanner(registry, router, _ => manifest, logger);
        scanner.ScanOnce();

        Assert.Equal("needs-permission", router.GetNookState(info.NookId)?.Status);
        registry.Kill(info.NookId);
    }
}
