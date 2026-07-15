# R3Config Release Notes

## Status

R3Config is still under development and has not been publicly released.

## Current highlights

- Local and offline RPCS3 companion for Windows x64.
- Arabic and English interface.
- Game detection and per-game configuration review.
- Support for Game, Canary, and Cheat patches.
- Compatibility checks using serial, version, and executable hash.
- Backup and rollback before writing configuration or patch files.
- Validation of affected YAML files before changes are committed.
- Read-only verification against a local RPCS3 installation.
- Portable build and package verification tools.

## Safety

R3Config does not modify game files or the official `patch.yml`. It does not include RPCS3, firmware, games, telemetry, cloud services, or automatic updates.

The bundled cheat catalog contains third-party community entries. See `CHEATS.md` for the available source and license information.