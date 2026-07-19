# Known Limitations — Unreleased

- Supports Windows 10 and Windows 11 x64 only.
- Recommendation and Canary data are bundled locally and do not update automatically.
- Hardware classification is approximate and does not guarantee performance.
- Hash detection relies on RPCS3 logs and local metadata and does not scan full game files.
- Patches with an explicit local hash mismatch cannot be enabled. When the local
  hash is unavailable, only trusted exact serial/version definitions with a valid
  RPCS3 runtime hash key may use RPCS3 as the final runtime guard.
- PRX and OVL support is limited when RPCS3 does not provide the required hash.
- Note translation and risk classification may not always be exact.
- `imported_patch.yml` is imported only after validation and backup.
- The bundled cheat library requires trusted exact serial/version metadata and a
  valid runtime hash key; explicit local mismatches remain blocked and RPCS3 is
  the final guard when local hash evidence is unavailable.
- The source and license details for all cheat entries are not fully documented. See `CHEATS.md`.
- The app is portable and does not include an installer or automatic updates.
