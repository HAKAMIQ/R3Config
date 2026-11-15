# R3Config — developer guide

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
[architecture pack](Arch/README.md). See [CHEATS.md](../CHEATS.md) for
the bundled Artemis catalog notice and [KL.md](../KL.md) for known limitations.

## Source handoff

The detailed Arabic product idea and implementation contract are documented in
[docs/ProdAr.md](ProdAr.md).

Create a clean source package with:

    .\Script\SrcExp.cmd

The exporter includes the current working tree, including uncommitted and new
source files. It excludes Git history, Visual Studio state, build outputs,
publish artifacts, logs, temporary files, local user files, backups, and nested
ZIP files. The verified archive and its SHA-256 checksum are written under
`artifacts/source`.
