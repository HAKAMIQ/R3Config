# R3Config 1.0.0 Release Notes

## Release status

R3Config 1.0.0 is the first final, offline Windows x64 release. The portable package is self-contained and does not require a separate .NET installation.

## Highlights

- Rebuilt the patch surface around the actual RPCS3 patch-engine contract.
- Explicit patch provenance: official, imported, bundled Canary, and title-specific.
- RPCS3-compatible PPU, SPU, PRX, OVL, and `All` target parsing.
- Best-target matching across every same-serial target with normalized versions such as `1.19`, `01.19`, and `v01.19`.
- Multiple PPU/SPU hashes retained from a bounded local RPCS3 session-log probe.
- Separate Game patches, Canary patches, and Cheats views and commands.
- No patch is preselected; every patch write requires an explicit user choice and confirmation.
- Direct row-level explanations for version mismatch, missing hash, hash mismatch, current enabled state, and required version.
- Current installed update version is merged from `dev_hdd0/game` instead of being masked by an older disc `PARAM.SFO`.
- `patch_config.yml` enable/disable shape matches RPCS3, including legacy scalar reads and preservation of configurable values.
- Backup, temporary YAML validation, atomic replacement, and automatic rollback on write failure.
- Managed Canary preparation preserves manual content outside R3Config's block.
- Arabic/English key parity, technical LTR isolation, and Western digits for serials, versions, and hashes.
- Clean self-contained portable publishing with SHA-256 manifests.

## Verified scope

Automated coverage includes language parity, WPF XAML loading, game discovery, missing/corrupt YAML, recommendation planning, backup/restore, failed-write rollback, Canary normalization, patch parsing, source precedence, duplicate diagnostics, version normalization, multiple targets, multiple hashes, PRX/OVL, global targets, enabled-state reading, enable/disable writing, and package validation.

The optional real-install test is read-only and verifies that analysis does not change `patch.yml`, `imported_patch.yml`, or `patch_config.yml`.

## Safety boundaries

R3Config does not download content, modify RPCS3 source, edit games, ship emulator or firmware files, use telemetry, or write to official `patch.yml`.

## Release artifacts

```text
R3Config-1.0.0-win-x64-portable.zip
R3Config-1.0.0-win-x64-portable.zip.sha256
```
