# R3Config short-name registry

All R3Config-owned identifiers and path segments use 2 through 8 characters,
excluding extensions. `R3Config` remains the product name and root namespace.
RPCS3 filenames and schema names listed under **External contracts** are not
renamed.

## Vocabulary

| Name | Meaning | Name | Meaning |
|---|---|---|---|
| R3 | RPCS3 | App | Application |
| Case | Use case | Svc | Service |
| Vm | View model | Dlg | Dialog |
| Cmd | Command | Res | Result |
| Inf | Info | Ent | Entry |
| Man | Manifest | Snap | Snapshot |
| Cfg | Config | Pat | Patch |
| Can | Canary | HCan | H-Canary |
| Cht | Cheat | Cat | Catalog |
| Rec | Recommendation | Bkp | Backup |
| Txn | Transaction | Mut | Mutation |
| Cmp | Compatibility | Imp | Import |
| Exp | Export | Prep | Prepare |
| Stg | Stage | Rst | Restore |
| Del | Delete/remove | Vld | Validate/validation |
| Norm | Normalize | Diag | Diagnostic |
| Msg | Message | Fmt | Formatter |
| Src | Source | Tgt | Target |
| Run | Runtime/execute | Ver | Version |
| Hash | Hash | Evi | Evidence |
| Inst | Install | Scan | Scan |
| Sfo | PARAM.SFO | Game | Game |
| Plan | Plan | Apply | Apply |
| Read | Reader/read | Write | Writer/write |
| Merge | Merge | Match | Match |
| Sel | Selection/selected | Risk | Risk |
| State | State | Guard | Guard |
| Path | Path | Bound | Bounded |
| Anlz | Analysis/analyze | Auth | Authoring |
| Ext | Extraction | Fam | Family |
| Anc | Anchor | Ins | Instruction |
| Def | Definition | Val | Value |
| Port | Port/portable | Note | Note |
| Text | Text | Theme | Theme |
| Layout | Layout | Pref | Preference |
| Err | Error | Cur | Current |
| Dir | Directory | Num | Number/count |
| Max | Maximum | Min | Minimum |
| Key | Key | List | List/collection |
| Item | Item | Set | Settings/set |
| Map | Mapping/map | Sum | Summary |
| User | User-owned | Root | Root |
| Link | Link/reparse link | File | File |
| Data | Data | Meta | Metadata |
| Cache | Cache/cached | Exe | Executable |
| Yaml | YAML | Json | JSON |
| Ui | User interface/presentation | Ctr | Contract |
| Act | Activation/action | Adv | Advanced |
| Ctx | Context | Ed | Editor |
| Io | Persistence/input-output | Win | Window |
| Gm | Game (filename/message qualifier) | Lib | Library |
| Qual | Quality | Sect | Section |
| Stor | Stored/storage | Draft | Draft |
| Issue | Issue | Code | Reason/code |
| Dec | Decision | Probe | Probe |
| Limit | Limit | Sev | Severity |
| Orig | Origin | Desc | Descriptor |

## External contracts

The only name-limit exceptions are:

```text
rpcs3.exe
PARAM.SFO
patch.yml
imported_patch.yml
patch_config.yml
config_<SERIAL>.yml
<TITLE_ID>_patch.yml
RPCS3_CONFIG_DIR
```

## Path registry

