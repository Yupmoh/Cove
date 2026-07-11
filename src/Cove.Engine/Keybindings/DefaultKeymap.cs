namespace Cove.Engine.Keybindings;

public static class DefaultKeymap
{
    public static void RegisterAll(KeybindingEngine engine)
    {
        RegisterShoreShortcuts(engine);
        RegisterBayShortcuts(engine);
        RegisterViewShortcuts(engine);
        RegisterToolNookShortcuts(engine);
        RegisterNookShortcuts(engine);
        RegisterTerminalShortcuts(engine);
        RegisterNookMosaicShortcuts(engine);
    }

    private static void RegisterShoreShortcuts(KeybindingEngine engine)
    {
        engine.RegisterDefault("cmd+t", "app-command", "shore.new", "New shore");
        engine.RegisterDefault("cmd+w", "app-command", "nook.close", "Close nook/shore");
        engine.RegisterDefault("cmd+shift+w", "app-command", "shore.close", "Close shore");
        engine.RegisterDefault("ctrl+tab", "app-command", "shore.next", "Next shore");
        engine.RegisterDefault("ctrl+shift+tab", "app-command", "shore.prev", "Previous shore");
        engine.RegisterDefault("cmd+shift+t", "app-command", "shore.omni-jump", "Omni jump to shore");
        engine.RegisterDefault("cmd+shift+p", "app-command", "shore.pin", "Pin shore");
    }

    private static void RegisterBayShortcuts(KeybindingEngine engine)
    {
        engine.RegisterDefault("cmd+shift+n", "app-command", "bay.create", "Create bay");
        engine.RegisterDefault("cmd+1", "app-command", "bay.switch-1", "Switch to bay 1");
        engine.RegisterDefault("cmd+2", "app-command", "bay.switch-2", "Switch to bay 2");
        engine.RegisterDefault("cmd+3", "app-command", "bay.switch-3", "Switch to bay 3");
        engine.RegisterDefault("cmd+4", "app-command", "bay.switch-4", "Switch to bay 4");
        engine.RegisterDefault("cmd+5", "app-command", "bay.switch-5", "Switch to bay 5");
        engine.RegisterDefault("cmd+6", "app-command", "bay.switch-6", "Switch to bay 6");
        engine.RegisterDefault("cmd+7", "app-command", "bay.switch-7", "Switch to bay 7");
        engine.RegisterDefault("cmd+8", "app-command", "bay.switch-8", "Switch to bay 8");
        engine.RegisterDefault("cmd+9", "app-command", "bay.switch-9", "Switch to bay 9");
    }

    private static void RegisterViewShortcuts(KeybindingEngine engine)
    {
        engine.RegisterDefault("cmd+b", "app-command", "view.toggle-sidebar", "Toggle sidebar");
        engine.RegisterDefault("cmd+shift+b", "app-command", "view.toggle-toolbar", "Toggle toolbar");
        engine.RegisterDefault("cmd+shift+`", "app-command", "view.zen-mode", "Toggle zen mode");
        engine.RegisterDefault("cmd+=", "app-command", "view.zoom-in", "Zoom in");
        engine.RegisterDefault("cmd+-", "app-command", "view.zoom-out", "Zoom out");
        engine.RegisterDefault("cmd+0", "app-command", "view.zoom-reset", "Reset zoom");
    }

    private static void RegisterToolNookShortcuts(KeybindingEngine engine)
    {
        engine.RegisterDefault("cmd+shift+g", "app-command", "tool.git", "Open git nook");
        engine.RegisterDefault("cmd+shift+f", "app-command", "tool.search", "Open search nook");
        engine.RegisterDefault("cmd+shift+b", "app-command", "tool.browser", "Open browser nook");
        engine.RegisterDefault("cmd+shift+k", "app-command", "tool.tasks", "Open tasks nook");
        engine.RegisterDefault("cmd+shift+l", "app-command", "tool.library", "Open library");
        engine.RegisterDefault("cmd+shift+p", "app-command", "tool.palette", "Open command palette");
        engine.RegisterDefault("cmd+l", "app-command", "tool.launcher", "Open launcher");
    }

    private static void RegisterNookShortcuts(KeybindingEngine engine)
    {
        engine.RegisterDefault("cmd+d", "app-command", "nook.split-right", "Split right");
        engine.RegisterDefault("cmd+shift+d", "app-command", "nook.split-down", "Split down");
        engine.RegisterDefault("cmd+[", "app-command", "nook.focus-prev", "Focus previous nook");
        engine.RegisterDefault("cmd+]", "app-command", "nook.focus-next", "Focus next nook");
        engine.RegisterDefault("cmd+f", "app-command", "nook.find", "Find in nook");
        engine.RegisterDefault("cmd+shift+up", "app-command", "nook.scroll-top", "Scroll to top");
        engine.RegisterDefault("cmd+shift+down", "app-command", "nook.scroll-bottom", "Scroll to bottom");
    }

    private static void RegisterTerminalShortcuts(KeybindingEngine engine)
    {
        engine.RegisterDefault("cmd+c", "app-command", "terminal.copy-or-sigint", "Copy or SIGINT");
        engine.RegisterDefault("cmd+a", "app-command", "terminal.select-all", "Select all");
        engine.RegisterDefault("cmd+v", "app-command", "terminal.paste", "Paste");
        engine.RegisterDefault("shift+enter", "send-text", "\n", "Shift+Enter newline");
    }

    private static void RegisterNookMosaicShortcuts(KeybindingEngine engine)
    {
        engine.RegisterDefault("cmd+enter", "app-command", "nook.maximize", "Maximize/zoom nook");
        engine.RegisterDefault("cmd+shift+enter", "app-command", "nook.popout", "Popout nook");
    }
}
