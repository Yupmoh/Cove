using Xunit;

namespace Cove.Pty.Harness;

public class SmokeTests
{
    [Fact]
    public void TestHost_Runs()
    {
        var sum = 2 + 2;
        Assert.Equal(4, sum);
    }
}
