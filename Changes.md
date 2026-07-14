# Changelog

## Unreleased

- Added Arabic and English support with improved RTL handling.
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
- Added portable build and release verification tools.
