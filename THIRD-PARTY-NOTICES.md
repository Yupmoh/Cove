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
## sherpa-onnx (org.k2fsa.sherpa.onnx + per-RID runtime packages)

- License: Apache-2.0
- Project: https://github.com/k2-fsa/sherpa-onnx
- Usage: local speech-to-text for voice dictation (`Cove.Dictation`). The runtime packages redistribute the sherpa-onnx native library and ONNX Runtime (MIT, https://github.com/microsoft/onnxruntime).

## PortAudioSharp2

- License: MIT
- Project: https://github.com/k2-fsa/portaudiosharp2
- Usage: microphone capture for voice dictation. Redistributes the PortAudio native library (PortAudio license, MIT-style, http://www.portaudio.com/license.html).

## Speech models (downloaded at first use, NOT redistributed with Cove)

- NVIDIA Parakeet TDT 0.6b v3 (int8 ONNX export via sherpa-onnx): CC-BY-4.0, https://huggingface.co/nvidia/parakeet-tdt-0.6b-v3
- Silero VAD (silero_vad.onnx): MIT, https://github.com/snakers4/silero-vad
- Both are fetched by the user's machine from the sherpa-onnx release mirror on first dictation use, verified by pinned SHA-256; Cove installers do not bundle them.
