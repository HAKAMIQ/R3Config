# NCL managed patch integration

Bundled discovery and external-file import are two read-only source paths into the same conversion/application use case. Neither source mode has an NCL-specific RPCS3 writer.

Both converge on the existing mutation boundary:

`external NCL file` or `pinned bundled candidate`
-> `NclPpuConversionService`
-> `ReadSvc` managed round-trip
-> exact `PatMatch`
-> existing `PatchMutationUseCase.BuildOn`
-> existing `BundledCheatPatchService.PrepGenerated`
-> preview with `PatchPreparedFile`
-> explicit apply
-> existing `PatchMutationUseCase.ApplyPreparedManaged`
-> `IRpcs3MutationGuard`
-> existing `PatchApplyService.ApplyTxn`
-> existing `PatchFileTransactionService`
-> backup / staged validation / expected-state validation / commit / read-back / rollback.

## External source snapshot

Inspection retains a SHA-256 of the bounded external source bytes. The UI passes
that expected fingerprint into Preview. Preview opens the source with read-only
sharing, reads at most 2 MiB, checks the fingerprint and converts that same
in-memory snapshot. A same-name/same-index cheat with changed bytes is rejected;
there is no second path read between validation and conversion. Apply reopens the
same canonical source path, rechecks the exact source SHA-256 and declared target,
re-runs the read-only conversion against the current exact PPU/PT_LOAD evidence,
and requires the regenerated RPCS3 patch bytes and conversion identity to equal
the reviewed Preview before entering the mutation boundary.

The focused executable mode is `R3Test.exe --ncl-only`; its semantic suite also
runs `NclManagedPatchT` (including external source drift and transaction rollback).

## Bundled candidate preparation

`DiscoverBundled` queries the configured pinned corpus path and SHA-256 using exact selected Serial/application-version identity.

`PrepareBundled` does not trust the previously returned UI object by itself. Before conversion it revalidates through `NclCorpusReader.ReadZipCandidate`:

- bundled corpus SHA-256;
- source-entry path and SHA-256;
- exact selected Serial;
- exact selected effective application version;
- cheat index and exact source name;
- pinned Artemis dialect;
- offline-static semantic classification;
- lowered static semantic SHA-256.

Caller-supplied bundled cheat title or dialect changes are rejected. Notes are taken again from the revalidated source cheat rather than trusted from the UI candidate. Generated YAML uses `Author: HAKAMIQ`; `Source Author` retains the original author when the parser exposes an independent Author field, rather than accepting a caller-provided author. The Artemis dialect retains its body metadata in Notes and does not infer an Author from those lines. Existing sibling authors and provenance are not replaced.

The NCL entry stays inside the ZIP; it is not extracted to a temporary source file. `ConvertTextFile` parses the already revalidated text while `NclPpuAddressDomainEvidenceCollector.CollectFile` continues to gather bounded read-only PPU PT_LOAD evidence from the real RPCS3 log path. Apply repeats the pinned corpus/candidate validation and the read-only conversion, then requires the regenerated patch bytes and exact conversion identity to match the reviewed Preview before mutation.

## Generated managed state

Generated definitions are written only to:

`<RPCS3 patches>/<SERIAL>_patch.yml`

They never modify official `patch.yml`, `imported_patch.yml`, shared manual/Canary state, bundled `art.yml`, RPCS3 process memory, game binaries or emulator binaries.

`PrepGenerated` accepts exactly one already validated Patch 1.2 definition. The container remains `<SERIAL>_patch.yml`, with independent `RunKey|Title` entries. Game title is target metadata, not a container identity. The existing selection compiler validates executable definitions; its projected output must not replace an existing document. `PatchSelectionCompiler.MergeGeneratedNcl` adds the definition to the existing YAML mapping, preserving sibling mappings and unknown fields. Serialization may change YAML layout; node values are preserved. Reapply retains the original document text when the identity, Games metadata, patch version and instructions agree. A duplicate identity with different target metadata or instructions fails closed, including equal instruction bytes targeting another application version. No implicit cross-version union or hash migration is performed.

Even an idempotent definition remains a prepared transaction target with its original SHA-256/existence precondition. Otherwise a no-op Preview could activate a definition that changed externally afterward. Each fresh Preview reads the managed target and captures new patch-config state; no previous generated catalog is cached in the use case. The dialog retains its enabled-match input for planner context, permits one successful Apply, and reloads the main catalog/config preview on successful close. The config writer still loads actual disk YAML and modifies only the selected activation path. Instruction order inside each cheat is preserved; independent cheat insertion order does not change the final definition set.

Apply accepts only the existing managed source contract, requires the prepared target to equal the exact serial-specific path, and delegates writes to the guarded transaction. `PatchFileTransactionService` validates every staged file and expected preimage, commits the complete transaction, verifies the final committed state from disk, and routes any read-back mismatch through the existing selective rollback path.

## Regression coverage

`R3Test.exe --ncl-sequential-only` runs `NclSequentialT`, also registered through `NclManagedPatchT` in the NCL and full suites. Synthetic integration cases cover A/B/C addition, reapply, fresh/stale Preview, unknown and manual siblings, activation-state preservation, disabled A, serial/version/PPU isolation, reload, second-commit rollback, duplicate conflicts and insertion-order independence. The pinned bundled BCUS98103 Ammo/Health case uses the reported PPU key with synthetic PT_LOAD log evidence in a temporary installation; it is not live RPCS3 execution evidence.

The pre-change sequential baseline passed A/B/C and the bundled Ammo/Health case without a resident official collision. It exposed four failures: unknown-root loss, cross-version identity reuse, missing concurrency validation on no-op definitions, and generated Author.

Read-only field inspection subsequently found an official `Infinite Health` under the same reported PPU key. Official output encodes one `be32`; NCL encodes four `byte` operations. `DefPrint` hashes instruction structure, so these representations differ. Applying B against temporary copies of the resident files reproduces `RPCS3 runtime identity collision ... between ManagedArtemis and Official` in `PatchMutationUseCase.VerifyRuntimeIdentityCollisions`, before transaction/serialization/activation. A's presence is incidental: the synthetic `CrossSourceRuntimeIdentity_RejectsBeforeCommit_WithOrWithoutA` reproduces the rejection in both states. The current plain field log no longer contains the selected game's PPU/PT_LOAD session, so the isolated collision reproduction uses synthetic log evidence, not a live emulator session.

This cross-source guard remains intact. Supporting both definitions requires an explicit generated-runtime-identity policy; treating structurally different definitions as equal alone is insufficient because RPCS3 also uses the resident Games metadata and source precedence. The additive guarantee here applies to compatible, non-colliding runtime identities. The field conflict is not claimed resolved by the preservation/concurrency corrections.

The NCL regression suite must prove:

- external preview performs no RPCS3 writes;
- a declared wrong external version fails closed;
- external source drift after Preview aborts Apply before mutation;
- exact PPU/log evidence drift after Preview aborts Apply before mutation;
- exact bundled `Serial + APP_VER` discovery reaches the same conversion pipeline;
- forged bundled title/dialect is rejected;
- bundled preview performs no RPCS3 writes;
- pinned bundled-corpus drift after Preview aborts Apply before mutation;
- bundled Apply traverses the existing guarded managed transaction;
- patch-config activation uses the existing writer;
- backup/rollback behavior remains shared;
- an injected second-file commit failure restores managed patch and `patch_config.yml` byte-for-byte;
- a silent committed-state mismatch is detected by final read-back and selectively rolled back;
- ManagedArtemis removal deletes only the NCL-owned definition/state and preserves unrelated `patch_config.yml` activation state.
