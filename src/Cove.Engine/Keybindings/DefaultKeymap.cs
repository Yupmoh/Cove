namespace Cove.Engine.Keybindings;

public static class DefaultKeymap
{
    public static void RegisterAll(KeybindingEngine engine)
    {
        RegisterRoomShortcuts(engine);
        RegisterWorkspaceShortcuts(engine);
        RegisterViewShortcuts(engine);
        RegisterToolPaneShortcuts(engine);
        RegisterPaneShortcuts(engine);
        RegisterTerminalShortcuts(engine);
        RegisterPaneMosaicShortcuts(engine);
    }

    private static void RegisterRoomShortcuts(KeybindingEngine engine)
    {
        engine.RegisterDefault("cmd+t", "app-command", "room.new", "New room");
        engine.RegisterDefault("cmd+w", "app-command", "pane.close", "Close pane/room");
        engine.RegisterDefault("cmd+shift+w", "app-command", "room.close", "Close room");
        engine.RegisterDefault("ctrl+tab", "app-command", "room.next", "Next room");
        engine.RegisterDefault("ctrl+shift+tab", "app-command", "room.prev", "Previous room");
        engine.RegisterDefault("cmd+shift+t", "app-command", "room.omni-jump", "Omni jump to room");
        engine.RegisterDefault("cmd+shift+p", "app-command", "room.pin", "Pin room");
    }

    private static void RegisterWorkspaceShortcuts(KeybindingEngine engine)
    {
        engine.RegisterDefault("cmd+shift+n", "app-command", "workspace.create", "Create workspace");
        engine.RegisterDefault("cmd+1", "app-command", "workspace.switch-1", "Switch to workspace 1");
        engine.RegisterDefault("cmd+2", "app-command", "workspace.switch-2", "Switch to workspace 2");
        engine.RegisterDefault("cmd+3", "app-command", "workspace.switch-3", "Switch to workspace 3");
        engine.RegisterDefault("cmd+4", "app-command", "workspace.switch-4", "Switch to workspace 4");
        engine.RegisterDefault("cmd+5", "app-command", "workspace.switch-5", "Switch to workspace 5");
        engine.RegisterDefault("cmd+6", "app-command", "workspace.switch-6", "Switch to workspace 6");
        engine.RegisterDefault("cmd+7", "app-command", "workspace.switch-7", "Switch to workspace 7");
        engine.RegisterDefault("cmd+8", "app-command", "workspace.switch-8", "Switch to workspace 8");
        engine.RegisterDefault("cmd+9", "app-command", "workspace.switch-9", "Switch to workspace 9");
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

    private static void RegisterToolPaneShortcuts(KeybindingEngine engine)
    {
        engine.RegisterDefault("cmd+shift+g", "app-command", "tool.git", "Open git pane");
        engine.RegisterDefault("cmd+shift+f", "app-command", "tool.search", "Open search pane");
        engine.RegisterDefault("cmd+shift+b", "app-command", "tool.browser", "Open browser pane");
        engine.RegisterDefault("cmd+shift+k", "app-command", "tool.tasks", "Open tasks pane");
        engine.RegisterDefault("cmd+shift+l", "app-command", "tool.library", "Open library");
        engine.RegisterDefault("cmd+shift+p", "app-command", "tool.palette", "Open command palette");
        engine.RegisterDefault("cmd+l", "app-command", "tool.launcher", "Open launcher");
    }

    private static void RegisterPaneShortcuts(KeybindingEngine engine)
    {
        engine.RegisterDefault("cmd+d", "app-command", "pane.split-right", "Split right");
        engine.RegisterDefault("cmd+shift+d", "app-command", "pane.split-down", "Split down");
        engine.RegisterDefault("cmd+[", "app-command", "pane.focus-prev", "Focus previous pane");
        engine.RegisterDefault("cmd+]", "app-command", "pane.focus-next", "Focus next pane");
        engine.RegisterDefault("cmd+f", "app-command", "pane.find", "Find in pane");
        engine.RegisterDefault("cmd+shift+up", "app-command", "pane.scroll-top", "Scroll to top");
        engine.RegisterDefault("cmd+shift+down", "app-command", "pane.scroll-bottom", "Scroll to bottom");
    }

    private static void RegisterTerminalShortcuts(KeybindingEngine engine)
    {
        engine.RegisterDefault("cmd+c", "app-command", "terminal.copy-or-sigint", "Copy or SIGINT");
        engine.RegisterDefault("cmd+a", "app-command", "terminal.select-all", "Select all");
        engine.RegisterDefault("cmd+v", "app-command", "terminal.paste", "Paste");
        engine.RegisterDefault("shift+enter", "send-text", "\n", "Shift+Enter newline");
    }

    private static void RegisterPaneMosaicShortcuts(KeybindingEngine engine)
    {
        engine.RegisterDefault("cmd+enter", "app-command", "pane.maximize", "Maximize/zoom pane");
        engine.RegisterDefault("cmd+shift+enter", "app-command", "pane.popout", "Popout pane");
    }
}
