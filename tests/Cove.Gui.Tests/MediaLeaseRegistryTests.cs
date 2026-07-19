using Xunit;

namespace Cove.Gui.Tests;

public sealed class MediaLeaseRegistryTests
{
    private static string MakeMediaFile(GuiTestDirectory directory, string extension)
    {
        var path = Path.Combine(directory.Path, "sample" + extension);
        File.WriteAllBytes(path, [1, 2, 3]);
        return path;
    }

    [Fact]
    public void Issue_ThenResolve_ReturnsCanonicalPath()
    {
        using var directory = GuiTestDirectory.Create("cove-media-lease-");
        var file = MakeMediaFile(directory, ".pdf");
        var registry = new MediaLeaseRegistry();
        var lease = registry.Issue(file);
        Assert.True(registry.TryResolve(lease, out var resolved));
        Assert.Equal(Path.GetFullPath(file), resolved);
    }

    [Fact]
    public void Issue_IdIsOpaqueHex()
    {
        using var directory = GuiTestDirectory.Create("cove-media-lease-");
        var file = MakeMediaFile(directory, ".mp4");
        var registry = new MediaLeaseRegistry();
        var lease = registry.Issue(file);
        Assert.Equal(64, lease.Length);
        Assert.True(lease.All(Uri.IsHexDigit));
        Assert.DoesNotContain(Path.GetFileNameWithoutExtension(file), lease, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Issue_TwoLeases_AreDistinct()
    {
        using var directory = GuiTestDirectory.Create("cove-media-lease-");
        var file = MakeMediaFile(directory, ".webm");
        var registry = new MediaLeaseRegistry();
        Assert.NotEqual(registry.Issue(file), registry.Issue(file));
    }

    [Fact]
    public void Issue_MissingFile_Throws()
    {
        using var directory = GuiTestDirectory.Create("cove-media-lease-");
        var registry = new MediaLeaseRegistry();
        var missing = Path.Combine(directory.Path, "gone.pdf");
        Assert.Throws<InvalidOperationException>(() => registry.Issue(missing));
    }

    [Fact]
    public void Issue_RelativePath_Throws()
    {
        var registry = new MediaLeaseRegistry();
        Assert.Throws<InvalidOperationException>(() => registry.Issue("relative/sample.pdf"));
    }

    [Fact]
    public void Issue_DisallowedExtension_Throws()
    {
        using var directory = GuiTestDirectory.Create("cove-media-lease-");
        var secret = Path.Combine(directory.Path, "id_rsa");
        File.WriteAllText(secret, "private");
        var registry = new MediaLeaseRegistry();
        Assert.Throws<InvalidOperationException>(() => registry.Issue(secret));
    }

    [Fact]
    public void Resolve_UnknownLease_IsFalse()
    {
        var registry = new MediaLeaseRegistry();
        Assert.False(registry.TryResolve(new string('A', 64), out _));
    }

    [Fact]
    public void Resolve_ExpiredLease_IsFalse()
    {
        using var directory = GuiTestDirectory.Create("cove-media-lease-");
        var file = MakeMediaFile(directory, ".png");
        var now = DateTimeOffset.UtcNow;
        var registry = new MediaLeaseRegistry(TimeSpan.FromMinutes(5), () => now);
        var lease = registry.Issue(file);
        Assert.True(registry.TryResolve(lease, out _));
        now += TimeSpan.FromMinutes(6);
        Assert.False(registry.TryResolve(lease, out _));
        Assert.False(registry.TryResolve(lease, out _));
    }
}
