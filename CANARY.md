# Canary Catalog Notice

The bundled `App/Data/can.yml` catalog contains community-authored RPCS3 Canary patches. R3Config did not author these definitions and never enables one automatically.

The catalog was normalized from the complete rendered Cargo result supplied for the RPCS3 Wiki page [Help:Game Patches/Canary](https://wiki.rpcs3.net/index.php?title=Help:Game_Patches/Canary). The page source is only a Cargo query over `PatchTable`; it is not itself the patch catalog. Normalization adds RPCS3's `Version: 1.2` document header, hoists shared YAML anchors, and merges the two repeated runtime-key blocks without broadening their declared serial or version scope.

RPCS3 Wiki states that its content is available under the Creative Commons Attribution-ShareAlike 4.0 International license unless otherwise noted. Individual patch author fields are retained in the catalog where supplied.

Source-Snapshot-SHA256: d2fb7ea82f32d86bf819eca31e87ddab2f4b221be3004b01a1ca6da7f0930fcf
Catalog-SHA256: bad9d62921c624c3081723eba489b4e3c1a329c1c84a0c79efcc5d49e4f82d5e
Runtime-Key-Count: 86
License-Status: CC-BY-SA-4.0-unless-otherwise-noted
Provenance-Status: rpcs3-wiki-cargo-snapshot-user-supplied
