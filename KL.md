# Known limitations — R3Config 1.0.0

- Windows 10/11 x64 only. There is no macOS, Linux, web, service, or mobile build.
- The recommendation and bundled Canary catalogs are offline snapshots. R3Config does not download updates.
- Hardware classification is a local heuristic, not a performance guarantee.
- Hash discovery is deliberately bounded to local RPCS3 logs and per-game cache names; it never hashes game binaries or scans the full disk.
- PPU and SPU hashes can be discovered from current RPCS3 logs. PRX and OVL entries are parsed and displayed, but remain non-actionable when RPCS3 metadata does not expose their exact hash.
- A patch with unknown or mismatched hash cannot be enabled. Cold-boot the game, close RPCS3, and analyze again.
- Global `All/All/All` entries are shown on a game page only when their hash matches that game or they are already enabled.
- Risk/type classification and Arabic note translation are local heuristics. The original note remains available when no curated translation exists.
- The advanced manual import replaces `imported_patch.yml` only after validation and backup. Managed bundled Canary preparation instead preserves manual content outside the R3Config-managed block.
- The final release is portable and self-contained; it does not include an installer or automatic updater.
