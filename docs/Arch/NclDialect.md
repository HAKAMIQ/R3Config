# Legacy NCL dialect compatibility

R3Config treats legacy NCL syntax as a source-format compatibility problem, not as permission to emit an RPCS3 patch.

## Dialects

`NclDialect.NetCheat453` preserves the NetCheat 4.53 semantic model used by the phase-one parser tests.

`NclDialect.ArtemisPs3` models the ArtemisPS3 source/GUI/PRX path used by the bundled `USERLIST` corpus.

The two dialects are intentionally distinct. Syntax that looks similar is not assumed to have identical runtime behavior.

## ArtemisPS3 source rules

The Artemis compatibility projection is based on the shipped ArtemisPS3 source behavior:

- modern source segments are terminated by `#`; the GUI reader treats `#` as a break and discards the remainder of that physical line,
- a modern segment is `name`, then constant-write state, then raw code/metadata lines,
- author-like text is not a structural field in the Artemis reader,
- constant write is true when the state begins with `1`, `T`, or `t`; other values are false rather than parser errors,
- `[Z]`, `[ZZ]`, and longer `Z` option tags are retained as explicit option-selection requirements,
- before execution Artemis substitutes selected option values, strips multiline comments, and removes lines rejected by its `isCodeLineValid` gate,
- non-executable descriptive text is retained as metadata and is not mislabeled as an executable opcode failure,
- Artemis opcode `6` redirects the next operation address; its PRX path does not use the NetCheat mode semantics for rewriting the next value,
- `B` find/replace permits original and replacement byte patterns of different lengths,
- malformed executable-looking hex still fails closed in R3Config rather than relying on undefined/truncating runtime behavior.

## Source encoding

The bundled Artemis corpus is decoded as strict UTF-8 first. If strict UTF-8 fails, R3Config uses an explicit Windows-1252 byte mapping. Replacement-character decoding is not accepted as conversion evidence.

The audit records each file that required the Windows-1252 fallback.

## Verification statuses

The verifier separates:

- `OfflineStaticCandidate`,
- `RuntimeEvidenceRequired`,
- `ConstantRuntimeRequired`,
- `OptionSelectionRequired`,
- `NonExecutableMetadata`,
- `Unsupported`,
- `Invalid`.

`OptionSelectionRequired` is fail-closed: no operation containing unresolved option placeholders may advance to RPCS3 conversion.

`NonExecutableMetadata` is not a cheat conversion candidate and is never emitted as an RPCS3 patch.

`OfflineStaticCandidate` still requires exact game identity, executable/module hash evidence, address mapping, and RPCS3-equivalent validation before a patch may be emitted.

## Runtime boundary

This phase adds no process memory access and no runtime writes. Runtime evidence remains a later isolated helper-process concern.
