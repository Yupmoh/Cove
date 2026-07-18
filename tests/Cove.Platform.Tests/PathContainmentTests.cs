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

    private static string MakeRealRoot()
    {
        var root = Root();
        Directory.CreateDirectory(root);
        return root;
    }

    [Fact]
    public void IsContainedPhysical_AcceptsRealChild()
    {
        var root = MakeRealRoot();
        var child = Path.Combine(root, "inner", "file.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(child)!);
        File.WriteAllText(child, "x");
        Assert.True(PathContainment.IsContainedPhysical(root, child));
    }

    [Fact]
    public void IsContainedPhysical_AcceptsNonexistentTailUnderRoot()
    {
        var root = MakeRealRoot();
        Assert.True(PathContainment.IsContainedPhysical(root, Path.Combine(root, "not-yet", "created.txt")));
    }

    [Fact]
    public void IsContainedPhysical_RejectsSymlinkFileEscapingRoot()
    {
        if (OperatingSystem.IsWindows()) return;
        var root = MakeRealRoot();
        var outside = Path.Combine(Path.GetTempPath(), "cove-contain-secret-" + Guid.NewGuid().ToString("N"));
        File.WriteAllText(outside, "secret");
        var link = Path.Combine(root, "innocent.txt");
        File.CreateSymbolicLink(link, outside);
        Assert.True(PathContainment.IsContained(root, link));
        Assert.False(PathContainment.IsContainedPhysical(root, link));
    }

    [Fact]
    public void IsContainedPhysical_RejectsSymlinkDirectoryEscapingRoot()
    {
        if (OperatingSystem.IsWindows()) return;
        var root = MakeRealRoot();
        var outsideDir = Path.Combine(Path.GetTempPath(), "cove-contain-outside-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outsideDir);
        File.WriteAllText(Path.Combine(outsideDir, "target.txt"), "x");
        var linkDir = Path.Combine(root, "sub");
        Directory.CreateSymbolicLink(linkDir, outsideDir);
        Assert.False(PathContainment.IsContainedPhysical(root, Path.Combine(linkDir, "target.txt")));
    }

    [Fact]
    public void IsContainedPhysical_AcceptsRootReachedThroughSymlink()
    {
        if (OperatingSystem.IsWindows()) return;
        var realRoot = MakeRealRoot();
        var linkRoot = Path.Combine(Path.GetTempPath(), "cove-contain-link-" + Guid.NewGuid().ToString("N"));
        Directory.CreateSymbolicLink(linkRoot, realRoot);
        var child = Path.Combine(linkRoot, "file.txt");
        File.WriteAllText(child, "x");
        Assert.True(PathContainment.IsContainedPhysical(linkRoot, child));
        Assert.True(PathContainment.IsContainedPhysical(realRoot, child));
    }

    [Fact]
    public void IsContainedPhysical_RejectsSymlinkChainEscape()
    {
        if (OperatingSystem.IsWindows()) return;
        var root = MakeRealRoot();
        var outside = Path.Combine(Path.GetTempPath(), "cove-contain-chain-" + Guid.NewGuid().ToString("N"));
        File.WriteAllText(outside, "secret");
        var hop = Path.Combine(root, "hop");
        var entry = Path.Combine(root, "entry");
        File.CreateSymbolicLink(hop, outside);
        File.CreateSymbolicLink(entry, hop);
        Assert.False(PathContainment.IsContainedPhysical(root, entry));
    }

    [Fact]
    public void IsContainedPhysical_RejectsLinkWithUnresolvedOutsideTarget()
    {
        if (OperatingSystem.IsWindows()) return;
        var root = MakeRealRoot();
        var pendingOutside = Path.Combine(Path.GetTempPath(), "cove-contain-pending-" + Guid.NewGuid().ToString("N"), "later.txt");
        var link = Path.Combine(root, "later.txt");
        File.CreateSymbolicLink(link, pendingOutside);
        Assert.False(PathContainment.IsContainedPhysical(root, link));
    }

    [Fact]
    public void IsContainedPhysical_AcceptsLinkWithUnresolvedInsideTarget()
    {
        if (OperatingSystem.IsWindows()) return;
        var root = MakeRealRoot();
        var pendingInside = Path.Combine(root, "future", "later.txt");
        var link = Path.Combine(root, "alias.txt");
        File.CreateSymbolicLink(link, pendingInside);
        Assert.True(PathContainment.IsContainedPhysical(root, link));
    }
}
