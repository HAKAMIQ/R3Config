# Changelog

## Unreleased

- Completed the native Windows 11 bidirectional layout contract: Arabic now
  mirrors window and control geometry with right-aligned localized text, English
  remains left-to-right/left-aligned, caption controls stay on the physical
  right, and technical values remain LTR/left-aligned.
- Added local RPCS3 game detection and recommendation review.
- Added support for Game, Canary, and Cheat patches.
- Added patch matching by serial, game version, and executable hash.
- Matched RPCS3's runtime Hash guard so trusted serial/version matches can be
  enabled without requiring a prior boot log, while authoritative main-PPU
  mismatches remain blocked.
- Added one compatibility architecture for official, Canary, imported, and
  cheat patches, including RPCS3 portable/config-root resolution, Base57 PPU
  cache decoding, PRX/OVL/SPU runtime evidence, and TITLE_ID-scoped keys.
- Simplified player patch pages to show enable/disable state and relevant
  instructions, while keeping file paths and authoring actions in advanced tools.
- Added safe backups, rollback, and protection against invalid writes.
- Added hardware and RPCS3 log detection for better compatibility checks.
- Improved the interface, empty states, diagnostics, and patch management.
- Reworked H-Canary authoring into four guided steps, with inline surfaces for
  saved patches, target versions, and advanced instruction references.
- Aligned H-Canary operations and value validation with RPCS3's current patch
  parser, including file operations, decimal values, and canonical All targets.
- Limited the H-Canary editor to patches created by R3Config so opening and
  saving cannot silently remove unsupported fields from other imported patches.
- Simplified H-Canary actions to one primary action per surface, with explicit
  Previous/Next navigation and contextual save, copy, and delete actions.
- Added portable build and release verification tools.

## H-Canary Advanced Tools workspace

- Integrated the H-Canary editor into `AdvDlg` without creating a second `HCanVm`.
- Replaced saved, target, and anchor window nesting with an in-workspace drawer and panels.
- Added deterministic step readiness, visible blocked-step guidance, and raw localization runtime checks.
- Kept RPCS3 validation, transaction, save/delete, mutation-guard, and `imported_patch.yml` contracts unchanged.