| Old path | New path |
|---|---|
| `R3Config.slnx` | `R3Cfg.slnx` |
| `App/R3Config.App.csproj` | `App/R3App.csproj` |
| `Core/R3Config.Core.csproj` | `Core/R3Core.csproj` |
| `Tst/R3Config.Tests.csproj` | `Tst/R3Test.csproj` |
| `App/Application` | `App/Case` |
| `App/Composition` | `App/Comp` |
| `Core/Svc/Input` | `Core/Svc/Inp` |
| `Core/Svc/Mutation` | `Core/Svc/Mut` |
| `Core/Svc/Patch` | `Core/Svc/Pat` |
| `docs/architecture` | `docs/Arch` |
| `scripts` | `Script` |
| `App/Asset/Icon.ico` | `App/Data/Icon.ico` |
| `CHANGELOG.md` | `Changes.md` |
| `docs/PRODUCT_SPECIFICATION_AR.md` | `docs/ProdAr.md` |
| `docs/architecture/DEVELOPER_GUIDE.md` | `docs/Arch/DevGuide.md` |
| `docs/architecture/FEATURE_PLAYBOOK.md` | `docs/Arch/FeatBook.md` |
| `docs/architecture/PATCH_COMPATIBILITY.md` | `docs/Arch/PatCmp.md` |
| `docs/architecture/README.md` | `docs/Arch/README.md` |
| `docs/architecture/SAFETY_CONTRACTS.md` | `docs/Arch/SafeCtr.md` |
| `docs/architecture/TESTING_RELEASE.md` | `docs/Arch/RelTest.md` |
| `docs/architecture/UI_THEME_GUIDE.md` | `docs/Arch/UiTheme.md` |
| `scripts/Export-Source.ps1` | `Script/SrcExp.ps1` |
| `scripts/Export-Source.cmd` | `Script/SrcExp.cmd` |
| `scripts/Publish-Portable.ps1` | `Script/PortPub.ps1` |
| `scripts/Publish-Portable.cmd` | `Script/PortPub.cmd` |
| `scripts/Test-Release.ps1` | `Script/RelTest.ps1` |
| `scripts/Test-Release.cmd` | `Script/RelTest.cmd` |
| `scripts/Verify-PortableRelease.ps1` | `Script/PortChk.ps1` |
| `scripts/Verify-SourceClean.ps1` | `Script/SrcChk.ps1` |
| `scripts/Verify-SourceClean.cmd` | `Script/SrcChk.cmd` |
| `config/R3ConfigBackups` | `config/R3Bkp` |
| `patches/r3config_hcanary.json` | `patches/r3hcan.json` |
| `patches/r3config_cheats_patch.yml` | `patches/r3cht.yml` |

### Source file paths

| Old path | New path |
|---|---|
| `App/Application/AnalysisUseCase.cs` | `App/Case/Anlz.cs` |
| `App/Application/AppUseCases.cs` | `App/Case/Cases.cs` |
| `App/Application/BackupUseCase.cs` | `App/Case/Bkp.cs` |
| `App/Application/CanaryImportUseCase.cs` | `App/Case/CanImp.cs` |
| `App/Application/ConfigUseCase.cs` | `App/Case/Cfg.cs` |
| `App/Application/Contracts.cs` | `App/Case/Ctrct.cs` |
| `App/Application/HCanaryUseCase.cs` | `App/Case/HCan.cs` |
| `App/Application/IPatchCatalogMessageFormatter.cs` | `App/Case/IPatMsg.cs` |
| `App/Application/PatchCatalogUseCase.cs` | `App/Case/PatCat.cs` |
| `App/Application/PatchMutationUseCase.cs` | `App/Case/PatMut.cs` |
| `App/Application/RecommendationCatalogUseCase.cs` | `App/Case/RecCat.cs` |
| `App/Application/Rpcs3AppPaths.cs` | `App/Case/R3Path.cs` |
| `App/Composition/AppComposition.cs` | `App/Comp/AppComp.cs` |
| `App/Svc/GameScanMessages.cs` | `App/Svc/GmMsg.cs` |
| `App/Svc/PatchCatalogMessages.cs` | `App/Svc/PatMsg.cs` |
| `App/View/Dlg/AppDialog.xaml` | `App/View/Dlg/App.xaml` |
| `App/Vm/Main.Advanced.cs` | `App/Vm/Main.Adv.cs` |
| `App/Vm/Main.Config.cs` | `App/Vm/Main.Cfg.cs` |
| `App/Vm/Main.Patches.cs` | `App/Vm/Main.Pat.cs` |
| `App/Vm/Main.Presentation.cs` | `App/Vm/Main.Ui.cs` |
| `App/Vm/Main.Shell.cs` | `App/Vm/Main.Sh.cs` |
| `App/Vm/HCan.Context.cs` | `App/Vm/HCan.Ctx.cs` |
| `App/Vm/HCan.Editor.cs` | `App/Vm/HCan.Ed.cs` |
| `App/Vm/HCan.Evidence.cs` | `App/Vm/HCan.Evi.cs` |
| `App/Vm/HCan.Persistence.cs` | `App/Vm/HCan.Io.cs` |
| `Core/Mdl/Canary.cs` | `Core/Mdl/Can.cs` |
| `Core/Mdl/Patch.cs` | `Core/Mdl/Pat.cs` |
| `Core/Mdl/PatchCompatibility.cs` | `Core/Mdl/PatCmp.cs` |
| `Core/Mdl/RState.cs` | `Core/Mdl/RecSt.cs` |
| `Core/Svc/Input/Bounded.cs` | `Core/Svc/Inp/Bnd.cs` |
| `Core/Svc/Mutation/Guard.cs` | `Core/Svc/Mut/Guard.cs` |
| `Core/Svc/Patch/Activation.cs` | `Core/Svc/Pat/Act.cs` |
| `Core/Svc/Patch/Apply.cs` | `Core/Svc/Pat/Apply.cs` |
| `Core/Svc/Patch/Cat.cs` | `Core/Svc/Pat/Cat.cs` |
| `Core/Svc/Patch/Compatibility.cs` | `Core/Svc/Pat/Cmp.cs` |
| `Core/Svc/Patch/HCan.cs` | `Core/Svc/Pat/HCan.cs` |
| `Core/Svc/Patch/Hash.cs` | `Core/Svc/Pat/Hash.cs` |
| `Core/Svc/Patch/Match.cs` | `Core/Svc/Pat/Match.cs` |
| `Core/Svc/Patch/Merge.cs` | `Core/Svc/Pat/Merge.cs` |
| `Core/Svc/Patch/Plan.cs` | `Core/Svc/Pat/Plan.cs` |
| `Core/Svc/Patch/Read.cs` | `Core/Svc/Pat/Read.cs` |
| `Core/Svc/Patch/Risk.cs` | `Core/Svc/Pat/Risk.cs` |
| `Core/Svc/Patch/Select.cs` | `Core/Svc/Pat/Select.cs` |
| `Core/Svc/Patch/Txn.cs` | `Core/Svc/Pat/Txn.cs` |
| `Core/Svc/Patch/Version.cs` | `Core/Svc/Pat/Ver.cs` |
| `Core/Svc/Patch/Write.cs` | `Core/Svc/Pat/Write.cs` |

