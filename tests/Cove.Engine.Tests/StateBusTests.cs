using System.IO;
using System.Text.Json;
using Cove.Engine.Protocol;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class StateBusTests
{
    private static string NewDir() => Path.Combine(Path.GetTempPath(), "cove-state-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Write_ThenRead_RoundTrips()
    {
        var dir = NewDir();
        try
        {
            var bus = new StateBus(dir);
            bus.Write("app", "ui", "theme", "dark");
            var (exists, value) = bus.Read("app", "ui", "theme");
            Assert.True(exists);
            Assert.Equal("dark", value);
        }
        finally { Cove.Testing.TestDirectory.Delete(dir); }
    }

    [Fact]
    public void Write_NullValue_Deletes()
    {
        var dir = NewDir();
        try
        {
            var bus = new StateBus(dir);
            bus.Write("app", "ui", "theme", "dark");
            bus.Write("app", "ui", "theme", null);
            var (exists, _) = bus.Read("app", "ui", "theme");
            Assert.False(exists);
        }
        finally { Cove.Testing.TestDirectory.Delete(dir); }
    }

    [Fact]
    public void Read_UnknownKey_ReturnsExistsFalse()
    {
        var dir = NewDir();
        try
        {
            var bus = new StateBus(dir);
            var (exists, _) = bus.Read("app", "ui", "never");
            Assert.False(exists);
        }
        finally { Cove.Testing.TestDirectory.Delete(dir); }
    }

    [Fact]
    public void Write_PersistsAcrossInstances()
    {
        var dir = NewDir();
        try
        {
            var bus1 = new StateBus(dir);
            bus1.Write("app", "ui", "theme", "dark");
            var bus2 = new StateBus(dir);
            var (exists, value) = bus2.Read("app", "ui", "theme");
            Assert.True(exists);
            Assert.Equal("dark", value);
        }
        finally { Cove.Testing.TestDirectory.Delete(dir); }
    }

    [Fact]
    public void Write_AllScopesSupported()
    {
        var dir = NewDir();
        try
        {
            var bus = new StateBus(dir);
            foreach (var scope in new[] { "app", "bay", "tab", "nook" })
                bus.Write(scope, "ns", "key", "val");
            foreach (var scope in new[] { "app", "bay", "tab", "nook" })
            {
                var (exists, value) = bus.Read(scope, "ns", "key");
                Assert.True(exists);
                Assert.Equal("val", value);
            }
        }
        finally { Cove.Testing.TestDirectory.Delete(dir); }
    }

    [Fact]
    public void IsValidScope_RejectsBogus()
    {
        Assert.True(StateBus.IsValidScope("app"));
        Assert.True(StateBus.IsValidScope("bay"));
        Assert.True(StateBus.IsValidScope("tab"));
        Assert.True(StateBus.IsValidScope("nook"));
        Assert.False(StateBus.IsValidScope("bogus"));
    }

    [Fact]
    public void Write_ValueWithQuotes_RoundTripsCleanly()
    {
        var dir = NewDir();
        try
        {
            var bus = new StateBus(dir);
            var tricky = "he said \"hi\" and \\backslash and \n newline";
            bus.Write("app", "ui", "note", tricky);
            var (exists, value) = bus.Read("app", "ui", "note");
            Assert.True(exists);
            Assert.Equal(tricky, value);
        }
        finally { Cove.Testing.TestDirectory.Delete(dir); }
    }

    [Fact]
    public void StateChanged_FiresOnWrite()
    {
        var dir = NewDir();
        try
        {
            var bus = new StateBus(dir);
            string? firedKey = null;
            bus.StateChanged += key => firedKey = key;
            bus.Write("app", "ui", "theme", "dark");
            Assert.Equal("app/ui/theme", firedKey);
        }
        finally { Cove.Testing.TestDirectory.Delete(dir); }
    }
}
