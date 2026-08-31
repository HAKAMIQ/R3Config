# Game Setup architecture

`Game Setup` is an orchestration workflow over the existing R3Config domains. It
does not replace their parsers, compatibility policy, or transaction hardening.

## Domain boundaries

1. **Game configuration** comes from the generated RPCS3 Wiki recommendation
   catalog. Only actionable, explicitly selected `CfgChg` values may target
   `config/custom_configs/config_<SERIAL>.yml`.
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