Source-file and symbol mappings are appended to this registry before each
semantic rename layer is applied.

## App case and composition symbols

| Old type/namespace | New type/namespace |
|---|---|
| `R3Config.UseCases` | `R3Config.Case` |
| `AnalysisUseCase` | `AnlzCase` |
| `AppUseCases` | `AppCases` |
| `BackupUseCase` | `BkpCase` |
| `CanaryImportUseCase` | `CanImp` |
| `ConfigUseCase` | `CfgCase` |
| `CatLoadResult` | `CatLoad` |
| `PatchCatResult` | `PatCatRs` |
| `PatchCatalogSourceStatus` | `PatSrcSt` |
| `GamePlanRow` | `GameRow` |
| `AnalyzeResult` | `AnlzRes` |
| `HCanaryUseCase` | `HCanCase` |
| `IPatchCatalogMessageFormatter` | `IPatMsg` |
| `PatchCatalogUseCase` | `PatCat` |
| `PatchMutationUseCase` | `PatMut` |
| `RecommendationCatalogUseCase` | `RecCat` |
| `Rpcs3AppPaths` | `R3Paths` |
| `R3Config.Composition` | `R3Config.Comp` |
| `AppComposition` | `AppComp` |
| `AppRuntimeServices` | `AppRun` |

## Core model symbols

