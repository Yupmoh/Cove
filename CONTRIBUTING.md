# Contributing to Cove

Thanks for your interest in Cove.

## Building

Cove targets the .NET SDK pinned in `global.json`. Build the whole solution:

```
dotnet build Cove.slnx
```

Run the tests:

```
dotnet test Cove.slnx
```

## Branches and pull requests

- Work on a topic branch and open a pull request against `main`.
- Keep each pull request focused. Small, reviewable changes merge fastest.
- Every change ships with its tests. New behavior without tests will be asked
  to add them.

## Commit style

Commit titles are type-prefixed, imperative, and describe the change:

```
feat: add framed control protocol codec
fix: reclaim stale control socket after crash
chore: bump SDK pin
```

Use one of: `feat`, `fix`, `chore`, `docs`, `refactor`, `style`, `test`, `ci`,
`perf`, `build`. No parenthetical scopes.

## Code style

Formatting is enforced by `dotnet format`. Before pushing:

```
dotnet format Cove.slnx --verify-no-changes
```

Warnings are treated as errors. A green build has zero warnings.
