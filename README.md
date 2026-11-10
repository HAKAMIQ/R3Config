# R3Config

> Release: v0.1.0 — free and open source under the GNU GPL v3 or later.

R3Config is a local Windows companion for RPCS3. It scans installed games, reviews their per-game settings, and helps the user manage compatible patches safely.

R3Config هو برنامج Windows محلي لمراجعة إعدادات ألعاب RPCS3 والباتشات المتوافقة، مع التحقق والنسخ الاحتياطي قبل أي تعديل.

## Features

- Arabic and English interface.
- Local RPCS3 game detection.
- Per-game configuration recommendations.
- Game, Canary, and Artemis Patch views.
- Compatibility checks using serial, version, and executable hash.
- Backup and rollback before configuration or patch writes.
- Offline operation with no telemetry or cloud services; the optional recommendation update only downloads public data from official RPCS3 sources.

## Requirements

- Windows 10 or Windows 11 x64.
- A local RPCS3 installation.
- RPCS3 must be closed before writing configuration or patch files.

## Safety

R3Config does not modify game files or the official `patch.yml`.

Patches are not selected automatically. A patch can be enabled only after the user selects it and the required compatibility information matches.

Invalid YAML or an active RPCS3 process blocks writing. Existing files are backed up before changes are committed.

## Build

The pinned SDK is declared by `global.json`. `R3Cfg.slnx` remains the solution
metadata, while the command-line verification graph is rooted internally at
`Tst/R3Test.csproj`, whose project references cover the solution projects.
`R3Toolchain.ps1` owns the scoped dotnet environment so unrelated machine-level
workload manifests cannot affect this workload-free WPF/.NET 8 repository.

    .\Script\RelTest.cmd -Configuration Release

## Local portable package

    .\Script\PortPub.cmd -Rpcs3Path "C:\path\to\rpcs3"

The local packaging script builds and verifies the project, then creates the portable package and SHA-256 checksums under `artifacts`. It does not upload the package or create a GitHub Release.
The portable package is self-contained: it bundles the .NET 8 runtime, so no separate .NET installation is needed to run it.

## Project structure

- `App/Application/` — use-case orchestration without WPF dependencies.
- `App/Composition/` — the single dependency composition root.
- `App/Vm/`, `App/View/`, `App/Ctrl/` — WPF presentation and commands.
- `App/Flow.cs` — a thin compatibility facade over the use cases.
- `Core/` — models, scanning, planning, parsing, matching, mutation guards,
  transactions, backup, and restore.
- `Tst/` — smoke and regression tests.
- `Script/` — build, verification, and packaging scripts.
- `docs/Arch/` — the developer architecture and extension guide.

Developers should start with the
[architecture pack](docs/Arch/README.md). See [CHEATS.md](CHEATS.md) for
the bundled Artemis catalog notice and [KL.md](KL.md) for known limitations.

## Source handoff

The detailed Arabic product idea and implementation contract are documented in
[docs/ProdAr.md](docs/ProdAr.md).

Create a clean source package with:

    .\Script\SrcExp.cmd

The exporter includes the current working tree, including uncommitted and new
source files. It excludes Git history, Visual Studio state, build outputs,
publish artifacts, logs, temporary files, local user files, backups, and nested
ZIP files. The verified archive and its SHA-256 checksum are written under
`artifacts/source`.

## License

Copyright (C) 2026 HAKAMIQ.

R3Config is free software: you can redistribute it and/or modify it under the
terms of the GNU General Public License as published by the Free Software
Foundation, either version 3 of the License, or (at your option) any later
version.

R3Config is distributed in the hope that it will be useful, but WITHOUT ANY
WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A
PARTICULAR PURPOSE. See [LICENSE](LICENSE) for the full GNU General Public
License text.

Third-party libraries, patch definitions, cheat definitions, and other bundled
materials remain under their own licenses; see
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) and the bundled notices.

R3Config is an independent companion tool. It is not part of RPCS3 and is not
affiliated with or endorsed by the RPCS3 project. R3Config does not include
RPCS3, PlayStation firmware, or games; use only games and firmware that you have
legally obtained.
