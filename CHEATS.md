# Artemis RPCS3 Patch Collection Notice

R3Config ships one executable Artemis cheat catalog: `App/Data/art.yml`. It is generated from the user-supplied ChiDreams RPCS3 patch collection and is consumed through the normal RPCS3 patch pipeline. The companion `App/Data/art_dep.json` contains only reviewed dependency relationships between exact entries in that catalog; it contains no executable patch operations. R3Config does not ship or execute a second Artemis database, raw PS3/NCL programs, a live-memory cheat engine, or a separate Artemis runtime transport.

Canonical cheat source: Artemis Patch Collection for RPCS3 by ChiDreams
Source URL: https://github.com/chidreams/Artemis-Patch-Collection-RPCS3
Source artifact: Artemis-Patch-Collection-RPCS3-acfd45b-r3fix2.zip
Source reference: pinned ChiDreams commit acfd45b61f8295298523548a9ff3406488736d68 via Artemis-Patch-Collection-RPCS3-acfd45b-r3fix2.zip; 13 reviewed source corrections
Source archive SHA-256: 26a72f769fb1f7cb51daa8502fbc39b817a904fa1073dfa2c602a8c5f2d76ac9
Source patch files: 178
Candidate entries: 1496
Accepted entries: 1471
Rejected records: 25
Generated art.yml SHA-256: b09ecd731e2808967dfd32cb9b112db3bf5aa81fe0ee89a827f7a85d99e7adf5
Dependency metadata rules: 23
Dependency source revision: acfd45b61f8295298523548a9ff3406488736d68
Generated art_dep.json SHA-256: ab0eab975a9217e1e91c406c5600fae1291f56498b89f3c28ea2a8d197844d68
License: MIT

## Manager reference

The second user-supplied archive is retained as a provenance/format reference for the same RPCS3 patch collection. It is not a second bundled cheat corpus and none of its Python/Tkinter application code is embedded in R3Config.

Manager reference: ARTEMIS RPCS3 Cheat Manager 1.0.1 RC4
Manager reference artifact: ARTEMIS-RPCS3-Cheat-Manager-1.0.1-RC4(1).zip
Manager reference archive SHA-256: 372a033655d9c40bbf306a1d56dc5e6749f63400522a577bd15eeadfe175780f
Manager role: external editor/downloader/validator reference for RPCS3 imported_patch.yml v1.2 and the ChiDreams collection; not a bundled cheat database

## Import result

The collection importer reads patch identity from each YAML definition, expands YAML aliases, validates RPCS3 patch keys and operations, resolves duplicates deterministically, and emits a single offline `Version: 1.2` catalog.

Rejected reason counts:

- ARTEMIS_DUPLICATE_SUPERSEDED: 21
- ARTEMIS_DUPLICATE_CONFLICT: 4

The rejected records are not silently repaired or guessed. Invalid source YAML, invalid RPCS3 executable keys, missing executable patch sequences, and conflicting same-version definitions stay outside the generated catalog.

## Application path

`art.yml` entries remain ordinary RPCS3 cheat patch definitions. R3Config matches them against the selected game's title ID, application version, and executable fingerprint where available. The dependency overlay is accepted only when its source revision and `art.yml` hash match this notice, and every reviewed rule resolves to one exact entry. Enable planning enforces ALL/ANY prerequisites; disable planning blocks removal while an enabled dependent remains. The user must explicitly select a patch. Apply/remove operations use the existing managed title-specific patch file, patch configuration, mutation guards, backup, validation, and rollback mechanisms.

Only `Data/art.yml` is shipped as executable Artemis cheat data. `Data/art_dep.json` is bounded, non-executable planning metadata pinned to that exact catalog. Legacy Artemis sidecars/reference databases, raw NCL libraries, native/reference projection layers, community evidence adapters, and live-memory execution components are not part of this product path.
