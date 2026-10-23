# Game Setup architecture

`Game Setup` is an orchestration workflow over the existing R3Config domains. It
does not replace their parsers, compatibility policy, or transaction hardening.

## Domain boundaries

1. **Game configuration** comes from the generated RPCS3 Wiki recommendation
   catalog. Only actionable, explicitly selected `CfgChg` values may target
   `config/custom_configs/config_<SERIAL>.yml`.
   A recommendation is a real change only when it deviates from the value
   RPCS3 would use anyway. `PlanService` therefore resolves
   `EffectiveCurrentValue` in this order: per-game `config_<SERIAL>.yml`, then
   global `config/config.yml`, then the verified built-in RPCS3 default for the
   same canonical section/key. A recommendation that already matches that
   effective value is reported as applied and is never written. The built-in
   default table is read-only comparison evidence; it is not written to RPCS3
   and is not a restore-defaults implementation. An existing malformed or
   unreadable custom/global YAML file fails closed as `Unknown` instead of
   silently falling through to a default.

   `WikiRecommendationClass` remains attached to each `CfgChg` through state and
   planning. Only `MandatoryGameConfig` and `GeneralRecommendation` may enter
   ordinary Game Setup configuration decisions. `PatchSpecific`,
   `VersionSpecific`, `InformationalNote`, `Unsupported`, and `Ambiguous` are
   non-actionable unless a separate, explicitly evidenced workflow owns them.
   `RecRisk` remains an independent risk/selection signal and is not a substitute
   for recommendation class.

   `WikiGameIdBinding.Role` describes a release label on the source page; it does
   not by itself determine recommendation applicability. The generator binds an
   unrestricted setting to explicit GameIDs on that page, preserving serial and
   release-label restrictions per setting. Adjacent GameID templates retain their
   explicit release heading. Dedicated primary pages with settings take precedence;
   an empty dedicated page does not hide a uniquely evidenced shared page.
   multiple unresolved page owners, aliases without GameIDs, non-game media and
   unresolvable scope remain fail-closed. Applicable generated rows use `Primary`
   binding and the ordinary comparison/selection/Preview/Apply path. A stale or
   externally supplied `Binding != Primary` row remains non-actionable at runtime.
   There is no separate RelatedEdition UI category.
   Game Setup consumes this generated applicability instead of maintaining a fixed
   blacklist of Wiki serial conflicts across unrelated source snapshots. Conflicting
   source ownership stays excluded by the generator and cannot supply selectable
   configuration or injected Preview changes. Historical settings removed or scoped
   differently in the current source remain a reported source delta, not a reason
   to resurrect older values in the shipped catalog.
2. **Official RPCS3 patches** come from resident `patch.yml`. The file is a
   read-only RPCS3-owned input. R3Config may change only the corresponding
   enabled state in `config/patch_config.yml`.
3. **Canary/custom patches** come from the bundled Canary corpus or an explicitly
   supported custom source. Selected definitions are merged into
   `patches/imported_patch.yml`; installation and enablement are separate plan
   operations.

Patch-required settings remain attached to the exact deterministic patch
identity. Notes are retained for preview, but prose is never converted into a
setting unless an explicit, tested requirement rule exists.

## Patch requirement catalog

`App/Data/patch_requirements.json` is a separate, pinned catalog. It records
source identity and snapshot date, then matches a patch by source, title,
Serial, GameVersion, and executable hash before resolving requirements to
`GameSetupPlanningService.PatchIdentity`. The shipped rules cover the pinned
Canary God of War 3 `Unlock FPS` variants as evidence records. The current
source says only to increase VBlank to the desired framerate, so the `120`
value is marked `UNPROVEN_VALUE` and is not applied automatically. Invalid,
duplicate, over-sized, or unproven rules fail closed.

The official regression fixture is intentionally small and labelled as an
RPCS3 contract fixture. It is not a substitute for a complete resident
`patch.yml` snapshot; release acceptance still requires supplying that external
snapshot explicitly.

## Reused path

```text
GameScanner (games.yml + PARAM.SFO)
  -> PatchHashProbeService (metadata/log/gzip/cache evidence)
  -> PlanService (base configuration)
  -> ReadSvc/HCanCat (official and Canary corpora)
  -> PatchCompatibilityService (serial/version/runtime key)
  -> GameSetupPlanningService (identity, provenance, requirements, conflicts)
  -> GameSetupUseCase (preview and orchestration)
  -> PatchFileTransactionService (one guarded backup/commit/rollback)
```

### RPCS3 game-library identity

Installed-library membership is keyed by the normalized RPCS3 Serial/Game ID,
never by the display title. `games.yml` is read from RPCS3's configuration root,
while `/dev_hdd0/` is resolved independently through `vfs.yml` using RPCS3's
`$(EmulatorDir)` and `/dev_hdd0/` contract. R3Config must not reconstruct that
VFS path as `DataRoot/dev_hdd0` when RPCS3 configured another local path.

`GameScanner.Games` is the authoritative installed set. A trusted `DG` (disc)
or `HG` (HDD) `PARAM.SFO` with an exact `TITLE_ID` may establish installed-game
membership. `GD` is game data/update evidence: it never creates an installed row
by itself, but an exact-serial `GD` entry under the resolved `/dev_hdd0/game`
may raise the effective version of an already detected `DG` game. Missing,
malformed, non-local, or ambiguous VFS/identity evidence fails closed for that
source while independent `games.yml` evidence remains readable and diagnostic.
`config_<SERIAL>.yml` and recommendation-catalog entries may enrich an already
detected serial, but they never create an installed-game row by themselves. Two
installed editions with the same title remain separate when their serials differ;
a stale custom config for another region remains hidden from the RPCS3-only
library view.

The setup read/preview path creates no RPCS3 directory or file. Apply prepares
every output in memory, captures existence/SHA-256 contracts, checks the RPCS3
mutation guard before preparation and again before commit, and commits only the
custom config, `patch_config.yml`, `imported_patch.yml`, and explicitly managed
metadata required by the plan. `patch.yml` is hashed before preparation,
revalidated immediately before commit, and verified again after commit.

## Failure and verification contract

- Unknown versions never select a version-scoped patch.
- A reliable executable mismatch is blocked; absence may use the documented
  RPCS3 runtime-key guard only for a trusted, exact serial/version target.
- Group conflicts and missing/cyclic dependencies block the whole setup plan.
- Existing user patches are parsed and preserved by the managed imported-patch
  merge. Unsupported anchors or structures fail closed.
- Any external change to a transaction target aborts before overwrite.
- A commit failure rolls back only targets whose R3Config commit was attempted;
  an unknown post-commit external state is preserved and reported.
- Success is returned only after re-reading config and patch enabled state,
  re-reading installed Canary definitions, and confirming `patch.yml` is
  unchanged.
