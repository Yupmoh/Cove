using Cove.Engine.Settings;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class SettingsStoreTests
{
    private static string NewDir() => System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cove-settings-" + System.Guid.NewGuid().ToString("N"));

    [Fact]
    public void Get_NonexistentKey_ReturnsDefault()
    {
        var dir = NewDir();
        System.IO.Directory.CreateDirectory(dir);
        try
        {
            var store = new SettingsStore(dir);
            Assert.Equal("default", store.Get("nonexistent", "default"));
        }
        finally { Cove.Testing.TestDirectory.Delete(dir); }
    }

    [Fact]
    public void Set_PersistsValue()
    {
        var dir = NewDir();
        System.IO.Directory.CreateDirectory(dir);
        try
        {
            var store = new SettingsStore(dir);
            store.Set("terminal.fontSize", "14");
            Assert.Equal("14", store.Get("terminal.fontSize", ""));
        }
        finally { Cove.Testing.TestDirectory.Delete(dir); }
    }

    [Fact]
    public void Set_SurvivesReload()
    {
        var dir = NewDir();
        System.IO.Directory.CreateDirectory(dir);
        try
        {
            var store1 = new SettingsStore(dir);
            store1.Set("theme", "dark");
            var store2 = new SettingsStore(dir);
            Assert.Equal("dark", store2.Get("theme", ""));
        }
        finally { Cove.Testing.TestDirectory.Delete(dir); }
    }

    [Fact]
    public void GetTyped_Int_ParsesValue()
    {
        var dir = NewDir();
        System.IO.Directory.CreateDirectory(dir);
        try
        {
            var store = new SettingsStore(dir);
            store.Set("terminal.fontSize", "14");
            Assert.Equal(14, store.GetInt("terminal.fontSize", 12));
        }
        finally { Cove.Testing.TestDirectory.Delete(dir); }
    }

    [Fact]
    public void GetTyped_Bool_ParsesValue()
    {
        var dir = NewDir();
        System.IO.Directory.CreateDirectory(dir);
        try
        {
            var store = new SettingsStore(dir);
            store.Set("terminal.showBell", "true");
            Assert.True(store.GetBool("terminal.showBell", false));
        }
        finally { Cove.Testing.TestDirectory.Delete(dir); }
    }

    [Fact]
    public void Delete_RemovesKey()
    {
        var dir = NewDir();
        System.IO.Directory.CreateDirectory(dir);
        try
        {
            var store = new SettingsStore(dir);
            store.Set("temp", "val");
            store.Delete("temp");
            Assert.False(store.HasKey("temp"));
        }
        finally { Cove.Testing.TestDirectory.Delete(dir); }
    }

    [Fact]
    public void GetAll_ReturnsAllKeys()
    {
        var dir = NewDir();
        System.IO.Directory.CreateDirectory(dir);
        try
        {
            var store = new SettingsStore(dir);
            store.Set("a", "1");
            store.Set("b", "2");
            var all = store.GetAll();
            Assert.Equal(2, all.Count);
            Assert.Equal("1", all["a"]);
            Assert.Equal("2", all["b"]);
        }
        finally { Cove.Testing.TestDirectory.Delete(dir); }
    }
}

public sealed class ThemeStoreTests
{
    [Fact]
    public void GetDefault_ReturnsDarkTheme()
    {
        var theme = ThemeStore.GetDefault();
        Assert.Equal("dark", theme.Name);
        Assert.NotNull(theme.Background);
    }

    [Fact]
    public void GetBuiltin_ReturnsDarkAndLight()
    {
        var themes = ThemeStore.GetBuiltinThemes();
        Assert.NotEmpty(themes);
        Assert.Contains(themes, t => t.Name == "dark");
        Assert.Contains(themes, t => t.Name == "light");
    }
}

public sealed class KeybindingStoreTests
{
    private static string NewDir() => System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cove-keys-" + System.Guid.NewGuid().ToString("N"));

    [Fact]
    public void GetDefault_ReturnsBuiltinBinding()
    {
        var dir = NewDir();
        System.IO.Directory.CreateDirectory(dir);
        try
        {
            var store = new KeybindingStore(dir);
            Assert.Equal("cmd+t", store.Get("nook.newTerminal", "none"));
        }
        finally { Cove.Testing.TestDirectory.Delete(dir); }
    }

    [Fact]
    public void Set_OverridesDefault()
    {
        var dir = NewDir();
        System.IO.Directory.CreateDirectory(dir);
        try
        {
            var store = new KeybindingStore(dir);
            store.Set("nook.newTerminal", "ctrl+t");
            Assert.Equal("ctrl+t", store.Get("nook.newTerminal", "none"));
        }
        finally { Cove.Testing.TestDirectory.Delete(dir); }
    }
}
