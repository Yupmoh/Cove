using System;
using System.IO;
using Microsoft.Extensions.Logging;
using Cove.Engine;
using Cove.Platform;
using Xunit;

public class LoggingAllocationTests
{
    private sealed class HotPathHost { }

    [Fact]
    public void DisabledHotPathLog_AllocatesZeroBytes()
    {
        var logDir = Path.Combine(Path.GetTempPath(), $"cove-alloc-{Guid.NewGuid():N}");
        using var factory = CoveLog.CreateEngineLoggerFactory(logDir, "test");
        var logger = factory.CreateLogger<HotPathHost>();

        for (int i = 0; i < 100_000; i++) logger.SessionActivity("7f3a", i);

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 200_000; i++) logger.SessionActivity("7f3a", i);
        long delta = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, delta);
    }
}
