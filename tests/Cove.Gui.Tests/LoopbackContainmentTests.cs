using Cove.Platform;
using Xunit;

public class LoopbackContainmentTests
{
    [Fact]
    public void Static_WebRoot_Rejects_Sibling_With_Shared_Prefix()
    {
        var parent = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var webRoot = Path.Combine(parent, "wwwroot");
        var sibling = Path.Combine(parent, "wwwroot-evil", "index.html");

        Assert.False(PathContainment.IsContained(webRoot, Path.GetFullPath(sibling)));
    }

    [Fact]
    public void Static_WebRoot_Accepts_Real_Child()
    {
        var parent = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var webRoot = Path.Combine(parent, "wwwroot");
        var child = Path.Combine(webRoot, "assets", "app.js");

        Assert.True(PathContainment.IsContained(webRoot, Path.GetFullPath(child)));
    }
}
