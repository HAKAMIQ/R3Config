# NCL UI integration

NCL remains part of the existing Cheat section. It does not create a new product surface or a parallel mutation path.

## Primary workflow: bundled database

1. Select the target RPCS3 game.
2. Open **NCL cheats** from the Cheat section.
3. R3Config uses the selected game's exact Serial and effective application version to query the pinned bundled NCL corpus.
4. Only unambiguous verified offline-static cheats for that exact game/version are listed.
5. Choose one cheat. A single unambiguous cheat may auto-select; multiple cheats are never guessed.
6. Choose the exact PPU run key when more than one authoritative PPU executable hash exists. A single authoritative run key may auto-select.
7. Build a read-only conversion preview.
8. Apply only after confirmation.

The user does not need to locate an `.ncl` file for normal use.

Discovery and preview run asynchronously; selectors cannot change their inputs
while the operation is active. Conversion evidence is available in the read-only
technical Details expander. Game titles use content-derived direction independently
of the LTR serial/version surface. Caption controls follow the language direction
exactly as the main window's caption buttons do: Close is physically left in Arabic
and physically right in English. Form rows follow the interface direction with the
label first: `Label [field]` in English, label on the physical right in Arabic.

If matching source exists only for other application versions, the UI reports those versions as information and does not expose them as compatible cheats.

## Advanced workflow: external NCL

**Import external NCL...** remains available inside the NCL dialog for testing or a source not present in the bundled corpus.

External import retains the stricter explicit-source workflow:

1. choose the file;
2. explicitly choose Artemis PS3 or NetCheat 4.53 dialect;
3. inspect the declared Serial/version and verified static candidates;
4. choose one cheat and exact PPU run key;
5. Preview;
6. confirmed Apply.

The bundled database has a known dialect from pinned provenance. That is not dialect autoguess. External files never inherit that dialect automatically.

## Evidence and fail-closed behavior

- Bundled discovery requires exact `Serial + APP_VER/effective application version` pairing from the source filename identity.
- `VERSION` and `APP_VER` tokens are not treated as interchangeable alternatives.
- Regional serial/version groups remain paired; no Cartesian matching is allowed.
- External declared Serial/version mismatches are rejected. Missing external identity remains an explicit selected-target limitation; it is not used to infer another target.
- Same-name bundled definitions with different static semantics are hidden and reported as conflicts; no last-write-wins behavior exists.
- Only cheats that pass semantic verification and static lowering are selectable.
- The bundled ZIP, selected entry and lowered semantic fingerprint are revalidated at Preview to detect source drift.
- The PPU run key comes only from authoritative game hash evidence already exposed by the selected game descriptor.
- `RPCS3.log` must exist and `NclPpuAddressDomainEvidenceCollector` must prove the matching PPU PT_LOAD address domains.
- Conversion remains PPU/static-only. SPU, PRX, OVL, unresolved options, runtime-dependent operations and unsupported semantics remain fail-closed.

## Mutation boundary

Preview is read-only and may run while RPCS3 is active according to the existing contract.

Apply remains blocked by the existing process/mutation guard when RPCS3 owns an unsafe target state.

Both bundled and external sources converge on:

`NclManagedPatchUseCase -> PatchMutationUseCase -> PatchApplyService.ApplyTxn -> PatchFileTransactionService`

The integration writes only:

- the R3Config-managed `<SERIAL>_patch.yml`; and
- the corresponding `patch_config.yml` enable state.

It does not write official `patch.yml`, bundled `art.yml`, RPCS3 process memory, game binaries or emulator binaries.

Expected-state validation, definition-fingerprint conflict detection, backup, staged commit, read-back validation and rollback remain mandatory.

## UI regression contract

The runtime UI matrix includes the NCL minimum (660 by 560) and default
(760 by 650) sizes, with Details collapsed and expanded, plus the shared window
sizes. It initializes bundled discovery while pumping the WPF dispatcher before
checking bindings, layout, caption geometry and content/technical text direction.
Both languages and themes are exercised at 96, 120 and 144 render DPI; bitmap
rendering does not substitute for physical per-monitor DPI transition testing.

The former frozen XAML count of 825 was reconstructed exactly from the original
14-entry NCL view plus the unchanged 811-entry non-NCL baseline. The subsequent
840 count was a net increase of 15 caused entirely by replacing NCL direct text
alignment attributes with localized/technical styles and custom caption/layout
properties. It was not a change in unrelated views. The corrected NCL view adds
content-derived game-title flow and the current reviewed NCL layout, giving 43 NCL
entries and 854 total. The frozen baseline is updated only after this comparison;
the runtime checks additionally enforce the actual physical caption side.
