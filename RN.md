# R3Config Release Notes

## Status

R3Config 0.1.0 is free and open source under the GNU General Public License v3 or later. Every feature is available with no license, activation, or account.

## Current highlights

- Local and offline RPCS3 companion for Windows x64.
- Arabic and English interface.
- Game detection and per-game configuration review.
- Support for Game, Canary, and Artemis patches.
- Compatibility checks using serial, version, and executable hash.
- Backup and rollback before writing configuration or patch files.
- Validation of affected YAML files before changes are committed.
- Read-only verification against a local RPCS3 installation.
- Portable build and package verification tools.

## Safety

R3Config does not modify game files or the official `patch.yml`. It does not include RPCS3, firmware, games, telemetry, cloud services, or automatic application updates. The recommendation database can refresh itself from official RPCS3 sources; this downloads public RPCS3 data only, sends no game list or personal data, never changes game settings, and can be turned off in Advanced tools.

The bundled Artemis catalog is generated deterministically from a pinned,
MIT-licensed source archive. See `CHEATS.md` for its source commit, hashes,
accepted/rejected entries, and the outstanding RPCS3 runtime acceptance test.
