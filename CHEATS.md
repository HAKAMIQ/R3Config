# Artemis RPCS3 Patch Collection Notice

R3Config ships a single executable Artemis cheat catalog: `App/Data/art.yml`.

The catalog is generated from the user-supplied **Artemis Patch Collection for RPCS3 by ChiDreams** and is consumed exclusively through R3Config's normal RPCS3 patch pipeline.

The companion file `App/Data/art_dep.json` contains only reviewed dependency metadata between exact entries in `art.yml`. It contains no executable patch operations and is not a second cheat catalog.

R3Config does not ship or execute:

* a second Artemis cheat database;
* raw PS3/NCL programs;
* a live-memory cheat engine;
* a separate Artemis runtime transport;
* executable code from the external Artemis RPCS3 Cheat Manager.

## Source provenance

Canonical cheat source: Artemis Patch Collection for RPCS3 by ChiDreams
Source URL: https://github.com/chidreams/Artemis-Patch-Collection-RPCS3
Source artifact: Artemis-Patch-Collection-RPCS3-1.01-1654321.zip
Source reference: user-supplied archive Artemis-Patch-Collection-RPCS3-1.01-1654321.zip
Source archive SHA-256: f9e915de0e535b0bbf0fedfbd743f974fc00fa1ea89682e8acb6f049f9491623
Source patch files: 132
Candidate entries: 1028
Accepted entries: 1023
Rejected records: 19
Generated art.yml SHA-256: 710c26be3b185e69f741e03dc8555fc3813c55919348d675e9d496aed7a6d1b3
License: MIT

`Rejected records` are validation records, not a direct rejected-candidate count. A single candidate may produce more than one rejection record when multiple independent validation failures are detected.

## Dependency metadata

Dependency metadata rules: 23
Dependency source revision: 2b981063a6e53cdb5dc8fefda4b19c74903a138c
Generated art_dep.json SHA-256: 4f6eecab14fc65f4194f41a7a9e8880c99205f271e52b3f214b43ab7e56e0063

`art_dep.json` is bounded, non-executable planning metadata pinned to the exact generated Artemis catalog.

The dependency overlay is accepted only when its recorded source revision and `art.yml` SHA-256 match the expected provenance contract and every dependency rule resolves to an exact catalog identity.

Dependency metadata never modifies or expands executable patch operations.

## Manager reference

A second user-supplied archive is retained only as an external provenance and format reference for the same RPCS3 patch ecosystem.

Manager reference: ARTEMIS RPCS3 Cheat Manager 1.0.1 RC4
Manager reference artifact: ARTEMIS-RPCS3-Cheat-Manager-1.0.1-RC4(1).zip
Manager reference archive SHA-256: 372a033655d9c40bbf306a1d56dc5e6749f63400522a577bd15eeadfe175780f

Manager role: external editor, downloader, and validator reference for RPCS3 `imported_patch.yml` v1.2 and the ChiDreams collection.

The manager archive is not a second bundled cheat corpus. None of its Python/Tkinter application code is embedded in or executed by R3Config.

## Import result

The Artemis importer:

* reads patch identity from each YAML definition;
* expands supported YAML aliases;
* validates RPCS3 patch keys and executable operations;
* validates executable runtime keys;
* rejects definitions without valid executable patch sequences;
* resolves supported duplicates deterministically;
* preserves conflicting definitions as rejection records rather than guessing;
* emits one offline RPCS3 `Version: 1.2` catalog.

Rejected reason counts:

* `ARTEMIS_RUNTIME_KEY`: 10
* `ARTEMIS_PATCH_MISSING`: 2
* `ARTEMIS_SOURCE_INVALID`: 2
* `ARTEMIS_DUPLICATE_SUPERSEDED`: 1
* `ARTEMIS_DUPLICATE_CONFLICT`: 4

Total rejection records: 19.

Rejected source data is not silently repaired, inferred, or broadened.

Invalid source YAML, invalid RPCS3 executable keys, missing executable patch sequences, and conflicting same-version definitions remain outside the generated executable catalog unless a deterministic supported resolution exists.

## Application path

Entries from `art.yml` remain ordinary RPCS3 cheat patch definitions.

R3Config evaluates them against the selected game's:

* title ID;
* application version;
* executable fingerprint where available.

The user must explicitly select an Artemis patch before any enable operation is planned.

For dependency-aware planning:

* `ALL` prerequisites require every reviewed prerequisite to be satisfied;
* `ANY` prerequisites require at least one reviewed prerequisite to be satisfied;
* disable planning prevents removal of a prerequisite while an enabled dependent still requires it.

Dependency checks occur before filesystem mutation.

Apply and remove operations use the existing R3Config-managed RPCS3 patch path, including:

* title-specific managed patch definitions;
* RPCS3 patch configuration;
* canonical mutation guards;
* validation;
* backups;
* transactional writes;
* rollback and recovery protections.

RPCS3 remains the runtime authority for executing applicable patch definitions.

## Bundled Artemis scope

Only `App/Data/art.yml` is shipped as executable Artemis cheat data.

`App/Data/art_dep.json` is non-executable dependency-planning metadata tied to that exact catalog.

The following legacy or reference-only Artemis components are not part of the executable product path:

* secondary Artemis databases;
* raw NCL libraries;
* native or reference projection layers;
* community evidence adapters;
* live-memory execution components;
* external Artemis manager application code.

This separation is intentional: Artemis definitions are handled through the same controlled RPCS3 patch pipeline, mutation guards, validation, backup, and rollback architecture used by the rest of R3Config.