| Old type | New type | Old type | New type |
|---|---|---|---|
| `BackupFileEntry` | `BkpEntry` | `BackupManifest` | `BkpMan` |
| `BackupSnapshot` | `BkpSnap` | `CanaryDiagnosticSeverity` | `CanSev` |
| `CanaryComplexity` | `CanCmp` | `CanaryCatalogDiagnostic` | `CanDiag` |
| `CanaryInstruction` | `CanInst` | `CanaryConfigValue` | `CanVal` |
| `CanaryPatchDefinition` | `CanDef` | `CanaryCatalog` | `CanCat` |
| `CanaryCatalogResult` | `CanCatRs` | `CanaryPatchFamily` | `CanFam` |
| `CanaryPortStatus` | `PortStat` | `CanaryPortAnalysis` | `PortAnlz` |
| `CanaryCatalogReport` | `CanRpt` | `HCanaryPatchType` | `HCanType` |
| `HCanaryDraftStatus` | `DraftSt` | `HCanaryHash` | `HCanHash` |
| `HCanaryHashReadResult` | `HashRead` | `HCanaryInstruction` | `HCanInst` |
| `HCanaryGameTarget` | `HCanTgt` | `HCanaryAnchor` | `HCanAnc` |
| `HCanaryPatchDraft` | `HCanDrft` | `HCanaryValidationIssue` | `VldIssue` |
| `HCanaryValidationResult` | `VldRes` | `HCanaryStoredPatch` | `HCanStor` |
| `HCanarySaveResult` | `SaveRes` | `CatalogInfo` | `CatInfo` |
| `ConfigPlanEntry` | `CfgEntry` | `ConfigPlan` | `CfgPlan` |
| `ConfigApplyResult` | `CfgRes` | `PatchHashKind` | `HashKind` |
| `PatchHashEvidenceKind` | `HashEvi` | `Rpcs3ExecutableHash` | `ExeHash` |
| `PatchSourceOrigin` | `PatSrc` | `PatchRisk` | `PatRisk` |
| `PatchMatchStatus` | `MatStat` | `Rpcs3PatchGameTarget` | `R3PatTgt` |
| `Rpcs3PatchEntry` | `PatEntry` | `Rpcs3PatchCatalog` | `PatCat` |
| `PatchCatalogDiagnostic` | `PatDiag` | `PatchCatalogReadResult` | `PatRead` |
| `Rpcs3PatchMatchResult` | `PatMatch` | `PatchApplyChange` | `PatChg` |
| `PatchApplyPlan` | `PatPlan` | `PatchPreparedFileKind` | `PrepKind` |
| `PatchPreparedFile` | `PrepFile` | `PatchApplyResult` | `PatRes` |
| `PatchHashProbeResult` | `HashRes` | `PatchCompatibilityVerdict` | `CmpVerd` |
| `PatchCompatibilityReasonCode` | `CmpCode` | `PatchCompatibilityDecision` | `CmpDec` |
| `Rpcs3PatchRuntimeKeyInfo` | `KeyInf` | `Rpcs3PatchRuntimeKey` | `RunKey` |
| `RecStateItem` | `RecItem` | `RecStateSummary` | `RecSum` |
| `RecommendationRisk` | `RecRisk` | `ConfigChange` | `CfgChg` |
| `WikiGameRecommendation` | `WikiRec` | `Rpcs3InputLimits` | `R3Limits` |
| `GameScanDiagnosticCode` | `ScanCode` | `GameScanDiagnosticSeverity` | `ScanSev` |
| `GameScanSourceKind` | `ScanSrc` | `GameScanDiagnostic` | `ScanDiag` |
| `GameScanResult` | `ScanRes` | `Rpcs3InstallDescriptor` | `R3Inst` |
| `Rpcs3GameDescriptor` | `R3Game` | `ParamSfoInfo` | `SfoInfo` |

## Core service symbols

