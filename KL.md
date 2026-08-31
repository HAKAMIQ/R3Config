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
- The bundled Artemis Patch catalog requires trusted exact serial/version metadata and a
  valid runtime hash key; explicit local mismatches remain blocked and RPCS3 is
  the final guard when local hash evidence is unavailable.
- The pinned Artemis source archive, commit, MIT license, generated hash, and
  accepted/rejected entry counts are recorded in `CHEATS.md`.
- Discovery and application of R3Config's generated `<TITLE_ID>_patch.yml` still
  require a manual acceptance test against the rolling RPCS3 runtime.
- The app is portable and does not include an installer or automatic updates.
