# Cove

An open-source, native-webview terminal bay.

Created & maintained by Moh — https://github.com/Yupmoh

## Status

Early development, evolving in the open. `v0.1.1` is the first tagged release —
the M0 foundations milestone (engine, control channel, lossless PTY, persistence,
source-gen dispatch). Interfaces and layout may still change.

## What Cove is

Cove is a fast, quiet terminal bay: a headless engine owns your shells,
sessions, and state, while thin GUI and terminal clients attach to it over a
local control channel. Cove aims to stay out of your way — no engagement
nudges, no forced ceremonies — and to treat latency and losslessness as
features.

## Download & run

Prebuilt `cove` command-line binaries for macOS (arm64), Linux (x64), and
Windows (x64) are attached to every release on the
[Releases](https://github.com/Yupmoh/Cove/releases) page. Download the archive
for your platform, unzip it, and run the `cove` binary next to its bundled
libraries:

```
unzip cove-osx-arm64.zip
./cove version
./cove daemon run --channel dev
```

The binaries are not yet code-signed. On macOS, clear the download quarantine
first: `xattr -dr com.apple.quarantine cove`.

## Desktop app (macOS)

macOS releases include `cove-macos-app.zip` — a self-contained `Cove.app`
bundling the GUI and its engine. Unzip and launch it:

```
unzip cove-macos-app.zip
xattr -dr com.apple.quarantine Cove.app
open Cove.app
```

The `xattr` step (or right-click → Open once) clears Gatekeeper for the
unsigned build. Cove starts its own engine and opens a live terminal.

## Run the desktop GUI (from source)

The graphical bay is a native-webview terminal that renders a real shell
losslessly. macOS ships the prebuilt `Cove.app` above; on Linux and Windows, or
for development, run it from source. Start the engine, then the GUI:

```
dotnet run --project src/Cove.Cli -- daemon run --channel dev
dotnet run --project src/Cove.Gui
```

## Platform support

The engine, control channel, and persistence run on macOS, Linux, and Windows.
Interactive PTY nooks are supported on macOS and Linux; the Windows PTY backend
is not yet implemented.

## License

Cove is licensed under the Apache License, Version 2.0. See `LICENSE` and
`NOTICE`. Third-party components and their licenses are listed in
`THIRD-PARTY-NOTICES.md`.

## Trademarks

"Cove" and the Cove wordmark are used by the project maintainer. The Apache-2.0
license grants no trademark rights (see `LICENSE` section 6). Please do not use
the name in a way that implies endorsement by or affiliation with the project
without permission.
