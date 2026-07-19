using System;
using System.IO;
using System.Linq;
using Cove.Engine.Management;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class AdapterRetentionPolicyTests
{
    private static string NewDir() => Path.Combine(Path.GetTempPath(), "cove-retain-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Enforce_NoSessions_NoOp()
    {
        var dir = NewDir();
        Directory.CreateDirectory(dir);
        try
        {
            var policy = new AdapterRetentionPolicy(maxSessions: 10, maxAge: TimeSpan.FromDays(7));
            var removed = policy.Enforce(dir);
            Assert.Empty(removed);
        }
        finally { Cove.Testing.TestDirectory.Delete(dir); }
    }

    [Fact]
    public void Enforce_UnderLimit_KeepsAll()
    {
        var dir = NewDir();
        Directory.CreateDirectory(dir);
        try
        {
            for (int i = 0; i < 5; i++)
                File.WriteAllText(Path.Combine(dir, $"session-{i}.json"), "{}");
            var policy = new AdapterRetentionPolicy(maxSessions: 10, maxAge: TimeSpan.FromDays(7));
            var removed = policy.Enforce(dir);
            Assert.Empty(removed);
            Assert.Equal(5, Directory.GetFiles(dir).Length);
        }
        finally { Cove.Testing.TestDirectory.Delete(dir); }
    }

    [Fact]
    public void Enforce_OverLimit_RemovesOldest()
    {
        var dir = NewDir();
        Directory.CreateDirectory(dir);
        try
        {
            for (int i = 0; i < 5; i++)
            {
                var path = Path.Combine(dir, $"session-{i}.json");
                File.WriteAllText(path, "{}");
                File.SetLastWriteTime(path, DateTime.Now.AddDays(-i));
            }
            var policy = new AdapterRetentionPolicy(maxSessions: 3, maxAge: TimeSpan.FromDays(30));
            var removed = policy.Enforce(dir).ToList();
            Assert.Equal(2, removed.Count);
            Assert.Equal(3, Directory.GetFiles(dir).Length);
        }
        finally { Cove.Testing.TestDirectory.Delete(dir); }
    }

    [Fact]
    public void Enforce_ExpiredByAge_RemovesOldSessions()
    {
        var dir = NewDir();
        Directory.CreateDirectory(dir);
        try
        {
            var fresh = Path.Combine(dir, "fresh.json");
            File.WriteAllText(fresh, "{}");
            File.SetLastWriteTime(fresh, DateTime.Now);

            var old = Path.Combine(dir, "old.json");
            File.WriteAllText(old, "{}");
            File.SetLastWriteTime(old, DateTime.Now.AddDays(-10));

            var policy = new AdapterRetentionPolicy(maxSessions: 100, maxAge: TimeSpan.FromDays(7));
            var removed = policy.Enforce(dir).ToList();
            Assert.Single(removed);
            Assert.Contains("old.json", removed[0]);
        }
        finally { Cove.Testing.TestDirectory.Delete(dir); }
    }

    [Fact]
    public void Enforce_NonExistentDir_ReturnsEmpty()
    {
        var policy = new AdapterRetentionPolicy(10, TimeSpan.FromDays(7));
        var removed = policy.Enforce(Path.Combine(Path.GetTempPath(), "nonexistent-" + Guid.NewGuid().ToString("N")));
        Assert.Empty(removed);
    }
}

public sealed class FirstRunWizardTests
{
    private static string NewDir() => Path.Combine(Path.GetTempPath(), "cove-firstrun-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void IsFirstRun_NoAdaptersDir_ReturnsTrue()
    {
        var dir = NewDir();
        Directory.CreateDirectory(dir);
        try
        {
            var wizard = new FirstRunWizard(dir);
            Assert.True(wizard.IsFirstRun());
        }
        finally { Cove.Testing.TestDirectory.Delete(dir); }
    }

    [Fact]
    public void IsFirstRun_EmptyAdaptersDir_ReturnsTrue()
    {
        var dir = NewDir();
        Directory.CreateDirectory(dir);
        Directory.CreateDirectory(Path.Combine(dir, "adapters"));
        try
        {
            var wizard = new FirstRunWizard(dir);
            Assert.True(wizard.IsFirstRun());
        }
        finally { Cove.Testing.TestDirectory.Delete(dir); }
    }

    [Fact]
    public void IsFirstRun_WithAdapters_ReturnsFalse()
    {
        var dir = NewDir();
        var adaptersDir = Path.Combine(dir, "adapters");
        Directory.CreateDirectory(adaptersDir);
        Directory.CreateDirectory(Path.Combine(adaptersDir, "claude-code"));
        try
        {
            var wizard = new FirstRunWizard(dir);
            Assert.False(wizard.IsFirstRun());
        }
        finally { Cove.Testing.TestDirectory.Delete(dir); }
    }

    [Fact]
    public void IsFirstRun_MarkedComplete_ReturnsFalse()
    {
        var dir = NewDir();
        Directory.CreateDirectory(dir);
        try
        {
            var wizard = new FirstRunWizard(dir);
            Assert.True(wizard.IsFirstRun());
            wizard.MarkComplete();
            Assert.False(wizard.IsFirstRun());
        }
        finally { Cove.Testing.TestDirectory.Delete(dir); }
    }

    [Fact]
    public void GetRecommendedAdapters_ReturnsDefaults()
    {
        var dir = NewDir();
        Directory.CreateDirectory(dir);
        try
        {
            var wizard = new FirstRunWizard(dir);
            var recommended = wizard.GetRecommendedAdapters();
            Assert.NotEmpty(recommended);
            Assert.Contains(recommended, a => a.Name == "claude-code");
        }
        finally { Cove.Testing.TestDirectory.Delete(dir); }
    }
}
