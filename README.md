# R3Config 1.0.0

R3Config is a local, offline Windows companion for RPCS3. It discovers installed games, compares their current per-game configuration with the bundled recommendation catalog, and manages explicitly selected RPCS3 patches without editing game files or the official patch catalog.

R3Config هو برنامج Windows محلي لمساعدة مستخدم RPCS3 على مراجعة إعدادات كل لعبة والباتشات المطابقة وتطبيق ما يختاره فقط، مع نسخة احتياطية قبل كل كتابة.

## Requirements

- Windows 10 or Windows 11, x64.
- A local RPCS3 installation selected through `rpcs3.exe`.
- The portable release is self-contained; users do not need to install .NET separately.

## Main workflow

1. Select `rpcs3.exe`.
2. Analyze installed games from `games.yml`, `dev_hdd0/game`, and `PARAM.SFO`.
3. Select a game and review its current and recommended settings.
4. Review patches grouped like RPCS3: game title, serial/version, then patch entries.
5. Select only the settings or patches you want.
6. Confirm the plan. R3Config creates a backup, validates the temporary YAML, then replaces the target atomically.

## RPCS3 compatibility contract

The patch implementation follows the local RPCS3 source contract:

- reads `patch.yml`, `imported_patch.yml`, and `*_patch.yml` in RPCS3 load order;
- supports PPU, SPU, PRX, OVL, and `All` patch targets;
- uses `Hash → Patch title → Game title → Serial → Version → Enabled` for `patch_config.yml`;
- preserves RPCS3 configurable values when disabling a patch;
- never writes to `patch.yml`;
- prepares bundled Canary entries in `imported_patch.yml` only when the user explicitly enables one.

## Safety boundaries

- No downloads, telemetry, cloud sync, auto-update, or full-disk scan.
- No emulator, firmware, game, license, or save-data distribution.
- No automatic patch selection and no blind Apply All.
- Enable requires an exact serial/version/hash match.
- RPCS3 must be closed before any configuration or patch write.
- Invalid YAML blocks writing and is shown in the interface.
- Every configuration and patch write is backed up and rolled back after failure.

## Build and test

```powershell
dotnet restore .\R3Config.slnx
dotnet build .\R3Config.slnx
dotnet run --project .\Tst\R3Config.Tests.csproj
```

Read-only verification against a real RPCS3 installation:

```powershell
.\scripts\Test-Release.cmd -Rpcs3Path "C:\path\to\rpcs3"
```

Add `-LaunchApp` for the final manual UI pass.

## Publish

```powershell
.\scripts\Publish-Portable.cmd -Rpcs3Path "C:\path\to\rpcs3"
```

The script restores, builds, runs the smoke and optional real-install tests, creates a self-contained single-file executable, validates the release tree and language/catalog assets, writes per-file checksums, and produces:

```text
artifacts\R3Config-1.0.0-win-x64-portable.zip
artifacts\R3Config-1.0.0-win-x64-portable.zip.sha256
```

The release contains `R3Config.exe`, the offline catalogs, Arabic/English language files, documentation, license, and `SHA256SUMS.txt`. It never contains RPCS3 or game data.

## Project layout

- `App/`: WPF UI, ViewModels, localization, bundled data, and Canary import workflow.
- `Core/`: scanning, recommendation planning, patch parsing/matching, YAML writes, backup, and restore.
- `Tst/`: executable smoke, regression, RPCS3 contract, and optional real-install tests.
- `scripts/`: release test, publish, package verification, and source-cleanliness scripts.

See [RN.md](RN.md) for release notes and [KL.md](KL.md) for intentional limitations.
