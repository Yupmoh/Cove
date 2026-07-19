using Cove.Adapters;
using Xunit;

namespace Cove.Adapters.Tests;

public sealed class HarnessUpdateCheckerTests
{
    private sealed class FakeTime : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 7, 18, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    [Theory]
    [InlineData("1.0.35", "1.0.33", true)]
    [InlineData("1.0.33", "1.0.33", false)]
    [InlineData("1.0.32", "1.0.33", false)]
    [InlineData("1.10.0", "1.9.9", true)]
    [InlineData("2.0.0", "1.99.99", true)]
    [InlineData("v2.0.0", "1.9.9", true)]
    [InlineData("1.0.0.1", "1.0.0", true)]
    [InlineData("1.0.0", "1.0.0.1", false)]
    [InlineData("1.0", "1.0.0", false)]
    [InlineData("1.0.1", "1.0", true)]
    [InlineData("latest", "1.0.0", false)]
    [InlineData("", "1.0.0", false)]
    [InlineData("1.0.1", "", false)]
    public void IsNewer_ComparesDottedNumericVersions(string latest, string installed, bool expected)
    {
        Assert.Equal(expected, HarnessUpdateChecker.IsNewer(latest, installed));
    }

    [Fact]
    public async Task GetLatestVersion_CachesWithinTtl()
    {
        var calls = 0;
        var time = new FakeTime();
        var checker = Checker((_, _) => { calls++; return Task.FromResult<string?>("2.0.0"); }, time);

        Assert.Equal("2.0.0", await checker.GetLatestVersionAsync("pkg"));
        Assert.Equal("2.0.0", await checker.GetLatestVersionAsync("pkg"));
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task GetLatestVersion_RefetchesAfterTtlExpires()
    {
        var calls = 0;
        var time = new FakeTime();
        var checker = Checker((_, _) =>
        {
            calls++;
            return Task.FromResult<string?>(calls == 1 ? "2.0.0" : "2.1.0");
        }, time);

        Assert.Equal("2.0.0", await checker.GetLatestVersionAsync("pkg"));
        time.Now = time.Now.AddMinutes(11);
        Assert.Equal("2.1.0", await checker.GetLatestVersionAsync("pkg"));
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task GetLatestVersion_CachesPerPackage()
    {
        var time = new FakeTime();
        var checker = Checker((package, _) => Task.FromResult<string?>(package == "a" ? "1.0.0" : "9.9.9"), time);

        Assert.Equal("1.0.0", await checker.GetLatestVersionAsync("a"));
        Assert.Equal("9.9.9", await checker.GetLatestVersionAsync("b"));
    }

    [Fact]
    public async Task GetLatestVersion_FetchFailureReturnsNullAndIsNegativeCached()
    {
        var calls = 0;
        var time = new FakeTime();
        var checker = Checker((_, _) => { calls++; throw new HttpRequestException("offline"); }, time);

        Assert.Null(await checker.GetLatestVersionAsync("pkg"));
        Assert.Null(await checker.GetLatestVersionAsync("pkg"));
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task GetLatestVersion_RetriesFailedFetchAfterTtl()
    {
        var calls = 0;
        var time = new FakeTime();
        var checker = Checker((_, _) =>
        {
            calls++;
            if (calls == 1)
                throw new HttpRequestException("offline");
            return Task.FromResult<string?>("3.0.0");
        }, time);

        Assert.Null(await checker.GetLatestVersionAsync("pkg"));
        time.Now = time.Now.AddMinutes(11);
        Assert.Equal("3.0.0", await checker.GetLatestVersionAsync("pkg"));
    }

    private static HarnessUpdateChecker Checker(
        Func<string, CancellationToken, Task<string?>> fetch,
        TimeProvider time)
        => new(new DelegateHarnessRegistryClient(fetch), time, TimeSpan.FromMinutes(10));

    private sealed class DelegateHarnessRegistryClient(
        Func<string, CancellationToken, Task<string?>> fetch) : IHarnessRegistryClient
    {
        public Task<string?> GetLatestVersionAsync(string package, CancellationToken cancellationToken = default)
            => fetch(package, cancellationToken);
    }
}
