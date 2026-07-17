# Configuration Reference

All configuration keys for Cove, grouped by settings tab.

## appearance

| Key | Label | Type | Control | Description |
|-----|-------|------|---------|-------------|
| `appearance` | Appearance | object | section |  |
| `appearance.accent` | Accent Override | string | text |  |
| `appearance.iconSet` | Icon Set | string | select |  |
| `appearance.layoutGap` | Layout Gap | int | number |  |
| `appearance.nookLight` | Nook Light | bool | toggle |  |
| `appearance.uiScale` | UI Scale | double | number |  |
| `appearance.wallpaper` | Wallpaper | string | text |  |
| `theme` | Theme | string | select |  |

## bay

| Key | Label | Type | Control | Description |
|-----|-------|------|---------|-------------|
| `session` | Sessions | object | section |  |
| `session.restoreOnLaunch` | Restore On Launch | bool | toggle |  |
| `worktree` | Worktree | object | section |  |
| `worktree.defaultLocationPattern` | Default Location Pattern | string | text |  |

## diagnostics

| Key | Label | Type | Control | Description |
|-----|-------|------|---------|-------------|
| `diagnostics` | Diagnostics | object | section |  |
| `diagnostics.captureMemoryStats` | Capture Memory Stats | bool | toggle |  |
| `diagnostics.captureTerminalStats` | Capture Terminal Stats | bool | toggle |  |
| `diagnostics.enabled` | Diagnostics Enabled | bool | toggle |  |
| `diagnostics.flushIntervalMs` | Flush Interval | int | number |  |

## keyboard

| Key | Label | Type | Control | Description |
|-----|-------|------|---------|-------------|
| `keybindings` | Keybindings | object | section |  |
| `keybindings.bindings` | Keybindings | object | text |  |

## terminal

| Key | Label | Type | Control | Description |
|-----|-------|------|---------|-------------|
| `markdownEditor` | Markdown Editor | object | section |  |
| `markdown_editor.bookView` | Book View | bool | toggle |  |
| `markdown_editor.bookViewMargin` | Book View Margin | string | text |  |
| `markdown_editor.bookViewWidth` | Book View Width | string | text |  |
| `markdown_editor.defaultFont` | Default Font | string | text |  |
| `markdown_editor.defaultViewMode` | Default View Mode | string | select |  |
| `markdown_editor.fontSize` | Font Size | int | number |  |
| `markdown_editor.textAlign` | Text Align | string | select |  |
| `terminal` | Terminal | object | section |  |
| `terminal.backgroundOpacity` | Background Opacity | double | number |  |
| `terminal.cursorBlink` | Cursor Blink | bool | toggle |  |
| `terminal.cursorStyle` | Cursor Style | string | select |  |
| `terminal.fontFamily` | Font Family | string | text |  |
| `terminal.fontSize` | Font Size | int | number |  |
| `terminal.letterSpacing` | Letter Spacing | double | number |  |
| `terminal.lineHeight` | Line Height | double | number |  |
| `terminal.padding` | Padding | int | number |  |
| `terminal.scrollbackLines` | Scrollback Lines | int | number |  |

## tools

| Key | Label | Type | Control | Description |
|-----|-------|------|---------|-------------|
| `lsp` | Language Servers | object | section |  |
| `lsp.servers` | Servers | object | text |  |

## updates

| Key | Label | Type | Control | Description |
|-----|-------|------|---------|-------------|
| `updates` | Updates | object | section |  |
| `updates.checkOnLaunch` | Check On Launch | bool | toggle |  |

