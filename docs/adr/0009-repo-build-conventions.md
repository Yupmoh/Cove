# 0009 — Repo and build conventions mirror Ryn

Status: Accepted

## Context

Cove should adopt proven, low-surprise open-source conventions rather than invent its own, and keep planning artifacts out of the product repo.

## Decision

Use the canonical layout (`src/ tests/ samples/ examples/ benchmarks/ docs/`), a single `Cove.slnx` solution, a pinned SDK via `global.json`, `Directory.Build.props`/`.targets` carrying MinVer (tag prefix `v`, minimum major-minor floor, releases cut by tagging), and `Directory.Packages.props` for central package management (no versions in any csproj). Ship `.editorconfig`, `LICENSE`, `NOTICE`, `THIRD-PARTY-NOTICES.md`, a README with owner-first credit, and `CONTRIBUTING`/`CODE_OF_CONDUCT`/`SECURITY`. Zero AI attribution anywhere. The planning and research trees are gitignored and never staged.

## Consequences

- Tag-driven, reproducible versioning.
- One place for package versions.
- Planning artifacts never leak into the repo history.

## Inventory IDs

- PL-76 — Signed/notarized cross-platform packaging (build/release foundation)

## Related

The build gates enforce 0007 (AOT/zero-reflection). Licensing in 0010.
