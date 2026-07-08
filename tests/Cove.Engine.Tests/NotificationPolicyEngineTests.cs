using Cove.Engine.Notifications;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class NotificationPolicyEngineTests
{
    private static NotificationPolicyEngine NewEngine()
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"cove-notif-{System.Guid.NewGuid():N}");
        System.IO.Directory.CreateDirectory(dir);
        return new NotificationPolicyEngine(dir, NullLogger.Instance);
    }

    [Fact]
    public void Evaluate_NeedsInputWhileFocused_NoOsNotification()
    {
        var engine = NewEngine();
        var result = engine.Evaluate(new NotificationTrigger(
            NeedsInput: true, AppFocused: true, PaneId: "pane-1", BannerId: null));

        Assert.True(result.SuppressOsNotification);
        Assert.False(result.SuppressToast);
    }

    [Fact]
    public void Evaluate_NeedsInputWhileBackgrounded_FiresOsNotification()
    {
        var engine = NewEngine();
        var result = engine.Evaluate(new NotificationTrigger(
            NeedsInput: true, AppFocused: false, PaneId: "pane-1", BannerId: null));

        Assert.False(result.SuppressOsNotification);
    }

    [Fact]
    public void Evaluate_NoNeedsInput_NoOsNotification()
    {
        var engine = NewEngine();
        var result = engine.Evaluate(new NotificationTrigger(
            NeedsInput: false, AppFocused: false, PaneId: "pane-1", BannerId: null));

        Assert.True(result.SuppressOsNotification);
    }

    [Fact]
    public void Evaluate_OsNotificationTierDisabled_SuppressesOsNotification()
    {
        var engine = NewEngine();
        engine.SetTierEnabled(NotificationTier.OsNotification, false);

        var result = engine.Evaluate(new NotificationTrigger(
            NeedsInput: true, AppFocused: false, PaneId: "pane-1", BannerId: null));

        Assert.True(result.SuppressOsNotification);
    }

    [Fact]
    public void Evaluate_ToastTierDisabled_SuppressesToast()
    {
        var engine = NewEngine();
        engine.SetTierEnabled(NotificationTier.Toast, false);

        var result = engine.Evaluate(new NotificationTrigger(
            NeedsInput: true, AppFocused: true, PaneId: "pane-1", BannerId: null));

        Assert.True(result.SuppressToast);
    }

    [Fact]
    public void Evaluate_DifferentTiersToggleIndependently()
    {
        var engine = NewEngine();
        engine.SetTierEnabled(NotificationTier.OsNotification, false);
        Assert.False(engine.IsTierEnabled(NotificationTier.OsNotification));
        Assert.True(engine.IsTierEnabled(NotificationTier.Toast));
        Assert.True(engine.IsTierEnabled(NotificationTier.Ambient));
    }

    [Fact]
    public void DismissBanner_PermanentlyDismissed()
    {
        var engine = NewEngine();
        engine.DismissBanner("banner-1");

        Assert.True(engine.IsDismissed("banner-1"));
    }

    [Fact]
    public void Evaluate_DismissedBanner_NeverReappears()
    {
        var engine = NewEngine();
        engine.DismissBanner("banner-1");

        var result = engine.Evaluate(new NotificationTrigger(
            NeedsInput: false, AppFocused: true, PaneId: "pane-1", BannerId: "banner-1"));

        Assert.True(result.SuppressBanner);
    }

    [Fact]
    public void Evaluate_NonDismissedBanner_ShowsBanner()
    {
        var engine = NewEngine();

        var result = engine.Evaluate(new NotificationTrigger(
            NeedsInput: false, AppFocused: true, PaneId: "pane-1", BannerId: "banner-2"));

        Assert.False(result.SuppressBanner);
    }

    [Fact]
    public void DismissBanner_PersistsAcrossInstances()
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"cove-notif-{System.Guid.NewGuid():N}");
        System.IO.Directory.CreateDirectory(dir);

        var engine1 = new NotificationPolicyEngine(dir, NullLogger.Instance);
        engine1.DismissBanner("banner-1");

        var engine2 = new NotificationPolicyEngine(dir, NullLogger.Instance);
        Assert.True(engine2.IsDismissed("banner-1"));
    }

    [Fact]
    public void Evaluate_AlwaysFiresAmbientRegardlessOfFocus()
    {
        var engine = NewEngine();
        var result = engine.Evaluate(new NotificationTrigger(
            NeedsInput: false, AppFocused: true, PaneId: "pane-1", BannerId: null));

        Assert.False(result.SuppressAmbient);
    }

    [Fact]
    public void SetTierEnabled_PersistsAcrossInstances()
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"cove-notif-{System.Guid.NewGuid():N}");
        System.IO.Directory.CreateDirectory(dir);

        var engine1 = new NotificationPolicyEngine(dir, NullLogger.Instance);
        engine1.SetTierEnabled(NotificationTier.OsNotification, false);

        var engine2 = new NotificationPolicyEngine(dir, NullLogger.Instance);
        Assert.False(engine2.IsTierEnabled(NotificationTier.OsNotification));
    }

    [Fact]
    public void Evaluate_OsNotificationFiresOncePerPaneNeedsInput()
    {
        var engine = NewEngine();
        var trigger = new NotificationTrigger(
            NeedsInput: true, AppFocused: false, PaneId: "pane-1", BannerId: null);

        var result1 = engine.Evaluate(trigger);
        Assert.False(result1.SuppressOsNotification);

        var result2 = engine.Evaluate(trigger);
        Assert.True(result2.SuppressOsNotification);
    }

    [Fact]
    public void ClearNeedsInput_ResetsOsNotificationGate()
    {
        var engine = NewEngine();
        engine.Evaluate(new NotificationTrigger(
            NeedsInput: true, AppFocused: false, PaneId: "pane-1", BannerId: null));

        engine.ClearNeedsInput("pane-1");

        var result = engine.Evaluate(new NotificationTrigger(
            NeedsInput: true, AppFocused: false, PaneId: "pane-1", BannerId: null));
        Assert.False(result.SuppressOsNotification);
    }
}
