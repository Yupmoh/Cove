# Cove PTY harness

Automated gates run with `dotnet test`. Two checks are manual and are not part of the
automated gate because their output is not byte-deterministic.

## Interactive shell smoke (M0 T7 human done-criterion)

Confirms a real interactive shell works through the engine PTY host.

1. Build the harness: `dotnet build tests/Cove.Pty.Harness`.
2. From the harness output directory, run a small program (or the interactive test tagged
   `Category=PtyInteractive`, if present) that spawns `/bin/zsh`, then confirm by hand that
   `vim` opens and redraws, 256-color output shows color, and `Ctrl+Z` then `fg` suspends
   and resumes a job. Resize the terminal mid-session and confirm the shell reflows.

## Missing-shim loud-fail

Confirms that when `libcove_pty` is absent the host fails loud, never silently unsafe.

1. Build the harness: `dotnet build tests/Cove.Pty.Harness`.
2. Locate the built shim under `tests/Cove.Pty.Harness/bin/Debug/net10.0/libcove_pty.dylib`
   (`.so` on Linux) and move it aside: `mv <path>/libcove_pty.dylib <path>/libcove_pty.dylib.bak`.
3. Run `dotnet test tests/Cove.Pty.Harness --filter Category=PtyDrain --no-build`.
   Expected: the run fails and the output contains `PlatformNotSupportedException` with the
   message about the missing shim (loud fail, not a hang or a silent pass).
4. Restore the shim: `mv <path>/libcove_pty.dylib.bak <path>/libcove_pty.dylib`.
