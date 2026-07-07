# Third-Party Notices

Cove redistributes and builds upon third-party components. Each is listed
below with its license. New entries are appended here as dependencies are
added to `Directory.Packages.props`.

## MinVer

- License: Apache-2.0
- Project: https://github.com/adamralph/minver
- Usage: build-time versioning (not redistributed at runtime).


## atrium-adapters (validate-adapter.sh, test-adapter.sh, schemas/)

- License: MIT
- Copyright (c) 2026 Jonny Asmar
- Project: https://github.com/jonnyasmar/atrium-adapters
- Usage: The adapter authoring harness (`validate-adapter.sh`, `test-adapter.sh`) and JSON-Schema files under `schemas/` are vendored from the atrium-adapters SDK and adapted for Cove (path resolution + `cove://` URIs). The upstream MIT license and copyright notice are preserved per its terms.