| Old type | New type | Old type | New type |
|---|---|---|---|
| `BackupService` | `BkpSvc` | `ValidatedBackupEntry` | `VldBkp` |
| `RestoreOperation` | `RstOp` | `CatInfoService` | `CatSvc` |
| `CatalogMissingException` | `MissEx` | `CatalogInvalidException` | `CatBadEx` |
| `RecommendationCatalogService` | `RecSvc` | `RecommendationCatalogDto` | `RecDto` |
| `WikiRecommendationService` | `WikiSvc` | `ConfigApplyService` | `CfgApply` |
| `CfgValueNorm` | `CfgNorm` | `PlanService` | `PlanSvc` |
| `Rpcs3ConfigYaml` | `R3Yaml` | `MemoryStatusEx` | `MemoryEx` |
| `BoundedDiagnosticList` | `DiagList` | `Rpcs3BoundedInput` | `R3Bound` |
| `Rpcs3MutationBlockReason` | `MutBlock` | `Rpcs3MutationBlockedException` | `MutBlkEx` |
| `IRpcs3MutationGuard` | `IMutGd` | `Rpcs3CanonicalPath` | `CanPath` |
| `Rpcs3MutationGuard` | `MutGuard` | `CanonicalInstallRoots` | `CanRoots` |
| `PatchActivationPolicy` | `PatAct` | `PatchApplyService` | `PatApply` |
| `PatchCatalogReader` | `ReadSvc` | `MappingScope` | `MapScope` |
| `PatchCompatibilityService` | `PatCmp` | `Candidate` | `Cand` |
| `PatchHashProbeService` | `HashPrb` | `MetadataCandidate` | `MetaCand` |
| `CachedMetadata` | `MetaCach` | `CacheProbe` | `CachePrb` |
| `HCanaryCatalogService` | `HCanCat` | `HCanaryFamilyIndex` | `FamIndex` |
| `HCanaryPortEvidenceService` | `PortEvi` | `HCanaryHashExtractionService` | `HashExt` |
| `HCanaryPatchAuthoringService` | `PatAuth` | `LocalMetadata` | `LocMeta` |
| `PatchMatchService` | `MatchSvc` | `PatchCatalogMergeService` | `PatMerge` |
| `PatchApplyPlanner` | `PatPlnr` | `PatchConfigReader` | `CfgRead` |
| `PatchConfigSnapshot` | `CfgSnap` | `PatchRiskPolicy` | `PatPol` |
| `PatchSelectionCompileResult` | `SelRes` | `PatchSelectionCompiler` | `SelComp` |
| `SelectionKey` | `SelKey` | `SelectionKeyComparer` | `KeyCmp` |
| `SourceCandidate` | `SrcCand` | `PatchTransactionPhase` | `PatPhase` |
| `PatchPathValidator` | `PathVld` | `PatchFileTransactionService` | `PatTxn` |
| `StagedPatchFile` | `StgFile` | `Rpcs3PatchVersion` | `PatVer` |
| `PatchConfigWriter` | `CfgWrite` | `RecommendationRiskPolicy` | `RecPol` |
| `RecStateService` | `StateSvc` | `GameScanner` | `GmScan` |
| `Rpcs3InstallService` | `InstSvc` | `ParamSfoReader` | `SfoRead` |
| `ParamSfoProbeResult` | `SfoProbe` |  |  |

Additional registered Core abbreviations: `Calc` (calculate), `Cand`
(candidate), `Cpx` (complexity), `Loc` (localized), `Plnr` (planner), `Pol`
(policy), `Prb` (probe), `Ser` (serial), `Unk` (unknown), `Miss` (missing),
`Bad` (invalid/mismatch),
`Made` (created), `Mod` (module), `Arg` (argument), `Allow` (allowed),
`Deps` (dependencies), `Vals` (values), `Print` (fingerprint), `Auto`
(automatic), `Class` (classification), `Why` (reason), `Sim` (similar),
`Gen` (generated), `Cpu` (logical processor), `Mem` (memory), `Fall`
(fallback), `Keep` (kept), `Drop` (dropped), `Mat` (matched), `Chg`
(change), `Raw` (source values), `Auth` (authoritative), `Diff`
(different), `Tech` (technical), `Cust` (custom), `Time` (timestamp),
`Utc` (UTC), `Idx` (index), and `Det` (detected).

## App service symbols

| Old type | New type | Old type | New type |
|---|---|---|---|
| `AppSettings` | `AppSet` | `AppSettingsService` | `SetSvc` |
| `BundledCanaryPatchService` | `CanBndl` | `BundledCheatPatchService` | `ChtBndl` |
| `PatchLibraryStageResult` | `LibStRes` | `CanaryPatchNormalizeResult` | `CanNorm` |
| `CanaryPatchWriteResult` | `CanWrite` | `CanaryPatchRemoveResult` | `CanDel` |
| `CanaryPatchStageResult` | `CanStage` | `CanaryPatchImportService` | `ImpSvc` |
| `PatchHashTextSection` | `HashSect` | `ManagedBlockScan` | `BlkScan` |
| `LocalizedGameScanDiagnosticFormatter` | `GmDiag` | `UiDialogKind` | `DlgKind` |
| `UiLayoutState` | `UiLaySt` | `UiLayoutService` | `UiLaySvc` |
| `PatchNoteTranslationQuality` | `NoteQual` | `PatchNoteTranslationResult` | `NoteRes` |
| `PatchNoteTranslationService` | `NoteSvc` | `LocalizedNoteText` | `NoteText` |
| `PatchTextLocalizer` | `PatText` | `LocalizedPatchCatalogMessageFormatter` | `PatMsgFm` |
| `UiPrefsData` | `PrefData` | `WindowThemeService` | `WinTheme` |

