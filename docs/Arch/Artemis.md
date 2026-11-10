# Artemis RPCS3 patch collection architecture

R3Config has one Artemis integration surface: the ChiDreams **Artemis Patch Collection for RPCS3** converted into the application's normal RPCS3 patch catalog. There is no separate Artemis execution engine.

## Retained source contract

- Canonical source: `Artemis-Patch-Collection-RPCS3-fd267a6-r3fix1.zip`
- Repository: `https://github.com/chidreams/Artemis-Patch-Collection-RPCS3`
- Upstream commit: `fd267a65759a2f1dcae31e362d318de7a6e16594` (2026-10-28)
- Source SHA-256: `cefd5b7475a67d3c36f165935c60f611b8ac6e10a095f4524e53c86fe7b430de`
- Source patch files: `237`
- Candidate definitions before duplicate/dependency resolution: `1956`
- Accepted definitions in the current generated catalog: `1922`
- Rejected source/duplicate records: `34`
- Generated artifact: `App/Data/art.yml`
- Generated artifact SHA-256: `68c1f43daef16b3f05514227d8e63346c56dee9f9e63fda273c8cd5acb630bf2`
- Reviewed dependency metadata: `App/Data/art_dep.json` (`23` exact-entry rules)
- Dependency source revision: `fd267a65759a2f1dcae31e362d318de7a6e16594`
- Dependency artifact SHA-256: `0d60bee207edeec0155b2e7c7010fcbaad70868478237dffe99d29131c55c87e`
- License: MIT

The second user-supplied archive, `ARTEMIS-RPCS3-Cheat-Manager-2.00.zip`, has SHA-256 `caaab46ad39c671470f1d2593958da8c2b9111348660cbd3f0f5a53e93c41604`. It is an external manager/editor/downloader for RPCS3 patch files and the same ChiDreams collection. It provides format/provenance reference only; it is not copied into the application and does not add a second cheat catalog.

### Source archive construction

The source archive is the upstream commit ZIP reduced to the per-game patch files plus `LICENSE`. It omits `README.md`, the `.github` folder and the CI-compiled aggregate `imported_patch.yml`, which repeats every per-game definition. Two reviewed corrections are applied, nothing else:

1. `God of War Ascension [BCUS98232] v1.12` declares its 10 definitions under `PPU-03a0b43e4a5f4bfea64f53612ee7c5d990f88129c`, a 41-hex key RPCS3 cannot generate. The key is replaced with `PPU-3a0b43e4a5f4bfea64f53612ee7c5d990f88129c`. This is the same correction the previous `acfd45b` archive carried, and the corrected hash matches a locally captured RPCS3 `PPU executable hash` for BCUS98232.
2. `The House Of The Dead 4 [NPUB30603] v1.00` is byte-identical to `THE HOUSE OF THE DEAD 4 [NPUB30603] v1.00`. The importer rejects file identities that differ only by case, so the second copy is omitted.

Every other upstream conflict stays fail-closed. For example, two Puppeteer files declare different `Invincible` definitions under the same key, and both are rejected.

Compared with the previous `acfd45b` catalog: 462 definitions were added, 11 were dropped (10 renamed or merged upstream, plus the conflicting Puppeteer `Invincible`), and 59 kept their identity but changed definition, notes or author upstream. All 23 dependency rules still resolve to exactly one entry, with definitions and notes unchanged.

### Catalog updates and installed cheats

A cheat the user already enabled lives in the managed `<SERIAL>_patch.yml` as a `ManagedArtemis` entry with the same run key and title as its `art.yml` source. `PatchCatalogMergeService` always keeps that resident copy over the bundled one, because the resident copy is what RPCS3 applies. A changed bundled definition at an equal or unordered Patch Version used to reject both copies. That hid an entry that stayed enabled in `patch_config.yml`, and a hidden resident could block a sibling enable through a write overlap the user could not clear. The pair is now resolved to the resident with the `PATCH_MANAGED_RESIDENT_KEPT` diagnostic, so the row stays visible and can be disabled or removed. Removing it exposes the updated bundled definition. Every other same-version conflict, including two bundled or two managed copies, still rejects all variants.

## Data flow

