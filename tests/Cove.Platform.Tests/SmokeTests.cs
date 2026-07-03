using Xunit;

namespace Cove.Platform.Tests;

public class SmokeTests
{
    [Fact]
    public void TestHost_Runs()
    {
        var sum = 2 + 2;
        Assert.Equal(4, sum);
    }
}
