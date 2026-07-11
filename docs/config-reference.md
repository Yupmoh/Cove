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

## audio

| Key | Label | Type | Control | Description |
|-----|-------|------|---------|-------------|
| `pushToTalk` | Push To Talk | object | section |  |
| `pushToTalk.enabled` | Push To Talk Enabled | bool | toggle |  |
| `pushToTalk.isModifier` | Is Modifier | bool | toggle |  |
| `pushToTalk.keyCode` | Key Code | int | number |  |
| `pushToTalk.label` | Label | string | text |  |
| `pushToTalk.requiredFlags` | Required Flags | int | number |  |
| `speech` | Speech | object | section |  |
| `speech.gain` | Speech Gain | double | number |  |
| `speech.inputDevice` | Input Device | string | text |  |
| `speech.onDeviceRecognition` | On-Device Recognition | bool | toggle |  |

## diagnostics

| Key | Label | Type | Control | Description |
|-----|-------|------|---------|-------------|
| `diagnostics` | Diagnostics | object | section |  |
| `diagnostics.captureIpcTimings` | Capture IPC Timings | bool | toggle |  |
| `diagnostics.captureLongTasks` | Capture Long Tasks | bool | toggle |  |
| `diagnostics.captureMemoryStats` | Capture Memory Stats | bool | toggle |  |
| `diagnostics.captureRenderStats` | Capture Render Stats | bool | toggle |  |
| `diagnostics.captureTerminalStats` | Capture Terminal Stats | bool | toggle |  |
| `diagnostics.enabled` | Diagnostics Enabled | bool | toggle |  |
| `diagnostics.flushIntervalMs` | Flush Interval | int | number |  |

## keyboard

| Key | Label | Type | Control | Description |
|-----|-------|------|---------|-------------|
| `keybindings` | Keybindings | object | section |  |
| `keybindings.bindings` | Keybindings | object | text |  |

## privacy

| Key | Label | Type | Control | Description |
|-----|-------|------|---------|-------------|
| `remoteConfig` | Remote Config | object | section |  |
| `remoteConfig.dismissedBannerIds` | Dismissed Banners | object | text |  |
| `telemetry` | Telemetry | object | section |  |
| `telemetry.analyticsOptIn` | Analytics Opt-In | bool | toggle |  |
| `telemetry.coreTelemetryDisclosed` | Core Telemetry Disclosed | bool | toggle |  |
| `telemetry.enabled` | Telemetry Enabled | bool | toggle |  |

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
| `markdown_editor.imagePasteFolder` | Image Paste Folder | string | text |  |
| `markdown_editor.textAlign` | Text Align | string | select |  |
| `terminal` | Terminal | object | section |  |
| `terminal.backgroundOpacity` | Background Opacity | double | number |  |
| `terminal.cursorBlink` | Cursor Blink | bool | toggle |  |
| `terminal.cursorStyle` | Cursor Style | string | select |  |
| `terminal.fontFamily` | Font Family | string | text |  |
| `terminal.fontLigatures` | Font Ligatures | bool | toggle |  |
| `terminal.fontSize` | Font Size | int | number |  |
| `terminal.letterSpacing` | Letter Spacing | double | number |  |
| `terminal.lineHeight` | Line Height | double | number |  |
| `terminal.padding` | Padding | int | number |  |
| `terminal.scrollbackLines` | Scrollback Lines | int | number |  |

## tools

| Key | Label | Type | Control | Description |
|-----|-------|------|---------|-------------|
| `adapterCommands` | Adapter Commands | object | section |  |
| `adapterCommands.commands` | Adapter Commands | object | text |  |
| `lsp` | Language Servers | object | section |  |
| `lsp.servers` | Servers | object | text |  |
| `lspServers` | LSP Servers | object | section |  |
| `lspServers.servers` | LSP Servers | object | text |  |

## updates

| Key | Label | Type | Control | Description |
|-----|-------|------|---------|-------------|
| `updates` | Updates | object | section |  |
| `updates.autoInstall` | Auto Install | bool | toggle |  |
| `updates.autoUpdateAdapters` | Auto Update Adapters | bool | toggle |  |
| `updates.channel` | Channel | string | select |  |
| `updates.checkIntervalHours` | Check Interval | int | number |  |
| `updates.checkOnLaunch` | Check On Launch | bool | toggle |  |

## bay

| Key | Label | Type | Control | Description |
|-----|-------|------|---------|-------------|
| `session` | Sessions | object | section |  |
| `session.restoreOnLaunch` | Restore On Launch | bool | toggle |  |
| `worktree` | Worktree | object | section |  |
| `worktree.defaultLocationPattern` | Default Location Pattern | string | text |  |
| `worktree.postCreateCommands` | Post-Create Commands | object | text |  |

