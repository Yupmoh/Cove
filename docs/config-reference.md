# Configuration Reference

All configuration keys for Cove, grouped by settings tab.

## appearance

| Key | Label | Type | Control | Description |
|-----|-------|------|---------|-------------|
| `appearance` | Appearance | object | section | Appearance settings |
| `appearance.accent` | Accent Override | string | text | Override accent color hex (empty for theme default) |
| `appearance.iconSet` | Icon Set | string | select | Icon set style |
| `appearance.layoutGap` | Layout Gap | int | number | Gap between nooks in pixels |
| `appearance.nookLight` | Nook Light | bool | toggle | Lighten inactive nooks |
| `appearance.uiScale` | UI Scale | double | number | Interface scale multiplier (0.8-1.5) |
| `appearance.wallpaper` | Wallpaper | string | text | Wallpaper image path or URL |
| `theme` | Theme | string | select | Active theme name |

## bay

| Key | Label | Type | Control | Description |
|-----|-------|------|---------|-------------|
| `session` | Sessions | object | section | Session restoration settings |
| `session.restoreOnLaunch` | Restore On Launch | bool | toggle | Respawn terminal and agent nooks when the daemon restarts |
| `worktree` | Worktree | object | section | Worktree settings |
| `worktree.defaultLocationPattern` | Default Location Pattern | string | text | Worktree default location pattern |

## diagnostics

| Key | Label | Type | Control | Description |
|-----|-------|------|---------|-------------|
| `diagnostics` | Diagnostics | object | section | Diagnostics settings |
| `diagnostics.captureMemoryStats` | Capture Memory Stats | bool | toggle | Capture memory statistics |
| `diagnostics.captureTerminalStats` | Capture Terminal Stats | bool | toggle | Capture terminal statistics |
| `diagnostics.enabled` | Diagnostics Enabled | bool | toggle | Enable diagnostics |
| `diagnostics.flushIntervalMs` | Flush Interval | int | number | Flush interval in milliseconds |

## keyboard

| Key | Label | Type | Control | Description |
|-----|-------|------|---------|-------------|
| `keybindings` | Keybindings | object | section | Keybinding overrides |
| `keybindings.bindings` | Keybindings | object | text | Custom keybinding overrides |

## terminal

| Key | Label | Type | Control | Description |
|-----|-------|------|---------|-------------|
| `markdownEditor` | Markdown Editor | object | section | Markdown editor settings |
| `markdown_editor.bookView` | Book View | bool | toggle | Enable book view |
| `markdown_editor.bookViewMargin` | Book View Margin | string | text | Book view margin |
| `markdown_editor.bookViewWidth` | Book View Width | string | text | Book view width |
| `markdown_editor.defaultFont` | Default Font | string | text | Default markdown editor font |
| `markdown_editor.defaultViewMode` | Default View Mode | string | select | Default view mode |
| `markdown_editor.fontSize` | Font Size | int | number | Markdown editor font size |
| `markdown_editor.textAlign` | Text Align | string | select | Text alignment |
| `terminal` | Terminal | object | section | Terminal settings |
| `terminal.backgroundOpacity` | Background Opacity | double | number | Background opacity 0-1 |
| `terminal.cursorBlink` | Cursor Blink | bool | toggle | Cursor blink |
| `terminal.cursorStyle` | Cursor Style | string | select | Cursor style |
| `terminal.fontFamily` | Font Family | string | text | Terminal font family |
| `terminal.fontSize` | Font Size | int | number | Terminal font size in pixels |
| `terminal.letterSpacing` | Letter Spacing | double | number | Terminal letter spacing |
| `terminal.lineHeight` | Line Height | double | number | Terminal line height |
| `terminal.padding` | Padding | int | number | Terminal padding in pixels |
| `terminal.scrollbackLines` | Scrollback Lines | int | number | Scrollback line count |

## tools

| Key | Label | Type | Control | Description |
|-----|-------|------|---------|-------------|
| `lsp` | Language Servers | object | section | Registered language servers |
| `lsp.servers` | Servers | array | text | Language server entries ({languages, command, args}); entries override built-ins for the same language |

## updates

| Key | Label | Type | Control | Description |
|-----|-------|------|---------|-------------|
| `updates` | Updates | object | section | Update settings |
| `updates.checkOnLaunch` | Check On Launch | bool | toggle | Check for updates on launch |

