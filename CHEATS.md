# Artemis RPCS3 Patch Collection Notice

R3Config ships one executable Artemis cheat catalog: `App/Data/art.yml`. It is generated from the user-supplied ChiDreams RPCS3 patch collection and is consumed through the normal RPCS3 patch pipeline. The companion `App/Data/art_dep.json` contains only reviewed dependency relationships between exact entries in that catalog; it contains no executable patch operations. R3Config does not ship or execute a second Artemis database, raw PS3/NCL programs, a live-memory cheat engine, or a separate Artemis runtime transport.

Canonical cheat source: Artemis Patch Collection for RPCS3 by ChiDreams
Source URL: https://github.com/chidreams/Artemis-Patch-Collection-RPCS3
Source artifact: Artemis-Patch-Collection-RPCS3-fd267a6-r3fix1.zip
Source reference: pinned ChiDreams commit fd267a65759a2f1dcae31e362d318de7a6e16594 via Artemis-Patch-Collection-RPCS3-fd267a6-r3fix1.zip; 2 reviewed source corrections
Source archive SHA-256: cefd5b7475a67d3c36f165935c60f611b8ac6e10a095f4524e53c86fe7b430de
Source patch files: 237
Candidate entries: 1956
Accepted entries: 1922
Rejected records: 34
Generated art.yml SHA-256: 68c1f43daef16b3f05514227d8e63346c56dee9f9e63fda273c8cd5acb630bf2
Dependency metadata rules: 23
Dependency source revision: fd267a65759a2f1dcae31e362d318de7a6e16594
Generated art_dep.json SHA-256: 0d60bee207edeec0155b2e7c7010fcbaad70868478237dffe99d29131c55c87e
License: MIT

## Manager reference

The second user-supplied archive is retained as a provenance/format reference for the same RPCS3 patch collection. It is not a second bundled cheat corpus and none of its Python/Tkinter application code is embedded in R3Config.

Manager reference: ARTEMIS RPCS3 Cheat Manager 2.00
Manager reference artifact: ARTEMIS-RPCS3-Cheat-Manager-2.00.zip
Manager reference archive SHA-256: caaab46ad39c671470f1d2593958da8c2b9111348660cbd3f0f5a53e93c41604
Manager role: external editor/downloader/validator reference for RPCS3 imported_patch.yml v1.2 and the ChiDreams collection; not a bundled cheat database

## Import result

The collection importer reads patch identity from each YAML definition, expands YAML aliases, validates RPCS3 patch keys and operations, resolves duplicates deterministically, and emits a single offline `Version: 1.2` catalog.

Rejected reason counts:

- ARTEMIS_DUPLICATE_SUPERSEDED: 28
- ARTEMIS_DUPLICATE_CONFLICT: 6

The rejected records are not silently repaired or guessed. Invalid source YAML, invalid RPCS3 executable keys, missing executable patch sequences, and conflicting same-version definitions stay outside the generated catalog.

## Application path

`art.yml` entries remain ordinary RPCS3 cheat patch definitions. R3Config matches them against the selected game's title ID, application version, and executable fingerprint where available. The dependency overlay is accepted only when its source revision and `art.yml` hash match this notice, and every reviewed rule resolves to one exact entry. Enable planning enforces ALL/ANY prerequisites; disable planning blocks removal while an enabled dependent remains. The user must explicitly select a patch. Apply/remove operations use the existing managed title-specific patch file, patch configuration, mutation guards, backup, validation, and rollback mechanisms.

Only `Data/art.yml` is shipped as executable Artemis cheat data. `Data/art_dep.json` is bounded, non-executable planning metadata pinned to that exact catalog. Legacy Artemis sidecars/reference databases, raw NCL libraries, native/reference projection layers, community evidence adapters, and live-memory execution components are not part of this product path.
