# Contributing to Cove

Cove is a free, open-source, AI-native terminal bay. We welcome contributions.

## Getting started

1. Clone the repository.
2. Install the .NET 10 SDK.
3. Run `dotnet build Cove.slnx` from the repo root.
4. Run `dotnet test Cove.slnx` to verify the full suite.

## Architecture overview

Cove is a **headless-first** terminal bay: a `cove daemon` engine owns PTYs, sessions, and SQLite state; the Ryn GUI, the TUI, and the `cove` CLI are all thin clients of one `cove://` control socket.

Read `docs/architecture.md` for the subsystem map, and `AGENTS.md` for executor rules if you are working from a plan packet.

## Engineering standards

- **Native-AOT safe**: zero reflection. Source-generate all JSON serialization (`JsonSerializerContext`), regex (`[GeneratedRegex]`), and logging (ZLogger).
- **Zero warnings**: the build treats warnings as errors. No CS8019/CS8933/IL2026/IL3050.
- **Tests first**: write the test, watch it fail, then implement.
- **No silent swallows**: log a Warning before any early return on missing/invalid data. `catch {}` is forbidden.
- **Self-documenting names**: no comments (`//`, `///`, `TODO`, `FIXME`).
- **Central Package Management**: package versions live only in `Directory.Packages.props`.
- **Vertical slices**: keep handlers + state + tests together. No horizontal `Services/` layers.

## Commit conventions

- Use a type-prefixed title: `feat:`, `fix:`, `chore:`, `docs:`, `test:`, `refactor:`, `style:`.
- After the title and one blank line, include one or more `- ` bullets explaining specific changes or why they were made.
- No scope, prose paragraphs, or trailers.
- No AI attribution in commit messages or committed files.

## Pull requests

- Branch from `main`, target `main`.
- One logical change per PR.
- Include tests for new behavior.
- Ensure `dotnet build Cove.slnx` and `dotnet test Cove.slnx` pass.

## License

By contributing, you agree your contributions are licensed under Apache-2.0.
