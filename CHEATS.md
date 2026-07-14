# Cheat Catalog Notice

The bundled `cheats.yml` file contains community-made cheat patches that are compatible with RPCS3. These entries were not created by R3Config, and many still include the original author information where available.

The original source of the full collection was not recorded, and there is no complete license list for every entry. Because of that, the R3Config license only covers the program itself and its documentation, not the third-party cheat definitions.

R3Config only uses this catalog after the user selects a compatible cheat. It checks the game serial, version, and executable hash, then writes the selected entries to the RPCS3 runtime filename `<TITLE_ID>_patch.yml` (for example, `BCUS98119_patch.yml`). R3Config refuses to overwrite an unmarked user-owned title-specific file.

Removing `Data/cheats.yml` only disables the built-in cheat library. The rest of R3Config will continue to work normally.

Catalog-SHA256: 0051fa333a38e89a1f626555d3acfae96ae785d66f3ea27c4f6dd53a2a2cb5b0
License-Status: upstream-license-not-provided
Provenance-Status: aggregate-source-not-recorded