Additional registered App service abbreviations: `Bndl` (bundled), `Blk`
(block), `Ppu` (PPU), `Merge` (merged), `Rx` (regular expression), `Top`
(top-level), `Head` (header), `Arab` (Arabic), `Trans` (translation), `Wide`
(width), `Algn` (alignment), `Hor` (horizontal), `Rght` (right), `Dwm`
(Desktop Window Manager), and `Dark` (dark mode).

## View-model symbols

| Old type | New type | Old type | New type |
|---|---|---|---|
| `AdvancedVm` | `AdvVm` | `CanaryPatchImportVm` | `CanImpVm` |
| `GameItemVm` | `GameVm` | `GameListVm` | `ListVm` |
| `BulkObservableCollection` | `BulkList` | `HCanaryVm` | `HCanVm` |
| `HCanaryHashVm` | `HashVm` | `HCanaryGameTargetVm` | `TgtVm` |
| `HCanaryInstructionVm` | `InsVm` | `HCanaryAnchorVm` | `AncVm` |
| `PlanItemVm` | `PlanItVm` | `PatchItemVm` | `PatItVm` |
| `PatchPreviewVm` | `PatPrev` | `PatchSurfaceState` | `PatSurf` |
| `PatchTargetGroupVm` | `TgtGrpVm` | `RelayCommand` | `RelayCmd` |
| `AsyncRelayCommand` | `AsyncCmd` | `PatchSourceKind` | `PatSrcKd` |
| `PatchSection` | `PatSect` |  |  |

Additional registered ViewModel abbreviations: `Dsp` (display), `Tip`
(tooltip), `Warn` (warning), `Ofcl` (official), `Begin` (beginner), `Surf`
(surface), `Det` (details), `Mgr` (manager), `Rfsh` (refresh), `Wide`
(width), `Col` (column), `Algn` (alignment), and `Rank` (rank).

## View, control, shell, and language symbols

| Old type | New type | Old type | New type |
|---|---|---|---|
| `ActionBarView` | `ActBar` | `GamesListView` | `GameList` |
| `HCanaryView` | `HCanView` | `PatchViewMode` | `PatMode` |
| `PatchView` | `PatView` | `RecPlanView` | `RecView` |
| `StatusBarView` | `StatBar` | `AdvancedDialog` | `AdvDlg` |
| `CanaryPatchImportDialog` | `CanDlg` | `MainWindow` | `MainWin` |
| `TextExtension` | `TextExt` | `AppDialog` | `AppDlg` |

Additional registered UI abbreviations: `Prop` (dependency property), `Cap`
(caption), `Drag` (window drag), `Click` (click handler), `Alt`
(secondary/alternative), and `Tog` (toggle).

## Test symbols

| Old type/property | New type/property | Old type/property | New type/property |
|---|---|---|---|
| `HCanTests` | `HCanTst` | `GameScanState` | `ScanStat` |
| `CfgPlanState` | `PlanStat` | `RecordingSynchronizationContext` | `SyncCtx` |
| `FakeRpcs3Root` | `FakeRoot` | `ConfigPath` | `CfgPath` |
| `GameFolder` | `GameDir` | `WikiXmlPath` | `WikiPath` |
| `Recommendation` | `Rec` | `SelectedChanges` | `SelChg` |
| `_postCount` | `_posts` | `PostCount` | `Posts` |

