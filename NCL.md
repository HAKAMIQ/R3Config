# Bundled NCL corpus

R3Config ships a read-only NCL source corpus for game-driven cheat discovery.
The corpus is separate from `App/Data/art.yml`; it remains legacy Artemis/NetCheat
source material and is converted only through the existing fail-closed NCL pipeline.

## Provenance

- R3Config verified merged NCL corpus
- Source lineage: 5 records
- Source NCL files: 2910
- Bundled artifact: App/Data/ncl.zip
- Bundled artifact SHA-256: C4C70C6E73A225F1E15096A3AEA06DD05461F7EF0CBB4BE8B106577ABC22D8B8
- Bundled dialect: ArtemisPs3
- Embedded provenance schema: 2
- Upstream ArtemisPS3 license notice: LICENSE-ArtemisPS3.txt
- Embedded provenance record: PROVENANCE.json

Source lineage recorded by `PROVENANCE.json`:

- `BUCANERO_CURRENT`: upstream `docs/cheatdb.zip`, SHA-256 `8289EFDB5B5441A579C1ECAC8E608852FAB4D225A92728B5EB8D6227ED89EBA2`, 2540 NCL files.
- `ALDOSTOOLS_CODELIST`: upstream `_Projects_/codelists` archive, SHA-256 `A1ABC379DD6F8BB65399EACD97D147A33C5907BAFE2C1E0B468BA86A786E8CEA`, 2501 NCL files.
- `DNAWRKSHP_HISTORICAL`: historical ArtemisPS3 archive, SHA-256 `8AC628D238D0501D699552EECF40C836D417DCADD66419699DC2C56BBE6D01A6`, 1287 NCL files.
- `R3CONFIG_CURRENT`: legacy carry-forward lineage from the former shipped corpus, SHA-256 `80AA918A4F98A7491A0D684456D7620486296C54B396FEA0C89D117CBF5DD17D`, 2497 NCL files. A current rebuilt corpus may be used as a carrier input but is not treated as independent upstream evidence.
- `AR_SITE_CODELIST_2026`: site-scraped Artemis codelist download, SHA-256 `734896CB7DD1EEE2B9E6720E6E75D9D852626F213C8676B03914257DF8555DC3`, 2495 NCL files. Every source entry is stored as `Items/<RowOrder>/source.ncl`; per-entry identity (RowOrder/Title/Serials/Version/MatchMode) is recorded in an embedded `manifest.json` instead of the physical filename.

`ncl.zip` contains exactly 2910 `.ncl` entries plus the root
`LICENSE-ArtemisPS3.txt` upstream license notice and `PROVENANCE.json` schema-2
lineage record. The license notice is provenance for upstream ArtemisPS3 material;
it is not a blanket license claim for every independently sourced cheat definition.

R3Config does not treat every entry as compatible or applicable. Discovery requires
exact Serial + application version identity; conversion still requires a verified
static cheat (or an option cheat whose values the user chose explicitly), an exact PPU run key, RPCS3 PT_LOAD address-domain evidence,
byte-equivalent Patch 1.2 emission, and the existing guarded patch transaction.

Same-name definitions with different semantics are listed as separate variants
with a stable suffix rather than selected by source order. Missing or ambiguous identity is not
promoted into the bundled compatible-cheat list. External `.ncl` import remains a
separate advanced workflow and is not allowed to overwrite this pinned corpus.
