# R3Config

> Development status: Private development — Unreleased

R3Config is a local Windows companion for RPCS3. It scans installed games, reviews their per-game settings, and helps the user manage compatible patches safely.

R3Config هو برنامج Windows محلي لمراجعة إعدادات ألعاب RPCS3 والباتشات المتوافقة، مع التحقق والنسخ الاحتياطي قبل أي تعديل.

## Features

- Arabic and English interface.
- Local RPCS3 game detection.
- Per-game configuration recommendations.
- Game, Canary, and Artemis Patch views.
- Compatibility checks using serial, version, and executable hash.
- Backup and rollback before configuration or patch writes.
- Offline operation with no telemetry or cloud services.

## Requirements

- Windows 10 or Windows 11 x64.
- A local RPCS3 installation.
- RPCS3 must be closed before writing configuration or patch files.

## Safety

R3Config does not modify game files or the official `patch.yml`.

Patches are not selected automatically. A patch can be enabled only after the user selects it and the required compatibility information matches.

Invalid YAML or an active RPCS3 process blocks writing. Existing files are backed up before changes are committed.

## Build

    dotnet restore .\R3Cfg.slnx
    dotnet build .\R3Cfg.slnx -c Release
    dotnet run --project .\Tst\R3Test.csproj -c Release

## Local portable package

    .\Script\PortPub.cmd -Rpcs3Path "C:\path\to\rpcs3"

The local packaging script builds and verifies the project, then creates the portable package and SHA-256 checksums under `artifacts`. It does not upload the package or create a GitHub Release.

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

Create a clean source package for an authorized developer with:

    .\Script\SrcExp.cmd

The exporter includes the current working tree, including uncommitted and new
source files. It excludes Git history, Visual Studio state, build outputs,
publish artifacts, logs, temporary files, local user files, backups, and nested
ZIP files. The verified archive and its SHA-256 checksum are written under
`artifacts/source`.
## License

R3Config is proprietary software owned exclusively by HAKAMIQ.

Repository access, possession of the source code, or receipt of a compiled
build does not grant permission to install, run, copy, modify, publish,
distribute, sublicense, sell, or create derivative works from R3Config.

Use by another person or organization requires prior written authorization
issued directly by HAKAMIQ.

See [LICENSE](LICENSE) for the complete terms.