| Old method | New method | Old method | New method |
|---|---|---|---|
| `RunHashExtractionTests` | `Hash_Ext` | `RunValidationTests` | `Vld_All` |
| `RunYamlTests` | `Yaml_All` | `RunImportEditTests` | `Imp_Edit` |
| `RunTransactionTests` | `Txn_All` | `RunViewModelTests` | `Vm_All` |
| `RunLocalizationAndViewTests` | `Ui_Lang` | `ValidDraft` | `GoodDrft` |
| `ExpectIssue` | `HasIssue` | `FindRepoRoot` | `RepoRoot` |
| `AssertThrows` | `MustFail` | `RunLangKeyTests` | `Lang_Key` |
| `RunMutationGuardTests` | `MutGuard` | `RunInputLimitTests` | `Inp_Lim` |
| `BuildYamlItems` | `YamlIts` | `RunGameScanDiagnosticTests` | `Scan_Src` |
| `DiagnosticIdentity` | `DiagId` | `RunMutationCommandStateTests` | `Mut_Cmd` |
| `SetPrivateField` | `SetField` | `CreateInputLimitInstall` | `LimInst` |
| `WriteSingleEntrySfo` | `OneSfo` | `WriteUtf8FileAtExactLength` | `Utf8Len` |
| `RunMutationGuardReparseTests` | `Mut_Link` | `CreateDirectoryJunction` | `MakeLink` |
| `RunArchitectureBoundaryTests` | `Arch_Svc` | `RunCanaryCompositionTests` | `Can_Comp` |
| `ReadLangFile` | `LangRead` | `RunAppPatchPresentationSourceTests` | `App_Pat` |
| `RunPatchCatalogReaderDuplicateTopLevelHashTests` | `Cat_Dup` | `RunPatchCompatibilityArchitectureTests` | `Pat_Cmp` |
| `CreatePatch` | `NewPatch` | `RunBundledCheatCompilerTests` | `Cht_Bndl` |
| `RunPatchTransactionTests` | `Pat_Txn` | `RunCanaryImportTransactionTests` | `Can_Txn` |
| `BuildCanaryImportPatch` | `CanPat` | `CountOccurrences` | `CountOf` |
| `RunBackupRestoreTransactionTests` | `Bkp_Rst` | `CreateBackupSnapshot` | `BkpSnap` |
| `WriteBackupManifest` | `BkpMan` | `ApplyRestoreOperationForTest` | `RstTest` |
| `HasRestoreStages` | `HasRst` | `BuildTransactionPatchCatalog` | `TxnCat` |
| `RunGameListSearchTests` | `ListFind` | `RunAdvancedDialogXamlSmokeTest` | `Adv_Xaml` |
| `CreateEnv` | `MakeEnv` | `RunHwTests` | `Hw_All` |
| `RunGameScanTests` | `GameScan` | `RunCatTests` | `Cat_All` |
| `RunAnalysisReadPathTests` | `AnlzPath` | `RunRecStateTests` | `Rec_Stat` |
| `RunPlanVmVisibilityTests` | `Plan_Ui` | `RunBackupTests` | `Bkp_All` |
| `RunCfgApplyTests` | `Cfg_All` | `RunCanaryImportFileContractTests` | `Can_Imp` |
| `RunPatchTests` | `Pat_All` | `RunRpcs3PatchContractTests` | `R3_Pat` |
| `RunAppFlowPatchCatalogLoadSmokeTests` | `Flow_Cat` | `RunReadOnlyRpcs3InstallContract` | `R3_Read` |
| `ComputeFileSha256` | `FileHash` | `BuildRecStateRecommendation` | `RecBuild` |
| `BuildManualTimingRecommendation` | `ManlRec` | `BuildLowEndBlockedRecommendation` | `LowRec` |
| `BuildWikiXml` | `WikiXml` | `BuildWrongRiskCatalogJson` | `RiskJson` |
| `BuildDuplicateJsonKeyCatalogJson` | `DupKeyJs` | `BuildDuplicateSerialCatalogJson` | `DupSerJs` |
| `BuildDuplicateSettingCatalogJson` | `DupSetJs` | `BuildDuplicateTopLevelHashPatchYml` | `DupHash` |
| `BuildPatchYml` | `PatYaml` | `BuildImportedPatchYml` | `ImpYaml` |
| `BuildImportedPatchDuplicateKeyYml` | `ImpDup` | `BuildExistingPatchConfigYml` | `CfgYaml` |
| `WriteInvalidOffsetParamSfo` | `BadSfo` | `WriteParamSfo` | `SfoWrite` |
| `EscapeYamlPath` | `EscPath` | `AssertMutationBlocked` | `MutFails` |
| `CanExecute` | `CanRun` |  |  |
| `PropertyChanged` | `PropChg` | `CanExecuteChanged` | `CanChg` |

Additional registered test abbreviations: `Arch` (architecture), `Dup`
(duplicate), `Js` (JSON fixture), `Lim` (limit), `Manl` (manual), and `Utf8`
(UTF-8).
