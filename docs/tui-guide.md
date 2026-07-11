# Cove TUI — Terminal User Interface

Cove ships a full terminal-mode client (`cove attach`) that drives the same headless daemon as the GUI. This document covers the TUI architecture, keymap, SSH usage, and terminal degradation matrix.

## Architecture

The TUI is a **second live client** of the `cove://` daemon — not a separate application. It connects via the same control protocol (M9-P02 ControlClient) and renders the same bay/shore/nook mosaic as the GUI.

| Component | Role |
|---|---|
| ControlClient | Connects to daemon over `cove://` (UDS/named-pipe), invokes verbs, subscribes to events, streams PTY bytes |
| TerminalCapabilities | Detects terminal color/glyph/unicode support from TERM/COLORTERM/NO_COLOR/TERM_PROGRAM |
| Cell compositor | Double-buffered grid with damage tracking (M9-P03, human-judgment) |
| VT emulator | xterm-subset parser for per-nook grid + scrollback (M9-P04, human-judgment) |
| Spectre backend | Custom IAnsiConsole compositing into cell regions (M9-P05, human-judgment) |
| Input layer | Raw mode, CSI-u/kitty decode, SGR-1006 mouse (M9-P06, human-judgment) |

## Keymap reference

The TUI uses the same default keymap as the GUI (MC-P13), adapted for terminal constraints. Prefix-based commands use a configurable prefix (default: `Ctrl+A`, tmux-style).

| Chord | Action |
|---|---|
| `prefix+c` | New shore |
| `prefix+n` | New nook (split right) |
| `prefix+shift+n` | New nook (split down) |
| `prefix+[` | Focus previous nook |
| `prefix+]` | Focus next nook |
| `prefix+d` | Detach from session |
| `prefix+l` | Open launcher |
| `prefix+:` | Open command palette |
| `prefix+s` | Switch bay |
| `prefix+w` | Close nook |
| `prefix+x` | Maximize/zoom nook |

## SSH guide

Cove's headless-first design means the daemon runs on a remote machine and the TUI attaches over SSH:

```bash
# On the remote machine (start the daemon)
cove daemon run &

# From your local machine (attach over SSH)
ssh remote-host cove attach
```

### Detach/reattach

- **Detach**: `prefix+d` (or close the SSH session)
- **Reattach**: `ssh remote-host cove attach` (resumes the same session)
- **List sessions**: `cove session list`

The daemon owns all PTYs and scrollback, so detaching never kills running processes. Reattaching restores the full mosaic + scrollback via the ring buffer replay.

## Terminal degradation matrix

Cove detects terminal capabilities and degrades gracefully:

| Terminal | Color | TrueColor | Unicode | Emoji | Glyph set |
|---|---|---|---|---|---|
| iTerm2 | 24-bit | ✓ | ✓ | ✓ | nerd-emoji |
| Ghostty | 24-bit | ✓ | ✓ | ✓ | nerd-emoji |
| WezTerm | 24-bit | ✓ | ✓ | ✓ | nerd-emoji |
| kitty | 24-bit | ✓ | ✓ | ✓ | nerd-emoji |
| Terminal.app | 16 | ✗ | ✓ | ✗ | nerd |
| Windows Terminal | 24-bit | ✓ | ✓ | ✓ | nerd-emoji |
| Alacritty | 24-bit | ✓ | ✓ | ✗ | nerd |
| tmux | 256 | ✗ | ✓ | ✗ | nerd |
| Linux console | 0 | ✗ | ✗ | ✗ | ascii |
| dumb | 0 | ✗ | ✗ | ✗ | ascii |

### NO_COLOR

When the `NO_COLOR` environment variable is set (any value), Cove disables all color output. Glyphs degrade to the terminal's unicode support level.

## GUI-only degradation stubs

Browser, editor, canvas, and media nooks render honest placeholders in TUI mode — a single-line description + "open in GUI" hint, never a fake rendering.

## Status

The TUI is a BEYOND feature (dual frontend). The cell compositor, VT emulator, Spectre backend, and input layer (M9-P01/P03/P04/P05/P06) are human-judgment infrastructure requiring owner design calls and performance tuning. The ControlClient transport (M9-P02) and terminal capability detection (M9-P09) are built and tested.
