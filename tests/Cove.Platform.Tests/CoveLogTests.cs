using Cove.Platform;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Cove.Platform.Tests;

public sealed class CoveLogTests
{
    [Fact]
    public void Named_file_logger_creates_requested_log_under_log_directory()
    {
        string root = Path.Combine(Path.GetTempPath(), "cove-log-" + Guid.NewGuid().ToString("N"));
        string logs = Path.Combine(root, "logs");

        try
        {
            using (ILoggerFactory factory = CoveLog.CreateNamedFileLoggerFactory(
                       logs,
                       "gui",
                       LogLevel.Information))
            {
                factory.CreateLogger("test").LogInformation("gui logging probe");
            }

            string path = Path.Combine(logs, "gui.log");
            Assert.True(File.Exists(path));
            Assert.Contains("gui logging probe", File.ReadAllText(path));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}
