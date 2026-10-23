# NCL PPU conversion service

`NclPpuConversionService` is the fail-closed orchestration boundary for the proven main-PPU static NCL subset.

The text path composes:

`NclParser.ReadText`
-> `NclSemanticVerifier.Verify`
-> selected cheat by explicit zero-based index
-> `NclStaticLowerer.Lower`
-> `NclPpuAddressDomainEvidenceCollector.CollectText`
-> `NclAddressBinder.Bind`
-> `NclRpcS3Emitter.Emit`

The file path uses the equivalent bounded file readers:

`NclParser.Read`
-> semantic verification
-> static lowering
-> `NclPpuAddressDomainEvidenceCollector.CollectFile`
-> address binding
-> RPCS3 Patch 1.2 emission.

A conversion succeeds only when every downstream result is `Success`. Runtime-dependent NCL operations, option-dependent cheats, malformed cheats, stale or mismatched PPU evidence, unmapped writes, cross-domain writes, ambiguous address evidence, metadata failures, and emitter round-trip failures remain rejected.

The service returns patch text only. It does not write `patch.yml`, `imported_patch.yml`, `patch_config.yml`, the Artemis catalog, RPCS3 memory, or any RPCS3 process state.

Cheat selection is explicit by parsed zero-based index. The service does not infer a cheat from title text and does not collapse duplicate cheat names.

PPU evidence remains limited to the exact runtime PPU hash and file-backed PT_LOAD ranges (`p_vaddr` + `p_filesz`). No PPU rule is generalized to SPU, PRX, or OVL.