# Security Policy

## Reporting a vulnerability

If you discover a security vulnerability in Cove, please report it responsibly:

1. **Do not** open a public GitHub issue.
2. Email the maintainer at the address listed on the GitHub profile.
3. Include a description of the vulnerability, steps to reproduce, and any relevant logs.

You will receive a response within 72 hours. If the vulnerability is confirmed, a fix will be prioritized and disclosed after a coordinated release.

## Scope

Cove is a local-first terminal workspace. The daemon listens on a local control socket (`cove://`) and does not expose network endpoints by default. Telemetry is opt-in and off by default. Crash reports are local-only with zero network egress.

## Supported versions

Only the latest release line receives security fixes.

## Disclosure policy

- Vulnerabilities are disclosed after a fix is released.
- We credit reporters unless they prefer to remain anonymous.
