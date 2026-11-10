# NCL corpus and bundled discovery

The NCL corpus layer has two responsibilities:

1. audit untrusted Artemis / NetCheat `.ncl` sources without converting them; and
2. discover exact game candidates from the pinned R3Config bundled corpus.

Neither responsibility writes RPCS3 files or process memory.

## Bundled source

The shipped database is `App/Data/ncl.zip`. It is the deterministic projection of the verified four-lineage NCL corpus and is not derived from `App/Data/art.yml`.

Pinned provenance is documented in repository-root `NCL.md` and embedded in the ZIP as schema-2 `PROVENANCE.json` plus the upstream ArtemisPS3 license notice `LICENSE-ArtemisPS3.txt`.

Current corpus contract:

- NCL entries: `2910`, of which 362 are same-name alternates stored under `codes/variants/`;
- total ZIP entries: `2912`;
- bundled ZIP SHA-256: `C4C70C6E73A225F1E15096A3AEA06DD05461F7EF0CBB4BE8B106577ABC22D8B8`;
- dialect: `ArtemisPs3`;
- provenance schema: `2`;
- source lineages (5): `BUCANERO_CURRENT`, `ALDOSTOOLS_CODELIST`, `DNAWRKSHP_HISTORICAL`, `AR_SITE_CODELIST_2026`, and the historical `R3CONFIG_CURRENT` carry-forward lineage.

Repository-root `NCL.md` records the per-lineage SHA-256 and NCL counts.

Schema 2 records each lineage independently with its pinned SHA-256 and NCL count. `R3CONFIG_CURRENT` is explicitly historical carry-forward lineage rather than independent upstream evidence, so rebuilding from the currently adopted corpus does not create self-referential provenance. The bundled ZIP SHA is intentionally not stored inside `PROVENANCE.json`.

`LICENSE-ArtemisPS3.txt` reproduces the proven upstream ArtemisPS3 license notice. It does not assert that every independently sourced cheat definition is covered by that license.

`SrcChk.ps1` and the portable release gate require the exact pinned ZIP, exactly 2910 safe `codes/` NCL entries, both root metadata records, schema 2, and the exact five source-lineage records. Hash, count, lineage, license-notice, or schema drift fails closed.

## Bounds and untrusted-input rules

The reader accepts a directory tree or ZIP for audit. Bundled discovery accepts only a ZIP with the expected SHA-256.

The reader enforces:

- at most 4096 NCL files;
- at most 2 MiB per NCL file;
- at most 32 MiB total expanded NCL bytes;
- at most 128 MiB for a source ZIP;
- no rooted or `..` ZIP logical paths;
- no duplicate NCL logical paths during audit;
- strict UTF-8 first, followed only by the explicit Windows-1252 source fallback.

Replacement-character decoding is not conversion evidence.

Bundled lookup hashes and reads the ZIP central directory through the same read-shared file handle. Preview reopens and rehashes the pinned bundle and verifies the selected entry SHA-256 and static semantic fingerprint before conversion. Source drift between discovery and preview aborts.

## Game identity

R3Config game `Version` is the effective SFO application version: `APP_VER` when present, otherwise `VERSION`.

Artemis filenames can encode both fields, for example:

`BLES01128 v01.00 av01.02`

The identity parser therefore records an exact target pair:

`BLES01128 -> APP_VER 01.02` with `VERSION 01.00` retained as source evidence.

It does not treat `01.00` and `01.02` as interchangeable versions. When one filename contains different serial/version groups, the groups remain paired; a version declared for one serial never leaks to another serial.

A bundled candidate requires exact `Serial + effective application version`. Missing or mismatching bundled identity is not selectable.

## Discovery classification

Filename identity is used to narrow the bundled ZIP first. Only exact files are then decoded, parsed and semantically verified.

Discovery and convertibility are separate. Every cheat parsed from an exact `Serial + effective application version` source is listed with its verifier status, so the user can see what the source contains. `NclCorpusCheatCandidate.CanConvert` is true only for:

- `OfflineStaticCandidate` cheats successfully lowered by `NclStaticLowerer`; and
- `OptionSelectionRequired` cheats whose option tags resolve to declared option sets. The user must choose every option value explicitly; `NclOptionSubstitution` reproduces the Artemis text substitution and the resolved text then goes through the same parser, verifier, lowerer, binder and emitter.

`RuntimeEvidenceRequired`, `ConstantRuntimeRequired`, `NonExecutableMetadata`, `Unsupported`, and `Invalid` entries stay discoverable with `CanConvert` false. Preview and Apply reject any candidate whose `CanConvert` is false.

Cheats from a source declaring the same Serial but a different application version are listed separately through `NclCorpusLookupResult.OtherVersionCheats`. They always carry `NclCorpusCheatCandidate.VersionProven` false and `CanConvert` false, and `NclCorpusReader.ReadZipCandidate` rejects them.

`CanConvert` still does **not** authorize an RPCS3 patch. Preview must subsequently prove the requirements in `NclConversion.md`, including exact PPU identity and address-domain binding.

## Duplicate and conflict behavior

The corpus never uses last-write-wins.

For the same exact game and cheat name, cheats are grouped by variant identity, never by name alone:

- a cheat with a proven effective-write projection is keyed by those writes, so definitions with identical byte effects collapse deterministically while retaining all source references;
- any other cheat is keyed by its verifier status and semantic SHA-256, so it is only ever distinguished, never merged;
- when one name has more than one variant, each variant is listed under the source name plus a stable 8-hex suffix derived from its variant key. Read-back validation compares the source against the original name, not the displayed one.

No variant is chosen by source order. A candidate keeps corpus SHA-256, entry SHA-256, source path, cheat index, dialect and semantic SHA-256 so Preview can revalidate the exact source state.

## Audit report

The existing audit report remains available for forensic/source analysis. It records source kind and SHA-256, file counts, modern/legacy format, semantic classifications, exact/missing filename identity, parser errors, encoding fallback, option sets, static-lowering results and bounded write counts.

## Known malformed Artemis baseline

The older authoritative audit fixture with SHA-256 `8AC628D238D0501D699552EECF40C836D417DCADD66419699DC2C56BBE6D01A6` remains a regression baseline for malformed-source classes:

- 15 files containing parser-invalid source entries;
- 16 invalid cheats;
- 27 error diagnostics;
- 0 document-level errors.

R3Config does not repair malformed source by padding odd hexadecimal data, removing unexpected punctuation, collapsing extra operands/separators, inventing a missing state line, inventing a missing second `B` line, or guessing an absent Artemis option value.

`ArtemisKnownMalformedCorpusSignatures_FailClosed` locks those fail-closed classes. A compatibility change requires source-semantic evidence before reclassification.