```text
ChiDreams source ZIP
 -> Script/ArtImport + ArtemisCatalogImporter
 -> validation / alias expansion / deterministic duplicate policy
 -> App/Data/art.yml
 + App/Data/art_dep.json (bounded, non-executable exact dependency overlay)
 -> PatchCatalogUseCase (PatSrc.Artemis)
 -> PatchMatchService / PatchCompatibilityService
 -> PatPrev Artemis/Cheat UI
 -> explicit user selection
 -> BundledCheatPatchService
 -> managed <TITLE_ID>_patch.yml
 -> patch_config.yml
 -> guarded transaction + backup/rollback
```

The application never parses the raw source ZIP at runtime. It reads the shipped `art.yml` catalog and its pinned dependency overlay. The overlay must match the exact catalog hash, source revision, and reviewed rule count; every rule must resolve to exactly one Artemis entry. Invalid or cyclic dependency metadata fails the Artemis source closed while leaving independent patch sources available.

Enable plans enforce both ALL and ANY prerequisite groups by exact run key, title ID, version, and patch title. Already-enabled or jointly selected prerequisites satisfy a group. Disable plans reject removal of a prerequisite while a dependent remains enabled. These checks run during planning, before any filesystem mutation.

## Import rules

The importer:

1. snapshots the source ZIP once under a compressed-size bound, verifies its pinned SHA-256, and parses that exact in-memory snapshot so hash verification and parsing cannot observe different source bytes;
2. verifies the user-supplied manager-reference archive itself against its expected SHA-256 instead of trusting a hash string as provenance;
3. rejects source, manager, catalog, notice, dependency-metadata, and transaction-artifact collisions by canonical filesystem identity (including resolved reparse targets) before any output side effect;
4. bounds ZIP entry count, actual decompressed bytes per entry, aggregate expanded bytes, UTF-8 decoding, and duplicate source-file identities;
5. requires the source MIT license and rejects unsafe ZIP paths;
6. loads each patch file independently and reads title IDs, versions, and executable keys from YAML content rather than filenames;
7. expands YAML aliases into standalone definitions, rejects malformed source files and invalid RPCS3 executable keys, and rejects definitions without executable `Patch` operations;
8. resolves equivalent duplicates deterministically and rejects conflicting same-version duplicates;
9. validates the generated catalog through `ReadSvc`, validates the colocated `art_dep.json` provenance against the generated catalog hash, and regenerates the complete `CHEATS.md` provenance contract including dependency metadata;
10. stages both generated outputs with write-through read-back, validates expected pre-commit state, serializes writers with a lock, uses atomic replace/move semantics, preserves external changes during rollback, and keeps a recovery journal/backups so an interrupted two-file commit can be recovered on the next importer run.

Regeneration from the repository root uses the build-time importer. The source-reference argument is a bounded descriptive label and must include the actual source archive file name; the SHA-256 arguments provide cryptographic identity. The manager archive path is supplied explicitly and its bytes are hashed by the importer:

```powershell
dotnet run --project .\Script\ArtImport\R3ArtImport.csproj -c Release -- `
  <Artemis-Patch-Collection-RPCS3-fd267a6-r3fix1.zip> `
  .\App\Data\art.yml `
  .\CHEATS.md `
  cefd5b7475a67d3c36f165935c60f611b8ac6e10a095f4524e53c86fe7b430de `
  "pinned ChiDreams commit fd267a65759a2f1dcae31e362d318de7a6e16594 via Artemis-Patch-Collection-RPCS3-fd267a6-r3fix1.zip; 2 reviewed source corrections" `
  <ARTEMIS-RPCS3-Cheat-Manager-2.00.zip> `
  caaab46ad39c671470f1d2593958da8c2b9111348660cbd3f0f5a53e93c41604
```

`art_dep.json` must already carry the new catalog SHA-256 and the `ArtDepSvc` constants must match it, because the importer validates the dependency overlay against the catalog it generates before it writes anything.

## Deliberately absent Artemis systems

R3Config does not include a raw PS3/NCL database, a second executable/reference catalog, general community/evidence sidecars, a separate native-projection subsystem, or direct guest-memory execution. The single `art_dep.json` overlay is limited to reviewed dependency relationships and provenance for exact entries in `art.yml`; it cannot carry patch operations. Artemis entries remain ordinary RPCS3 patch definitions and follow the same compatibility, confirmation, mutation, backup, and rollback boundaries as other managed patches.

Generic RPCS3 executable-key matching remains part of the patch compatibility model; it is not an Artemis execution subsystem.
