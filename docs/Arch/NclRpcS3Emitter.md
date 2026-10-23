# NCL RPCS3 emitter

`NclRpcS3Emitter` is an offline serialization boundary. It accepts only a
successful `NclAddressBindResult`; it never discovers addresses, never reads or
writes RPCS3 process memory, and never writes RPCS3 files.

## Canonical phase-one encoding

Every verified bound byte is emitted as one RPCS3 `byte` instruction, in the
same order as the bound write set. This deliberately avoids endian
reinterpretation and keeps every emitted write visible to the existing
`PatchSelectionCompiler` static conflict decoder.

The emitter validates its output through `ReadSvc` as RPCS3 Patch version 1.2,
then independently re-reads the generated `Patch` sequence and requires exact
ordered equality of:

- RPCS3 run key
- patch title and game target
- patch address
- byte value
- instruction count

A parseable YAML document is not sufficient; byte-for-byte round-trip
equivalence is required.

## Fail-closed boundaries

The emitter rejects:

- failed or empty address-binding results
- invalid RPCS3 run keys
- missing/invalid patch or game metadata
- a single NCL cheat spanning more than one run key
- empty or out-of-range bound writes
- output exceeding the bounded YAML sequence instruction limit
- any `ReadSvc` validation failure
- any round-trip identity or byte mismatch

Multi-run-key NCL cheats remain rejected until the product has an atomic
cross-module activation owner. Emitting separate independently selectable
entries would allow partial application and is therefore not equivalent.

## Non-goals

This phase does not:

- install or merge a patch into RPCS3
- modify `patch.yml`, `imported_patch.yml`, `art.yml`, or `patch_config.yml`
- enable or disable a patch
- infer executable hashes or address domains
- handle runtime-dependent NCL operations
- publish, sign, or package R3Config
