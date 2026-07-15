# R3Config naming and contract registry

This registry records the names that form the maintained architecture and the
external or persistent contracts of R3Config. It deliberately defines no
numeric identifier-length limit. Prefer, in order: an externally required
name, the .NET/WPF convention, the architectural role, and then the clearest
unambiguous local name.

## Current architecture

- Application orchestration: `App/Application/` in `R3Config.Application`.
- Dependency composition: `App/Composition/AppComposition.cs` in
  `R3Config.Composition`.
- Core models: `Core/Mdl/` in `R3Config.Mdl`.
- Core services: `Core/Svc/` in `R3Config.Svc`, with the clear domain folders
  `Core/Svc/Patch/`, `Core/Svc/Mutation/`, and `Core/Svc/Input/`.
- WPF state and presentation: `App/Vm/`, `App/View/`, and `App/Ctrl/`.
- Executable regression runner: `Tst/`.
- Source and release tooling: `Script/`.
- Architecture documentation: `docs/Arch/`.

The historical folders "App/Case" and "App/Comp" are retired. Their current
locations are `App/Application/` and `App/Composition/` respectively.

## Restored application and composition names

| Retired identifier | Current identifier |
| --- | --- |
| AnlzCase | `AnalysisUseCase` |
| AppCases | `AppUseCases` |
| BkpCase | `BackupUseCase` |
| CanImp (application use case) | `CanaryImportUseCase` |
| CfgCase | `ConfigUseCase` |
| HCanCase | `HCanaryUseCase` |
| PatCat (application use case) | `PatchCatalogUseCase` |
| PatMut | `PatchMutationUseCase` |
| RecCat (application use case) | `RecommendationCatalogUseCase` |
| AppComp | `AppComposition` |
| AppRun | `AppRuntimeServices` |
| CatLoad | `CatalogLoadResult` |
| PatCatRs | `PatchCatalogResult` |
| PatSrcSt | `PatchCatalogSourceStatus` |
| GameRow | `GamePlanRow` |
| AnlzRes | `AnalyzeResult` |
| IPatMsg | `IPatchCatalogMessageFormatter` |
| R3Paths | `Rpcs3AppPaths` |

## Restored service names

| Retired identifier | Current identifier |
| --- | --- |
| PatCmp | `PatchCompatibilityService` |
| PatTxn | `PatchFileTransactionService` |
| MutGuard | `Rpcs3MutationGuard` |
| IMutGd | `IRpcs3MutationGuard` |
| ImpSvc | `CanaryPatchImportService` |
| CanBndl | `BundledCanaryPatchService` |
| ChtBndl | `BundledCheatPatchService` |
| BkpSvc | `BackupService` |
| GmScan | `GameScanner` |
| InstSvc | `Rpcs3InstallService` |
| HashPrb | `PatchHashProbeService` |
| PatMerge | `PatchCatalogMergeService` |
| SelComp | `PatchSelectionCompiler` |
| PatApply | `PatchApplyService` |
| CfgApply | `ConfigApplyService` |
| PlanSvc | `PlanService` |
| MatchSvc | `PatchMatchService` |
| PatAuth | `HCanaryPatchAuthoringService` |
| PatMsgFm | `LocalizedPatchCatalogMessageFormatter` |

## Restored domain contract names

| Retired identifier | Current identifier |
| --- | --- |
| R3Inst | `Rpcs3InstallDescriptor` |
| R3Game | `Rpcs3GameDescriptor` |
| CmpDec | `PatchCompatibilityDecision` |
| CmpVerd | `PatchCompatibilityVerdict` |
| CmpCode | `PatchCompatibilityReasonCode` |
| PatEntry | `Rpcs3PatchEntry` |
| PatPlan | `PatchApplyPlan` |
| PrepFile | `PatchPreparedFile` |
| ScanDiag | `GameScanDiagnostic` |
| ScanRes | `GameScanResult` |
| BkpEntry | `BackupFileEntry` |
| BkpMan | `BackupManifest` |
| BkpSnap | `BackupSnapshot` |

## Intentionally retained concise names

These names remain because their role is established and unambiguous in their
own layer; expanding them would not improve a public or architectural contract.

| Name | Reason |
| --- | --- |
| `R3Config` | Product name and root namespace. |
| `MainVm`, `AdvVm`, `PlanVm` | Conventional WPF ViewModel suffix with a clear view context. |
| `AppFlow` | Stable compatibility facade used by presentation code. |
| `UiText`, `UiDlg` | Small presentation adapters with explicit UI scope. |
| `CfgPlan`, `HashKind`, `PatRisk` | Established domain terms that remain clear in `R3Config.Mdl`. |
| `SaveCmd`, `ApplyCmd` | Conventional command properties in ViewModels. |
| `IsBusy`, `CanSave` | Conventional boolean state properties. |

Concise private implementation names may remain when their containing type and
scope make the meaning obvious. They are not a license to abbreviate public
architecture or external contracts.

## RPCS3 external contracts

These values are owned by RPCS3 or an external format and must remain exact:

- `rpcs3.exe`
- `PARAM.SFO`
- `patch.yml`
- `imported_patch.yml`
- `patch_config.yml`
- `config_<SERIAL>.yml`
- `<TITLE_ID>_patch.yml`
- `RPCS3_CONFIG_DIR`

External YAML/JSON schema keys are also preserved verbatim.

## R3Config persistent names and migration

R3Config reads both the current and legacy names, writes only the current name,
and never automatically deletes the legacy source:

| Data | Current name | Legacy read name |
| --- | --- | --- |
| Backup root | `config/R3Bkp/` | `config/R3ConfigBackups/` |
| H-Canary store | `patches/r3hcan.json` | `patches/r3config_hcanary.json` |
| Cheat migration source | `patches/r3cht.yml` | `patches/r3config_cheats_patch.yml` |

The arbitrary cheat migration files are not RPCS3 boot inputs. Selected cheats
are compiled into the managed title-specific `<TITLE_ID>_patch.yml` file that
RPCS3 loads.

## Verification policy

`Script/NameChk.ps1` enforces the maintained contracts rather than identifier
length. It verifies:

- required architecture paths, namespaces, and public types;
- absence of retired architecture identifiers outside this history registry;
- public type collisions and one-character public API names;
- XAML class/resource consistency;
- documented paths and important documented types;
- RPCS3 external names and both sides of persistent-name migration;
- absence of a reintroduced numeric naming rule.

`Script/SrcChk.ps1` runs this contract gate for both a working tree and an
exported source ZIP.
