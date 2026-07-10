# Changelog

## 1.0.0 — Final release

- Aligned patch parsing, hierarchy, source precedence, config keys, and enable/disable persistence with the local RPCS3 patch engine and Patch Manager source.
- Added explicit source provenance and RPCS3-style duplicate/version precedence diagnostics.
- Added PRX, OVL, and global `All` target support.
- Fixed same-serial multi-target matching and normalized RPCS3 app-version formats.
- Retained multiple PPU/SPU hashes from one bounded local log session and cached log reads for fast multi-game analysis.
- Merged installed `dev_hdd0` update versions over older disc metadata.
- Rebuilt the patch UI with separate Game, Canary, and Cheat views; direct compatibility reasons; installed/required versions; enabled state; and technical LTR isolation.
- Removed automatic patch selection and the competing Canary Prepare action.
- Added section-isolated enable/disable commands and automatic managed Canary preparation during explicit enable.
- Changed disable writes to remove `Enabled` like RPCS3 while preserving configurable values and supporting legacy scalar config entries.
- Added warning surfaces for invalid YAML and RPCS3-running write blocks.
- Renamed projects and release outputs to stable `R3Config` names and unified version metadata at 1.0.0.
- Added self-contained single-file publishing, package validation, language/catalog checks, SHA-256 manifests, and optional read-only real-install regression testing.

## 0.6.0-beta.1 — P6 Beta Hardening

- Added safe `games.yml` scanner handling for invalid YAML and unreadable files.
- Added config apply service in Core with backup-before-write and rollback after failed writes.
- Hardened patch apply service with automatic restore after failed writes.
- Made corrupt custom config preview tolerant by treating unreadable values as unknown instead of crashing analysis.
- Added SmokeTests for missing config folder, corrupt games list, large games list, missing patches folder, missing/existing/corrupt `patch_config.yml`, permission-denied simulation, and restore-after-failed-write behavior.
- Added `docs/P6_BETA_QA.md` with Windows 10/11, Arabic/English, and P7 final gate checklist.
- Added `scripts/Test-P6-Beta.ps1` for the required restore/build/smoke/app run sequence.
- Added `scripts/Verify-SourceClean.ps1` for source ZIP cleanliness checks.
- Extended portable release verification to reject games, firmware, RPCS3 binaries, and source/build artifacts.
- Kept release local/offline only; no cloud update and no auto-update.

## 0.5.0-p5 — Packaging / Release

- Added app version metadata for the WPF release executable.
- Added embedded application icon.
- Added `LICENSE`.
- Added `KL.md`.
- Added `RN.md`.
- Added portable publish script: `scripts/Publish-Portable.ps1`.
- Added release verification script: `scripts/Verify-PortableRelease.ps1`.
- Configured publish output to include README, LICENSE, CHANGELOG, known limitations, release notes, and the built-in catalog.
- Kept publish target portable, x64, and framework-dependent: `--self-contained false`.
- Kept release local/offline only; no cloud update and no auto-update.

## 0.4.0-p4 — Arabic UX + Windows 11 Polish

- Added Arabic UI text for the main workflow.
- Added RTL support for the WPF shell and major controls.
- Added Arabic risk labels and tooltips.
- Added clearer Arabic error/status messages.
- Added empty states for games, recommendations, patches, and backups.
- Applied compact Windows 11-like cards, borders, and radius.
- Avoided WinUI rewrite, heavy blur, and heavy shadows.

## 0.3.0-p3 — Data Update / Catalog System

- Kept the built-in catalog at `App/Data/cat.json`.
- Added catalog metadata in Core.
- Added `CatInfoService`.
- Added built-in data date and recommendation count display.
- Moved catalog import/rebuild/export functions into Advanced / Developer only.
- Added safe missing/invalid catalog handling.
- Re-applied risk policy when loading catalog data instead of trusting stored JSON risk.
- Kept catalog local/offline only.

## P1-C Safe Patch Apply

- Added patch apply planning in Core.
- Added patch_config reader/writer with temp-file write, YAML validation, and atomic replace.
- Added backup-before-write patch apply service.
- Added UI command for applying checked safe patches only.
- Preserved unrelated patch_config entries.
- Added restore coverage for patch_config backups.
- Kept 60 FPS, Cheat, Canary, Experimental, Unknown, and hash-unknown patches out of apply plans.

## P1-B.1 Patch Preview Closeout

- Changed patch preview to show patches for the selected game only.
- Added exact/hash-unknown/mismatch status coverage.
- Added tests for Experimental and Unknown default selection behavior.
- Kept Canary hidden in normal mode and visible only in Expert preview mode.

## P1-B Patch Preview UI

- Added preview-only patch table in the main UI.
- Added detailed patch match statuses, including hash-unknown and mismatch states.
- Added conservative patch preview selection policy.
- Kept Canary patches hidden unless Expert preview mode is enabled.
- Kept 60 FPS, Cheat, Canary, Experimental, Performance, Unknown, and hash-unknown patches unchecked by default.
- Confirmed no code writes `patch_config.yml` in this phase.

## P1-A Patch Reader Preview

- Added `patch.yml` reader in Core.
- Added patch risk classification.
- Added serial/version/hash preview matching.
- Added SmokeTests for valid/invalid patch YAML and no-write preview behavior.

- Added `scripts/Publish-Portable.cmd` to run the portable publish flow when unsigned PowerShell scripts are blocked by local execution policy.


### Packaging fix
- Added runtime-specific restore for `win-x64` publish to prevent NETSDK1047.
- Added native dotnet exit-code checks so failed publish stops before release verification.
- Fixed portable publish PowerShell parser error by delimiting `${LASTEXITCODE}` before colon text.

## 0.6.0-beta.2 - P2 hardware/profile backfill

- Added Core `HwInfo` and `HwProbe`.
- Added read-only PatchHashProbeService for RPCS3.log/metadata hash probing.
- Added hardware status to the WPF status bar.
- Added smoke coverage for safe hardware probe failure and HashUnknown patch safety.
- Added manual QA document for patch_config.yml / RPCS3 Enabled verification.

## UI hardening follow-up

- Fixed recommendation plan mismatch: normal UI now shows only settings that actually need changes, so a game with `0` required changes no longer shows already-matching rows as actionable options.
- Reworded config status from `مطبّق` to `مطابق` / `يحتاج تعديل` to avoid ambiguous Arabic UX.
- Stabilized WPF DataGrid column layout by keeping the grid layout LTR internally while rendering Arabic headers/cells right-aligned.
- Renamed game-list diff header to `تحتاج تعديل`.
- Added CMD wrappers for PowerShell-gated scripts: `Verify-SourceClean.cmd` and `Test-P6-Beta.cmd`.
- Updated source verification to support development-tree mode after local builds and strict ZIP verification for release/source packages.
