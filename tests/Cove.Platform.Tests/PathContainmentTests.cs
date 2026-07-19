using Cove.Testing;
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

    private static (string Parent, string Root) MakeRealRoot()
    {
        var parent = TestDirectory.Create("cove-contain-");
        var root = Path.Combine(parent, "root");
        Directory.CreateDirectory(root);
        return (parent, root);
    }

    [PlatformFact]
    [Trait(TestTraits.Category, TestTraits.Platform)]
    public void IsContainedPhysical_AcceptsRealChild()
    {
        var (parent, root) = MakeRealRoot();
        try
        {
            var child = Path.Combine(root, "inner", "file.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(child)!);
            File.WriteAllText(child, "x");
            Assert.True(PathContainment.IsContainedPhysical(root, child));
        }
        finally { TestDirectory.Delete(parent); }
    }

    [PlatformFact]
    [Trait(TestTraits.Category, TestTraits.Platform)]
    public void IsContainedPhysical_AcceptsNonexistentTailUnderRoot()
    {
        var (parent, root) = MakeRealRoot();
        try
        {
            Assert.True(PathContainment.IsContainedPhysical(root, Path.Combine(root, "not-yet", "created.txt")));
        }
        finally { TestDirectory.Delete(parent); }
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    [Trait(TestTraits.Category, TestTraits.Platform)]
    public void IsContainedPhysical_RejectsSymlinkFileEscapingRoot()
    {
        var (parent, root) = MakeRealRoot();
        try
        {
            var outside = Path.Combine(parent, "secret.txt");
            File.WriteAllText(outside, "secret");
            var link = Path.Combine(root, "innocent.txt");
            File.CreateSymbolicLink(link, outside);
            Assert.True(PathContainment.IsContained(root, link));
            Assert.False(PathContainment.IsContainedPhysical(root, link));
        }
        finally { TestDirectory.Delete(parent); }
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    [Trait(TestTraits.Category, TestTraits.Platform)]
    public void IsContainedPhysical_RejectsSymlinkDirectoryEscapingRoot()
    {
        var (parent, root) = MakeRealRoot();
        try
        {
            var outsideDir = Path.Combine(parent, "outside");
            Directory.CreateDirectory(outsideDir);
            File.WriteAllText(Path.Combine(outsideDir, "target.txt"), "x");
            var linkDir = Path.Combine(root, "sub");
            Directory.CreateSymbolicLink(linkDir, outsideDir);
            Assert.False(PathContainment.IsContainedPhysical(root, Path.Combine(linkDir, "target.txt")));
        }
        finally { TestDirectory.Delete(parent); }
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    [Trait(TestTraits.Category, TestTraits.Platform)]
    public void IsContainedPhysical_AcceptsRootReachedThroughSymlink()
    {
        var (parent, realRoot) = MakeRealRoot();
        try
        {
            var linkRoot = Path.Combine(parent, "link");
            Directory.CreateSymbolicLink(linkRoot, realRoot);
            var child = Path.Combine(linkRoot, "file.txt");
            File.WriteAllText(child, "x");
            Assert.True(PathContainment.IsContainedPhysical(linkRoot, child));
            Assert.True(PathContainment.IsContainedPhysical(realRoot, child));
        }
        finally { TestDirectory.Delete(parent); }
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    [Trait(TestTraits.Category, TestTraits.Platform)]
    public void IsContainedPhysical_RejectsSymlinkChainEscape()
    {
        var (parent, root) = MakeRealRoot();
        try
        {
            var outside = Path.Combine(parent, "secret.txt");
            File.WriteAllText(outside, "secret");
            var hop = Path.Combine(root, "hop");
            var entry = Path.Combine(root, "entry");
            File.CreateSymbolicLink(hop, outside);
            File.CreateSymbolicLink(entry, hop);
            Assert.False(PathContainment.IsContainedPhysical(root, entry));
        }
        finally { TestDirectory.Delete(parent); }
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    [Trait(TestTraits.Category, TestTraits.Platform)]
    public void IsContainedPhysical_RejectsLinkWithUnresolvedOutsideTarget()
    {
        var (parent, root) = MakeRealRoot();
        try
        {
            var pendingOutside = Path.Combine(parent, "outside", "later.txt");
            var link = Path.Combine(root, "later.txt");
            File.CreateSymbolicLink(link, pendingOutside);
            Assert.False(PathContainment.IsContainedPhysical(root, link));
        }
        finally { TestDirectory.Delete(parent); }
    }

    [PlatformFact(TestOperatingSystem.Unix)]
    [Trait(TestTraits.Category, TestTraits.Platform)]
    public void IsContainedPhysical_AcceptsLinkWithUnresolvedInsideTarget()
    {
        var (parent, root) = MakeRealRoot();
        try
        {
            var pendingInside = Path.Combine(root, "future", "later.txt");
            var link = Path.Combine(root, "alias.txt");
            File.CreateSymbolicLink(link, pendingInside);
            Assert.True(PathContainment.IsContainedPhysical(root, link));
        }
        finally { TestDirectory.Delete(parent); }
    }
}
