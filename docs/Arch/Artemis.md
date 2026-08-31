# Artemis RPCS3 patch collection architecture

R3Config has one Artemis integration surface: the ChiDreams **Artemis Patch Collection for RPCS3** converted into the application's normal RPCS3 patch catalog. There is no separate Artemis execution engine.

## Retained source contract

- Canonical source: `Artemis-Patch-Collection-RPCS3-1.01-1654321.zip`
- Repository: `https://github.com/chidreams/Artemis-Patch-Collection-RPCS3`
- Source SHA-256: `f9e915de0e535b0bbf0fedfbd743f974fc00fa1ea89682e8acb6f049f9491623`
- Source patch files: `132`
- Candidate definitions before duplicate/dependency resolution: `1028`
- Accepted definitions in the current generated catalog: `1023`
- Rejected source/duplicate records: `19`
- Generated artifact: `App/Data/art.yml`
- Generated artifact SHA-256: `710c26be3b185e69f741e03dc8555fc3813c55919348d675e9d496aed7a6d1b3`
- Reviewed dependency metadata: `App/Data/art_dep.json` (`23` exact-entry rules)
- Dependency source revision: `2b981063a6e53cdb5dc8fefda4b19c74903a138c`
- Dependency artifact SHA-256: `4f6eecab14fc65f4194f41a7a9e8880c99205f271e52b3f214b43ab7e56e0063`
- License: MIT

The second user-supplied archive, `ARTEMIS-RPCS3-Cheat-Manager-1.0.1-RC4(1).zip`, has SHA-256 `372a033655d9c40bbf306a1d56dc5e6749f63400522a577bd15eeadfe175780f`. It is an external manager/editor/downloader for RPCS3 patch files and the same ChiDreams collection. It provides format/provenance reference only; it is not copied into the application and does not add a second cheat catalog.

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
  <Artemis-Patch-Collection-RPCS3-1.01-1654321.zip> `
  .\App\Data\art.yml `
  .\CHEATS.md `
  f9e915de0e535b0bbf0fedfbd743f974fc00fa1ea89682e8acb6f049f9491623 `
  "user-supplied archive Artemis-Patch-Collection-RPCS3-1.01-1654321.zip" `
  <ARTEMIS-RPCS3-Cheat-Manager-1.0.1-RC4(1).zip> `
  372a033655d9c40bbf306a1d56dc5e6749f63400522a577bd15eeadfe175780f
```

## Deliberately absent Artemis systems

R3Config does not include a raw PS3/NCL database, a second executable/reference catalog, general community/evidence sidecars, a separate native-projection subsystem, or direct guest-memory execution. The single `art_dep.json` overlay is limited to reviewed dependency relationships and provenance for exact entries in `art.yml`; it cannot carry patch operations. Artemis entries remain ordinary RPCS3 patch definitions and follow the same compatibility, confirmation, mutation, backup, and rollback boundaries as other managed patches.

Generic RPCS3 executable-key matching remains part of the patch compatibility model; it is not an Artemis execution subsystem.
