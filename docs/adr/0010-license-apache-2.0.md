# 0010 — License: Apache-2.0

Status: Accepted

## Context

The license shapes adoption, corporate contribution, and the third-party-client ecosystem that the daemon/client protocol exists to enable. The alternative considered was AGPL-3.0.

## Decision

License Cove under Apache-2.0. A permissive license maximizes adoption and corporate contribution (many organizations ban AGPL dependencies), mirrors the MIT atrium adapter SDK Cove interoperates with, and matches the permissive posture of the Ryn framework. AGPL's network-copyleft is largely inert for a local desktop app and would chill exactly the third-party-client ecosystem the control protocol is built to enable. Apache adds an explicit patent grant — relevant to the novel PTY flow-control work — and NOTICE/trademark provisions that protect the Cove name and owner credit better than copyleft. Ship `LICENSE` (Apache-2.0), `NOTICE`, and `THIRD-PARTY-NOTICES.md`, with a light trademark note in the README and CONTRIBUTING. Verify the Ryn framework's own license and align before the first tag.

## Consequences

- Broadest adoption plus explicit patent protection.
- Residual fork-and-close risk is accepted; the moats are the maintainer, the ecosystem, and the brand.
- Owner-first credit is carried by NOTICE and README, not by the license mechanism.

## Inventory IDs

- PL-76 — Signed/notarized cross-platform packaging (licensing/governance)

## Related

Applies to the artifacts scaffolded in 0009.
