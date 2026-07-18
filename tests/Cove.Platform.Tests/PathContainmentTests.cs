using Xunit;

namespace Cove.Platform.Tests;

public sealed class PathContainmentTests
{
    private static string Root() => Path.Combine(Path.GetTempPath(), "cove-contain-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void IsContained_AcceptsChildPath()
    {
        var root = Root();
        Assert.True(PathContainment.IsContained(root, Path.Combine(root, "a", "b.txt")));
    }

    [Fact]
    public void IsContained_AcceptsRootItself()
    {
        var root = Root();
        Assert.True(PathContainment.IsContained(root, root));
        Assert.True(PathContainment.IsContained(root, root + Path.DirectorySeparatorChar));
    }

    [Fact]
    public void IsContained_RejectsSiblingPrefixEscape()
    {
        var root = Path.Combine(Path.GetTempPath(), "cove-wwwroot");
        var sibling = Path.Combine(Path.GetTempPath(), "cove-wwwroot-evil", "x.js");
        Assert.False(PathContainment.IsContained(root, sibling));
    }

    [Fact]
    public void IsContained_RejectsParentTraversal()
    {
        var root = Root();
        Assert.False(PathContainment.IsContained(root, Path.Combine(root, "..", "themes")));
    }

    [Fact]
    public void IsContained_RejectsEmpty()
    {
        Assert.False(PathContainment.IsContained("", "/x"));
        Assert.False(PathContainment.IsContained("/x", ""));
    }

    [Fact]
    public void TryResolveContained_AcceptsSafeSegments()
    {
        var root = Root();
        Assert.True(PathContainment.TryResolveContained(root, out _, out var resolved, "bay1", "note1"));
        Assert.EndsWith(Path.Combine("bay1", "note1"), resolved);
    }

    [Fact]
    public void IsContained_FilesystemRootAcceptsDescendants()
    {
        var root = OperatingSystem.IsWindows() ? "C:\\" : "/";
        var child = OperatingSystem.IsWindows() ? "C:\\Windows\\x" : "/etc/x";
        Assert.True(PathContainment.IsContained(root, child));
    }

    [Fact]
    public void TryResolveContained_RejectsTraversalSegment()
    {
        var root = Root();
        Assert.False(PathContainment.TryResolveContained(root, out _, out _, "..", "themes"));
    }

    [Fact]
    public void TryResolveContained_RejectsRootedSegment()
    {
        var root = Root();
        var abs = OperatingSystem.IsWindows() ? "C:\\Windows" : "/etc";
        Assert.False(PathContainment.TryResolveContained(root, out _, out _, abs, "passwd"));
    }

    [Theory]
    [InlineData("note1", true)]
    [InlineData("my-bay_2", true)]
    [InlineData("..", false)]
    [InlineData(".", false)]
    [InlineData("a/b", false)]
    [InlineData("a\\b", false)]
    [InlineData("", false)]
    [InlineData("  ", false)]
    public void IsSafeSegment_ValidatesSingleComponent(string segment, bool expected)
    {
        Assert.Equal(expected, PathContainment.IsSafeSegment(segment));
    }
}
