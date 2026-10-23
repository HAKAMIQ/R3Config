using System.IO;
using System.Text;
using R3Config.Application;
using R3Config.Mdl;
using R3Config.Svc;

namespace R3Config.Tests;

internal static class NclManagedPatchT
{
    public static void Run()
    {
        NclSequentialT.Run();
        var root = Path.Combine(
            Path.GetTempPath(),
            "R3ConfigNclManaged_" +
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(root);

        try
        {
            PreviewApplyAndRollback(root);
            DeclaredWrongVersionFailsClosed(root);
            ExternalOptionSelectionPreviewApplyParity(root);
            MissingDeclaredIdentityFailsClosed(root);
            BundledCorpusPreviewAndApply(root);
            BundledOptionCheatPreviewAndApply(root);
            BundledOptionRunKeyEligibility(root);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(
                    root,
                    recursive: true);
            }
        }
    }

    private static void PreviewApplyAndRollback(
        string root)
    {
        var config = Path.Combine(root, "config");
        var custom = Path.Combine(root, "custom_configs");
        var patches = Path.Combine(root, "patches");

        Directory.CreateDirectory(config);
        Directory.CreateDirectory(custom);
        Directory.CreateDirectory(patches);

        var install = new Rpcs3InstallDescriptor(
            RootPath: root,
            ExePath: Path.Combine(root, "rpcs3.exe"),
            CfgPath: config,
            CustPath: custom,
            PatPath: patches,
            PatCfg: Path.Combine(
                config,
                "patch_config.yml"),
            DataRoot: root);

        const string ppuHash =
            "a2c90aafce24744d5ac6da8ad009ce301458c8a7";
        const string ppuKey =
            "PPU-a2c90aafce24744d5ac6da8ad009ce301458c8a7";

        var nclPath = Path.Combine(
            root,
            "Grand Theft Auto IV Complete Edition BLES01128 01.00.ncl");
        var logPath = Path.Combine(
            root,
            "RPCS3.log");

        File.WriteAllText(
            nclPath,
            "1P Invincible Mode\n" +
            "0\n" +
            "xtatu\n" +
            "0 0066C908 419EFF40\n" +
            "#\n",
            new UTF8Encoding(false));

        File.WriteAllText(
            logPath,
            "X SYS: Serial: BLES01128\n" +
            "X ppu_loader: ** Segment: p_type=0x1, p_vaddr=0x10000, p_filesz=0xeb3548, p_memsz=0xeb3548, flags=0x400005\n" +
            "X ppu_loader: ** Segment: p_type=0x1, p_vaddr=0xed0000, p_filesz=0xcf714, p_memsz=0xa20588, flags=0x600006\n" +
            "X ppu_loader: PPU executable hash: PPU-" +
            ppuHash +
            "\n",
            new UTF8Encoding(false));

        var guard = new AllowGuard();
        var bundled = new BundledCheatPatchService();
        var mutation = BuildMutation(
            guard,
            bundled,
            new PatchApplyService());

        var useCase = new NclManagedPatchUseCase(
            bundled,
            mutation);

        var target = new NclPpuConversionTarget(
            ppuKey,
            "1P Invincible Mode",
            "Grand Theft Auto IV Complete Edition",
            "BLES01128",
            "01.00",
            "xtatu",
            "Managed NCL transaction regression");

        var inspection = useCase.Inspect(
            nclPath, NclDialect.ArtemisPs3, "BLES01128", "01.00");
        Assert(inspection.IsOk && inspection.SourceSha256?.Length == 64,
            "External inspection must retain the exact bounded source fingerprint.");

        var sourceBefore = File.ReadAllText(nclPath);
        File.WriteAllText(nclPath,
            sourceBefore.Replace("419EFF40", "60000000", StringComparison.Ordinal),
            new UTF8Encoding(false));
        var changedSource = useCase.Prepare(
            new NclPpuConversionFileRequest(
                nclPath, logPath, NclDialect.ArtemisPs3, 0, target,
                inspection.SourceSha256), install);
        Assert(!changedSource.IsOk &&
               changedSource.Code == NclManagedPatchPreviewCode.ConversionRejected &&
               changedSource.Error!.Contains("changed after inspection", StringComparison.Ordinal) &&
               !File.Exists(install.PatCfg) &&
               !File.Exists(BundledCheatPatchService.GetPath(patches, "BLES01128")),
            "Same-name/same-index external byte drift must reject without writing RPCS3 state.");
        File.WriteAllText(nclPath, sourceBefore, new UTF8Encoding(false));

        var preview = useCase.Prepare(
            new NclPpuConversionFileRequest(
                nclPath,
                logPath,
                NclDialect.ArtemisPs3,
                0,
                target,
                inspection.SourceSha256),
            install);

        var managedPath =
            BundledCheatPatchService.GetPath(
                patches,
                "BLES01128");

        Assert(
            preview.IsOk &&
            preview.Code ==
                NclManagedPatchPreviewCode.Success &&
            preview.Conversion?.Code ==
                NclPpuConversionCode.Success &&
            preview.Match?.Patch.Source ==
                PatSrc.ManagedArtemis &&
            preview.Plan?.Changes.Count == 1 &&
            preview.ManagedPreparation?.IsOk == true &&
            preview.ManagedPreparation.TgtPath.Equals(
                managedPath,
                StringComparison.OrdinalIgnoreCase) &&
            preview.ManagedPreparation.PreparedFile is not null,
            "Expected a successful read-only NCL managed patch preview.");

        Assert(
            !File.Exists(managedPath) &&
            !File.Exists(install.PatCfg),
            "Expected NCL managed preview to perform no RPCS3 writes.");

        File.WriteAllText(
            nclPath,
            sourceBefore.Replace(
                "419EFF40",
                "60000000",
                StringComparison.Ordinal),
            new UTF8Encoding(false));

        var applySourceDriftRejected = false;
        try
        {
            _ = useCase.Apply(
                preview,
                install);
        }
        catch (IOException ex) when (
            ex.Message.Contains(
                "changed after Preview",
                StringComparison.Ordinal))
        {
            applySourceDriftRejected = true;
        }

        Assert(
            applySourceDriftRejected &&
            guard.Calls == 0 &&
            !File.Exists(managedPath) &&
            !File.Exists(install.PatCfg),
            "Expected external NCL source drift after Preview to abort Apply before any RPCS3 mutation.");

        File.WriteAllText(
            nclPath,
            sourceBefore,
            new UTF8Encoding(false));

        var logBefore = File.ReadAllText(logPath);
        File.WriteAllText(
            logPath,
            "X SYS: Serial: BLES01128\n" +
            "X ppu_loader: ** Segment: p_type=0x1, p_vaddr=0x10000, p_filesz=0xeb3548, p_memsz=0xeb3548, flags=0x400005\n" +
            "X ppu_loader: PPU executable hash: PPU-bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb\n",
            new UTF8Encoding(false));

        var applyPpuDriftRejected = false;
        try
        {
            _ = useCase.Apply(
                preview,
                install);
        }
        catch (IOException ex) when (
            ex.Message.Contains(
                "PPU evidence changed after Preview",
                StringComparison.Ordinal))
        {
            applyPpuDriftRejected = true;
        }

        Assert(
            applyPpuDriftRejected &&
            guard.Calls == 0 &&
            !File.Exists(managedPath) &&
            !File.Exists(install.PatCfg),
            "Expected exact PPU evidence drift after Preview to abort Apply before any RPCS3 mutation.");

        File.WriteAllText(
            logPath,
            logBefore,
            new UTF8Encoding(false));

        var apply = useCase.Apply(
            preview,
            install);

        Assert(
            File.Exists(managedPath) &&
            File.Exists(install.PatCfg) &&
            guard.Calls > 0,
            "Expected generated NCL apply to pass through the guarded managed patch transaction.");

        var managedText = R3Bound.ReadUtf8(
            managedPath,
            R3Limits.PatCatBy,
            "managed NCL patch");

        Assert(
            managedText.StartsWith(
                BundledCheatPatchService.ManMark,
                StringComparison.Ordinal) &&
            managedText.Contains(
                "0x0066C908",
                StringComparison.OrdinalIgnoreCase),
            "Expected the title-specific managed patch to retain the verified NCL address.");

        var read = new ReadSvc().ReadText(
            managedText,
            PatSrc.ManagedArtemis,
            managedPath);

        Assert(
            read.IsOk &&
            read.Catalog.Entries.Count == 1 &&
            read.Catalog.Entries[0].RunKey.Equals(
                ppuKey,
                StringComparison.OrdinalIgnoreCase),
            "Expected the committed NCL managed patch to round-trip through ReadSvc.");

        var change =
            preview.Plan?.Changes.Single()
            ?? throw new InvalidOperationException(
                "The NCL preview lost its patch activation change.");

        var snapshot =
            new CfgRead().ReadSnap(
                install.PatCfg);

        Assert(
            snapshot.IsOn(change),
            "Expected the existing patch_config writer to enable the generated NCL definition.");

        Assert(
            Directory.Exists(
                Rpcs3AppPaths.BkpRoot(install)) &&
            apply.ChgList.Count == 1,
            "Expected the existing backup/apply transaction to report the generated NCL activation.");

        var managedBefore =
            File.ReadAllBytes(managedPath);
        var configBefore =
            File.ReadAllBytes(install.PatCfg);

        File.WriteAllText(
            nclPath,
            "Second Verified NCL Patch\n" +
            "0\n" +
            "xtatu\n" +
            "0 0066C90C 60000000\n" +
            "#\n",
            new UTF8Encoding(false));

        var secondTarget = new NclPpuConversionTarget(
            ppuKey,
            "Second Verified NCL Patch",
            "Grand Theft Auto IV Complete Edition",
            "BLES01128",
            "01.00",
            "xtatu",
            "Rollback regression");

        var secondPreview = useCase.Prepare(
            new NclPpuConversionFileRequest(
                nclPath,
                logPath,
                NclDialect.ArtemisPs3,
                0,
                secondTarget),
            install,
            [preview.Match!]);

        Assert(
            secondPreview.IsOk &&
            secondPreview.ManagedPreparation?.PreparedFile is not null,
            "Expected a second verified NCL definition to prepare against the existing managed file.");

        var commitCount = 0;
        var failingTransaction =
            new PatchFileTransactionService(
                commitFile: (stagePath, targetPath) =>
                {
                    File.Copy(
                        stagePath,
                        targetPath,
                        overwrite: true);

                    commitCount++;
                    if (commitCount == 2)
                    {
                        throw new IOException(
                            "Injected NCL managed transaction commit failure.");
                    }
                });

        var failingMutation = BuildMutation(
            new AllowGuard(),
            bundled,
            new PatchApplyService(
                transaction: failingTransaction));

        var failingUseCase =
            new NclManagedPatchUseCase(
                bundled,
                failingMutation);

        var failed = false;

        try
        {
            _ = failingUseCase.Apply(
                secondPreview,
                install);
        }
        catch (IOException ex) when (
            ex.Message.Contains(
                "Injected NCL managed transaction commit failure",
                StringComparison.Ordinal))
        {
            failed = true;
        }

        Assert(
            failed &&
            File.ReadAllBytes(managedPath)
                .SequenceEqual(managedBefore) &&
            File.ReadAllBytes(install.PatCfg)
                .SequenceEqual(configBefore),
            "Expected an injected NCL managed commit failure to roll back both the title patch and patch_config byte-for-byte.");

        var readBackCommitCount = 0;
        var readBackTransaction =
            new PatchFileTransactionService(
                commitFile: (stagePath, targetPath) =>
                {
                    File.Copy(
                        stagePath,
                        targetPath,
                        overwrite: true);

                    readBackCommitCount++;
                    if (readBackCommitCount == 1)
                    {
                        File.WriteAllBytes(
                            targetPath,
                            managedBefore);
                    }
                });

        var readBackMutation = BuildMutation(
            new AllowGuard(),
            bundled,
            new PatchApplyService(
                transaction: readBackTransaction));
        var readBackUseCase =
            new NclManagedPatchUseCase(
                bundled,
                readBackMutation);

        var readBackRejected = false;
        try
        {
            _ = readBackUseCase.Apply(
                secondPreview,
                install);
        }
        catch (IOException)
        {
            readBackRejected = true;
        }

        Assert(
            readBackRejected &&
            readBackCommitCount == 2 &&
            File.ReadAllBytes(managedPath)
                .SequenceEqual(managedBefore) &&
            File.ReadAllBytes(install.PatCfg)
                .SequenceEqual(configBefore),
            "Expected final committed-state read-back to detect a silent target mismatch and selectively roll back the other committed NCL target.");

        var unrelatedChange = new PatChg(
            "PPU-cccccccccccccccccccccccccccccccccccccccc",
            "Unrelated Manual Patch",
            "Unrelated Game",
            "BLUS99999",
            "01.00",
            PatRisk.Cheat);
        var configWithUnrelated = new CfgWrite().Build(
            install.PatCfg,
            [unrelatedChange],
            enabled: true);
        File.WriteAllText(
            install.PatCfg,
            configWithUnrelated,
            new UTF8Encoding(false));

        var disablePlan = mutation.BuildOff(
            install,
            [preview.Match!],
            [preview.Match!]);
        var remove = mutation.RemoveArtemis(
            disablePlan,
            install,
            [preview.Match!]);
        var afterRemove = new CfgRead().ReadSnap(
            install.PatCfg);

        Assert(
            remove.ChgList.Count == 1 &&
            !File.Exists(managedPath) &&
            !afterRemove.IsOn(change) &&
            afterRemove.IsOn(unrelatedChange),
            "Expected ManagedArtemis removal to delete only the NCL-owned definition/state while preserving unrelated patch_config activation state.");
    }

    private static void DeclaredWrongVersionFailsClosed(
        string root)
    {
        var caseRoot = Path.Combine(root, "gta-iv-version-mismatch");
        var install = BuildReadOnlyInstall(caseRoot);
        var nclPath = Path.Combine(
            caseRoot,
            "GTAIV-BLES01128-01.00.ncl");
        var logPath = Path.Combine(caseRoot, "RPCS3.log");
        const string ppuHash =
            "a2c90aafce24744d5ac6da8ad009ce301458c8a7";
        File.WriteAllText(
            nclPath,
            "1P Invincible Mode\n0\nxtatu\n0 0066C908 419EFF40\n#\n",
            new UTF8Encoding(false));
        File.WriteAllText(
            logPath,
            "X SYS: Serial: BLES01128\n" +
            "X ppu_loader: ** Segment: p_type=0x1, p_vaddr=0x10000, p_filesz=0xeb3548, p_memsz=0xeb3548, flags=0x400005\n" +
            "X ppu_loader: PPU executable hash: PPU-" + ppuHash + "\n",
            new UTF8Encoding(false));

        var guard = new AllowGuard();
        var bundled = new BundledCheatPatchService();
        var useCase = new NclManagedPatchUseCase(
            bundled,
            BuildMutation(
                guard,
                bundled,
                new PatchApplyService()));
        var installedGame = new Rpcs3GameDescriptor(
            "BLES01128",
            "Grand Theft Auto IV Complete Edition",
            caseRoot,
            "01.02",
            null,
            null);

        var inspection = useCase.Inspect(
            nclPath,
            NclDialect.ArtemisPs3,
            installedGame.Serial,
            installedGame.Version!);

        Assert(
            !inspection.IsOk &&
            inspection.Code == NclManagedPatchInspectionCode.SourceVersionMismatch &&
            inspection.StaticCheats.Count == 0,
            "Expected a declared BLES01128 01.00 external NCL to fail closed when the selected game is BLES01128 01.02.");

        var preview = useCase.Prepare(
            new NclPpuConversionFileRequest(
                nclPath,
                logPath,
                NclDialect.ArtemisPs3,
                0,
                new NclPpuConversionTarget(
                    "PPU-" + ppuHash,
                    "1P Invincible Mode",
                    installedGame.Title,
                    installedGame.Serial,
                    installedGame.Version!,
                    "xtatu")),
            install);

        var applyRejected = false;
        try
        {
            _ = useCase.Apply(preview, install);
        }
        catch (InvalidOperationException)
        {
            applyRejected = true;
        }

        Assert(
            !preview.IsOk &&
            preview.Code == NclManagedPatchPreviewCode.ConversionRejected &&
            applyRejected &&
            guard.Calls == 0 &&
            !File.Exists(BundledCheatPatchService.GetPath(
                install.PatPath,
                installedGame.Serial)) &&
            !File.Exists(install.PatCfg),
            "Installed BLES01128 01.02 with GTAIV-BLES01128-01.00.ncl must reject both Preview and Apply before mutation.");
    }

    private static void MissingDeclaredIdentityFailsClosed(
        string root)
    {
        var useCase = new NclManagedPatchUseCase(
            new BundledCheatPatchService(),
            BuildMutation(
                new AllowGuard(),
                new BundledCheatPatchService(),
                new PatchApplyService()));

        foreach (var fileName in new[]
                 {
                     "Unscoped.ncl",
                     "Unversioned-BLES01128.ncl"
                 })
        {
            var path = Path.Combine(root, fileName);
            File.WriteAllText(
                path,
                "Static\n0\ntester\n0 00100000 60000000\n#\n",
                new UTF8Encoding(false));

            var inspection = useCase.Inspect(
                path,
                NclDialect.ArtemisPs3,
                "BLES01128",
                "01.00");

            Assert(
                !inspection.IsOk &&
                inspection.Code == NclManagedPatchInspectionCode.InvalidInput &&
                inspection.StaticCheats.Count == 0,
                $"External NCL identity must require an exact declared Serial and APP_VER: {fileName}.");
        }
    }

    private static void ExternalOptionSelectionPreviewApplyParity(
        string root)
    {
        var caseRoot = Path.Combine(root, "external-option");
        var install = BuildReadOnlyInstall(caseRoot);
        var nclPath = Path.Combine(
            caseRoot,
            "External-Option-BLES01128-01.00.ncl");
        var logPath = Path.Combine(caseRoot, "RPCS3.log");
        const string ppuHash =
            "a2c90aafce24744d5ac6da8ad009ce301458c8a7";

        File.WriteAllText(
            nclPath,
            "External Option\n0\ntester\n" +
            "0 00100000 Z\n" +
            "[Z]60000000=NOP;419EFF40=Other[/Z]\n#\n",
            new UTF8Encoding(false));
        File.WriteAllText(
            logPath,
            "X SYS: Serial: BLES01128\n" +
            "X ppu_loader: ** Segment: p_type=0x1, p_vaddr=0x10000, p_filesz=0x200000, p_memsz=0x200000, flags=0x400005\n" +
            "X ppu_loader: PPU executable hash: PPU-" + ppuHash + "\n",
            new UTF8Encoding(false));

        var guard = new AllowGuard();
        var bundled = new BundledCheatPatchService();
        var useCase = new NclManagedPatchUseCase(
            bundled,
            BuildMutation(
                guard,
                bundled,
                new PatchApplyService()));
        var inspection = useCase.Inspect(
            nclPath,
            NclDialect.ArtemisPs3,
            "BLES01128",
            "01.00");

        Assert(
            inspection.IsOk &&
            inspection.StaticCheats.Count == 1 &&
            inspection.StaticCheats[0].RequiresOptionSelection,
            "The exact external fixture must expose one selectable option cheat.");

        var target = new NclPpuConversionTarget(
            "PPU-" + ppuHash,
            "External Option",
            "External Option Test",
            "BLES01128",
            "01.00",
            "tester");
        var withoutSelection = useCase.Prepare(
            new NclPpuConversionFileRequest(
                nclPath,
                logPath,
                NclDialect.ArtemisPs3,
                0,
                target,
                inspection.SourceSha256),
            install);
        var preview = useCase.Prepare(
            new NclPpuConversionFileRequest(
                nclPath,
                logPath,
                NclDialect.ArtemisPs3,
                0,
                target,
                inspection.SourceSha256,
                new NclOptionSelection(
                    0,
                    new Dictionary<int, string> { [1] = "419EFF40" })),
            install);

        Assert(
            !withoutSelection.IsOk &&
            withoutSelection.Code == NclManagedPatchPreviewCode.ConversionRejected &&
            preview.IsOk &&
            preview.Conversion?.Code == NclPpuConversionCode.Success &&
            EmitsWord(preview.Conversion.PatchText, 0x00100000, 0x419EFF40) &&
            !EmitsWord(preview.Conversion.PatchText, 0x00100000, 0x60000000) &&
            preview.ApplyEvidence?.OptionSelection?.ValuesByPlaceholderId[1] ==
                "419EFF40",
            "External Preview must reject a missing selection and preserve the explicit non-default option bytes.");

        var managedPath = BundledCheatPatchService.GetPath(
            install.PatPath,
            "BLES01128");
        var preparedText = preview.ManagedPreparation?.PreparedFile?.Content;
        Assert(
            !File.Exists(managedPath) &&
            !File.Exists(install.PatCfg),
            "External option Preview must remain read-only.");

        var apply = useCase.Apply(preview, install);
        var appliedText = R3Bound.ReadUtf8(
            managedPath,
            R3Limits.PatCatBy,
            "external NCL option managed patch");

        Assert(
            preparedText is not null &&
            appliedText.Equals(preparedText, StringComparison.Ordinal) &&
            EmitsWord(appliedText, 0x00100000, 0x419EFF40) &&
            !EmitsWord(appliedText, 0x00100000, 0x60000000) &&
            guard.Calls > 0 &&
            apply.ChgList.Count == 1,
            "External Apply must commit byte-for-byte the explicitly selected non-default option emitted by Preview.");
    }

    private static void BundledCorpusPreviewAndApply(
        string root)
    {
        var caseRoot = Path.Combine(root, "bundled");
        var config = Path.Combine(caseRoot, "config");
        var custom = Path.Combine(caseRoot, "custom_configs");
        var patches = Path.Combine(caseRoot, "patches");

        Directory.CreateDirectory(config);
        Directory.CreateDirectory(custom);
        Directory.CreateDirectory(patches);

        var install = new Rpcs3InstallDescriptor(
            RootPath: caseRoot,
            ExePath: Path.Combine(caseRoot, "rpcs3.exe"),
            CfgPath: config,
            CustPath: custom,
            PatPath: patches,
            PatCfg: Path.Combine(config, "patch_config.yml"),
            DataRoot: caseRoot);

        const string ppuHash =
            "a2c90aafce24744d5ac6da8ad009ce301458c8a7";
        const string ppuKey =
            "PPU-a2c90aafce24744d5ac6da8ad009ce301458c8a7";
        var logPath = Path.Combine(caseRoot, "RPCS3.log");

        File.WriteAllText(
            logPath,
            "X SYS: Serial: BLES01128\n" +
            "X ppu_loader: ** Segment: p_type=0x1, p_vaddr=0x10000, p_filesz=0xeb3548, p_memsz=0xeb3548, flags=0x400005\n" +
            "X ppu_loader: ** Segment: p_type=0x1, p_vaddr=0xed0000, p_filesz=0xcf714, p_memsz=0xa20588, flags=0x600006\n" +
            "X ppu_loader: PPU executable hash: PPU-" +
            ppuHash +
            "\n",
            new UTF8Encoding(false));

        var sourceCorpusPath = Path.Combine(
            FindRepoRoot(),
            "App",
            "Data",
            NclManagedPatchUseCase.BundledCorpusName);
        var corpusPath = Path.Combine(
            caseRoot,
            NclManagedPatchUseCase.BundledCorpusName);
        File.Copy(
            sourceCorpusPath,
            corpusPath,
            overwrite: true);

        var guard = new AllowGuard();
        var bundled = new BundledCheatPatchService();
        var mutation = BuildMutation(
            guard,
            bundled,
            new PatchApplyService());
        var useCase = new NclManagedPatchUseCase(
            bundled,
            mutation,
            corpusPath);

        var discovery = useCase.DiscoverBundled(
            "BLES01128",
            "01.02");
        var candidate = discovery.Cheats.SingleOrDefault(item =>
            item.Name.Equals(
                "Invincibility",
                StringComparison.Ordinal));

        Assert(
            discovery.IsOk &&
            discovery.Code == NclCorpusLookupCode.Success &&
            candidate is not null &&
            candidate.PrimarySource.RelativePath.Contains(
                "BLES01128 v01.00 av01.02.ncl",
                StringComparison.Ordinal),
            "Expected the pinned Artemis r6.4 corpus to discover the exact BLES01128 APP_VER 01.02 static Invincibility candidate.");

        var target = new NclPpuConversionTarget(
            ppuKey,
            candidate!.Name,
            "Grand Theft Auto IV Complete Edition",
            "BLES01128",
            "01.02",
            candidate.Author,
            candidate.Notes);

        var forgedTitle = useCase.PrepareBundled(
            new NclBundledManagedPatchRequest(
                candidate,
                logPath,
                target with { PatchTitle = "Forged title" }),
            install);
        var forgedDialect = useCase.PrepareBundled(
            new NclBundledManagedPatchRequest(
                candidate with { Dialect = NclDialect.NetCheat453 },
                logPath,
                target),
            install);

        Assert(
            !forgedTitle.IsOk &&
            forgedTitle.Code == NclManagedPatchPreviewCode.ConversionRejected &&
            !forgedDialect.IsOk &&
            forgedDialect.Code == NclManagedPatchPreviewCode.ConversionRejected,
            "Expected bundled preview to reject forged cheat identity or dialect instead of trusting caller-supplied provenance.");

        var preview = useCase.PrepareBundled(
            new NclBundledManagedPatchRequest(
                candidate,
                logPath,
                target),
            install);

        Assert(
            preview.IsOk &&
            preview.Code == NclManagedPatchPreviewCode.Success &&
            preview.Conversion?.Code == NclPpuConversionCode.Success &&
            preview.Conversion.RunKey?.Equals(
                ppuKey,
                StringComparison.OrdinalIgnoreCase) == true &&
            preview.Match?.MatSer == "BLES01128" &&
            preview.Match?.MatVer == "01.02" &&
            preview.ManagedPreparation?.IsOk == true,
            "Expected the bundled corpus candidate to reuse the verified PPU conversion and existing managed patch preparation path.");

        var managedPath = BundledCheatPatchService.GetPath(
            patches,
            "BLES01128");

        Assert(
            !File.Exists(managedPath) &&
            !File.Exists(install.PatCfg),
            "Expected bundled NCL preview to remain read-only.");

        var corpusBefore = File.ReadAllBytes(corpusPath);
        using (var stream = new FileStream(
                   corpusPath,
                   FileMode.Append,
                   FileAccess.Write,
                   FileShare.None))
        {
            stream.WriteByte(0);
        }

        var bundledApplyDriftRejected = false;
        try
        {
            _ = useCase.Apply(preview, install);
        }
        catch (IOException)
        {
            bundledApplyDriftRejected = true;
        }

        Assert(
            bundledApplyDriftRejected &&
            guard.Calls == 0 &&
            !File.Exists(managedPath) &&
            !File.Exists(install.PatCfg),
            "Expected pinned bundled corpus drift after Preview to abort Apply before any RPCS3 mutation.");

        File.WriteAllBytes(
            corpusPath,
            corpusBefore);

        var apply = useCase.Apply(preview, install);

        Assert(
            File.Exists(managedPath) &&
            File.Exists(install.PatCfg) &&
            guard.Calls > 0 &&
            apply.ChgList.Count == 1,
            "Expected bundled NCL apply to terminate in the existing guarded managed patch transaction.");

        var managedText = R3Bound.ReadUtf8(
            managedPath,
            R3Limits.PatCatBy,
            "bundled NCL managed patch");

        Assert(
            managedText.Contains(
                "0x0066A950",
                StringComparison.OrdinalIgnoreCase) &&
            managedText.Contains(
                ppuKey,
                StringComparison.OrdinalIgnoreCase),
            "Expected the bundled GTA IV Invincibility candidate to preserve its verified static address and exact PPU run key.");
    }

    // Built-in corpus option regression. App/Data/ncl.zip carries
    // "codes/God Of War Ascension BCUS98232 v01.00 av01.12.ncl", whose
    // "Combo Multiplier" cheat writes
    //     0 0067ED08 3CA0Z
    // and declares
    //     [Z]4120=10x;42C8=100x;447A=1,000x[/Z]
    // The bundled candidate must therefore be discovered as an option cheat,
    // must refuse conversion without a complete explicit selection, and must
    // emit exactly the selected half-word into the lis r5 instruction at
    // 0x0067ED08. Apply revalidates the pinned archive from scratch, so the
    // applied managed patch proves the same selection survived Preview.
    private static void BundledOptionCheatPreviewAndApply(
        string root)
    {
        var caseRoot = Path.Combine(root, "bundled-option");
        var config = Path.Combine(caseRoot, "config");
        var custom = Path.Combine(caseRoot, "custom_configs");
        var patches = Path.Combine(caseRoot, "patches");

        Directory.CreateDirectory(config);
        Directory.CreateDirectory(custom);
        Directory.CreateDirectory(patches);

        var install = new Rpcs3InstallDescriptor(
            RootPath: caseRoot,
            ExePath: Path.Combine(caseRoot, "rpcs3.exe"),
            CfgPath: config,
            CustPath: custom,
            PatPath: patches,
            PatCfg: Path.Combine(config, "patch_config.yml"),
            DataRoot: caseRoot);

        const string ppuHash =
            "b31f7c0d5e9a4468f2c10d7ab5e3946f8c2d1a07";
        const string ppuKey =
            "PPU-b31f7c0d5e9a4468f2c10d7ab5e3946f8c2d1a07";
        var logPath = Path.Combine(caseRoot, "RPCS3.log");

        File.WriteAllText(
            logPath,
            "X SYS: Serial: BCUS98232\n" +
            "X ppu_loader: ** Segment: p_type=0x1, p_vaddr=0x10000, p_filesz=0xeb3548, p_memsz=0xeb3548, flags=0x400005\n" +
            "X ppu_loader: ** Segment: p_type=0x1, p_vaddr=0xed0000, p_filesz=0xcf714, p_memsz=0xa20588, flags=0x600006\n" +
            "X ppu_loader: PPU executable hash: PPU-" +
            ppuHash +
            "\n",
            new UTF8Encoding(false));

        var sourceCorpusPath = Path.Combine(
            FindRepoRoot(),
            "App",
            "Data",
            NclManagedPatchUseCase.BundledCorpusName);
        var corpusPath = Path.Combine(
            caseRoot,
            NclManagedPatchUseCase.BundledCorpusName);
        File.Copy(
            sourceCorpusPath,
            corpusPath,
            overwrite: true);

        var guard = new AllowGuard();
        var bundled = new BundledCheatPatchService();
        var mutation = BuildMutation(
            guard,
            bundled,
            new PatchApplyService());
        var useCase = new NclManagedPatchUseCase(
            bundled,
            mutation,
            corpusPath);

        var discovery = useCase.DiscoverBundled(
            "BCUS98232",
            "01.12");
        var candidate = discovery.Cheats.SingleOrDefault(item =>
            item.Name.Equals(
                "Combo Multiplier",
                StringComparison.Ordinal));

        Assert(
            discovery.IsOk &&
            discovery.Code == NclCorpusLookupCode.Success &&
            candidate is not null &&
            candidate.PrimarySource.RelativePath.Contains(
                "BCUS98232 v01.00 av01.12.ncl",
                StringComparison.Ordinal),
            "Expected the built-in corpus to discover the BCUS98232 APP_VER 01.12 Combo Multiplier candidate instead of filtering bundled option cheats out.");

        Assert(
            candidate!.RequiresOptionSelection &&
            candidate.RequiredOptionIds.SequenceEqual([1]) &&
            candidate.RequiredOptionSets.Count == 1 &&
            candidate.RequiredOptionSets[0].PlaceholderId == 1 &&
            candidate.RequiredOptionSets[0].Values
                .Select(item => item.Value)
                .SequenceEqual(["4120", "42C8", "447A"]) &&
            candidate.RequiredOptionSets[0].Values
                .Select(item => item.Label)
                .SequenceEqual(["10x", "100x", "1,000x"]),
            "Expected the bundled option candidate to carry its referenced placeholder id and every declared choice exactly as the source declares them.");

        var cheatIndex = candidate.PrimarySource.CheatIndex;
        var target = new NclPpuConversionTarget(
            ppuKey,
            candidate.Name,
            "God Of War Ascension",
            "BCUS98232",
            "01.12",
            candidate.Author,
            candidate.Notes);

        var withoutSelection = useCase.PrepareBundled(
            new NclBundledManagedPatchRequest(
                candidate,
                logPath,
                target),
            install);

        var wrongCheat = useCase.PrepareBundled(
            new NclBundledManagedPatchRequest(
                candidate,
                logPath,
                target,
                new NclOptionSelection(
                    cheatIndex + 1,
                    new Dictionary<int, string> { [1] = "4120" })),
            install);

        var undeclaredValue = useCase.PrepareBundled(
            new NclBundledManagedPatchRequest(
                candidate,
                logPath,
                target,
                new NclOptionSelection(
                    cheatIndex,
                    new Dictionary<int, string> { [1] = "4121" })),
            install);

        Assert(
            !withoutSelection.IsOk &&
            withoutSelection.Code ==
                NclManagedPatchPreviewCode.ConversionRejected &&
            !wrongCheat.IsOk &&
            wrongCheat.Code ==
                NclManagedPatchPreviewCode.ConversionRejected &&
            !undeclaredValue.IsOk,
            "Expected a bundled option cheat to fail closed without a selection, with a selection aimed at another cheat, and with a value its option set never declared.");

        var managedPath = BundledCheatPatchService.GetPath(
            patches,
            "BCUS98232");

        Assert(
            !File.Exists(managedPath) &&
            !File.Exists(install.PatCfg),
            "Expected rejected bundled option previews to remain read-only.");

        // 0 0067ED08 3CA0Z  ->  lis r5, <selected half-word>
        (string Value, uint Word)[] expectations =
        [
            ("4120", 0x3CA04120),
            ("42C8", 0x3CA042C8),
            ("447A", 0x3CA0447A)
        ];

        NclManagedPatchPreview? hundredPreview = null;

        foreach (var (value, word) in expectations)
        {
            var preview = useCase.PrepareBundled(
                new NclBundledManagedPatchRequest(
                    candidate,
                    logPath,
                    target,
                    new NclOptionSelection(
                        cheatIndex,
                        new Dictionary<int, string> { [1] = value })),
                install);

            Assert(
                preview.IsOk &&
                preview.Code == NclManagedPatchPreviewCode.Success &&
                preview.Conversion?.Code == NclPpuConversionCode.Success &&
                preview.Conversion.RunKey?.Equals(
                    ppuKey,
                    StringComparison.OrdinalIgnoreCase) == true &&
                preview.Match?.MatSer == "BCUS98232" &&
                preview.Match?.MatVer == "01.12" &&
                preview.ManagedPreparation?.IsOk == true,
                $"Expected the bundled Combo Multiplier option {value} to convert through the existing verified PPU conversion and managed preparation path.");

            Assert(
                EmitsWord(
                    preview.Conversion!.PatchText,
                    0x0067ED08,
                    word) &&
                EmitsWord(
                    preview.Conversion!.PatchText,
                    0x0067ED04,
                    0x3C6000D8) &&
                preview.Conversion!.VerifiedByteCount == 32,
                $"Expected bundled option {value} to emit exactly the selected half-word into the lis r5 instruction at 0x0067ED08.");

            if (value == "42C8")
            {
                hundredPreview = preview;
            }
        }

        Assert(
            !File.Exists(managedPath) &&
            !File.Exists(install.PatCfg),
            "Expected bundled option preview to remain read-only.");

        var apply = useCase.Apply(hundredPreview!, install);

        Assert(
            File.Exists(managedPath) &&
            File.Exists(install.PatCfg) &&
            guard.Calls > 0 &&
            apply.ChgList.Count == 1,
            "Expected bundled option apply to terminate in the existing guarded managed patch transaction.");

        var managedText = R3Bound.ReadUtf8(
            managedPath,
            R3Limits.PatCatBy,
            "bundled NCL option managed patch");

        Assert(
            EmitsWord(
                managedText,
                0x0067ED08,
                0x3CA042C8) &&
            managedText.Contains(
                ppuKey,
                StringComparison.OrdinalIgnoreCase),
            "Expected Apply to revalidate the pinned archive with the very same option selection Preview used.");
    }

    // Real BCUS98232 field evidence. RPCS3 logged two PPU executables for this
    // session: a small file-backed module around 0x00010000..0x0002xxxx, and the
    // main game executable whose single PT_LOAD (p_vaddr 0x00010000,
    // p_filesz 0x0080E468) covers 0x0024A7BC and 0x0067ED04..0x0067ED1C.
    // Only the second can carry Combo Multiplier, and that verdict has to come
    // from address-domain evidence rather than from the title, the serial, or
    // the order of the hash list.
    private const string SmallModuleHash =
        "1f2967e954ef4713e2d37bc8ce41ed93d17413a7";
    private const string MainExecutableHash =
        "3a0b43e4a5f4bfea64f53612ee7c5d990f88129c";

    private static void BundledOptionRunKeyEligibility(
        string root)
    {
        var caseRoot = Path.Combine(root, "bundled-eligibility");
        Directory.CreateDirectory(caseRoot);

        var sourceCorpusPath = Path.Combine(
            FindRepoRoot(),
            "App",
            "Data",
            NclManagedPatchUseCase.BundledCorpusName);
        var corpusPath = Path.Combine(
            caseRoot,
            NclManagedPatchUseCase.BundledCorpusName);
        File.Copy(
            sourceCorpusPath,
            corpusPath,
            overwrite: true);

        // The real shape: only the main executable's segment is large enough.
        var fieldLog = WriteEligibilityLog(
            Path.Combine(caseRoot, "field.log"),
            "0x18000",
            "0x80e468");

        // Both executables cover every write.
        var bothLog = WriteEligibilityLog(
            Path.Combine(caseRoot, "both.log"),
            "0x80e468",
            "0x80e468");

        // Neither does.
        var noneLog = WriteEligibilityLog(
            Path.Combine(caseRoot, "none.log"),
            "0x18000",
            "0x18000");

        var bundled = new BundledCheatPatchService();
        var guard = new AllowGuard();
        var useCase = new NclManagedPatchUseCase(
            bundled,
            BuildMutation(guard, bundled, new PatchApplyService()),
            corpusPath);

        var discovery = useCase.DiscoverBundled(
            "BCUS98232",
            "01.12");
        var candidate = discovery.Cheats.SingleOrDefault(item =>
            item.Name.Equals(
                "Combo Multiplier",
                StringComparison.Ordinal));

        Assert(
            discovery.IsOk &&
            candidate is not null,
            "Expected the built-in corpus to still discover the BCUS98232 01.12 Combo Multiplier option candidate.");

        var cheatIndex = candidate!.PrimarySource.CheatIndex;
        var target = new NclPpuConversionTarget(
            string.Empty,
            candidate.Name,
            "God Of War Ascension",
            "BCUS98232",
            "01.12",
            candidate.Author,
            candidate.Notes);

        string[] candidateRunKeys =
        [
            "PPU-" + SmallModuleHash,
            "PPU-" + MainExecutableHash
        ];

        // Every declared option value is measured on its own: a different value
        // is a different resolved instance, so eligibility is recomputed rather
        // than carried over.
        string[] optionValues = ["4120", "42C8", "447A"];

        foreach (var value in optionValues)
        {
            var eligibility = useCase.DescribeBundledRunKeys(
                new NclBundledManagedPatchRequest(
                    candidate,
                    fieldLog,
                    target,
                    new NclOptionSelection(
                        cheatIndex,
                        new Dictionary<int, string> { [1] = value })),
                candidateRunKeys);

            Assert(
                eligibility.IsOk &&
                eligibility.Code == NclRunKeyEligibilityCode.Success &&
                eligibility.Candidates.Count == 2 &&
                eligibility.EligibleRunKeys.SequenceEqual(
                    ["PPU-" + MainExecutableHash]),
                $"Expected option {value} to reduce two logged PPU executables to the one whose file-backed range covers every write.");

            var excluded = eligibility.Candidates.Single(item =>
                item.RunKey.Equals(
                    "PPU-" + SmallModuleHash,
                    StringComparison.OrdinalIgnoreCase));

            Assert(
                !excluded.IsEligible &&
                excluded.EvidenceCode ==
                    NclPpuDomainEvidenceCode.Success &&
                excluded.BindingCode ==
                    NclAddressBindCode.UnmappedWrite,
                $"Expected PPU-{SmallModuleHash} to be excluded because its proven ranges do not contain the writes, not because it was missing from the log.");
        }

        var bothEligible = useCase.DescribeBundledRunKeys(
            new NclBundledManagedPatchRequest(
                candidate,
                bothLog,
                target,
                new NclOptionSelection(
                    cheatIndex,
                    new Dictionary<int, string> { [1] = "42C8" })),
            candidateRunKeys);

        Assert(
            bothEligible.IsOk &&
            bothEligible.EligibleRunKeys.Count == 2,
            "Expected two executables that both cover the writes to both stay eligible, so the choice is left to the user.");

        var noneEligible = useCase.DescribeBundledRunKeys(
            new NclBundledManagedPatchRequest(
                candidate,
                noneLog,
                target,
                new NclOptionSelection(
                    cheatIndex,
                    new Dictionary<int, string> { [1] = "42C8" })),
            candidateRunKeys);

        Assert(
            noneEligible.IsOk &&
            noneEligible.EligibleRunKeys.Count == 0 &&
            noneEligible.Candidates.All(item =>
                item.BindingCode == NclAddressBindCode.UnmappedWrite),
            "Expected zero eligible executables when no logged range covers the writes, rather than a fallback choice.");

        // Fail closed, not just unlisted: converting against an executable that
        // does not cover the writes is rejected by the same binder contract.
        var install = BuildReadOnlyInstall(caseRoot);
        var rejected = useCase.PrepareBundled(
            new NclBundledManagedPatchRequest(
                candidate,
                fieldLog,
                target with { RunKey = "PPU-" + SmallModuleHash },
                new NclOptionSelection(
                    cheatIndex,
                    new Dictionary<int, string> { [1] = "42C8" })),
            install);

        Assert(
            !rejected.IsOk &&
            rejected.Conversion?.Code ==
                NclPpuConversionCode.BindingRejected &&
            rejected.Conversion.BindingCode ==
                NclAddressBindCode.UnmappedWrite,
            "Expected an ineligible PPU executable to fail closed in Preview with the canonical binding code, not to be silently retargeted.");

        var accepted = useCase.PrepareBundled(
            new NclBundledManagedPatchRequest(
                candidate,
                fieldLog,
                target with { RunKey = "PPU-" + MainExecutableHash },
                new NclOptionSelection(
                    cheatIndex,
                    new Dictionary<int, string> { [1] = "42C8" })),
            install);

        Assert(
            accepted.IsOk &&
            accepted.Conversion?.Code == NclPpuConversionCode.Success &&
            accepted.Conversion.InstructionCount == 32 &&
            accepted.Conversion.VerifiedByteCount == 32 &&
            EmitsWord(
                accepted.Conversion.PatchText,
                0x0067ED08,
                0x3CA042C8),
            "Expected the eligible main executable to emit the resolved 8 writes / 32 bytes with the selected option half-word.");

        Assert(
            guard.Calls == 0 &&
            !File.Exists(install.PatCfg),
            "Expected PPU eligibility and preview to stay read-only.");
    }

    private static Rpcs3InstallDescriptor BuildReadOnlyInstall(
        string caseRoot)
    {
        var config = Path.Combine(caseRoot, "config");
        var custom = Path.Combine(caseRoot, "custom_configs");
        var patches = Path.Combine(caseRoot, "patches");

        Directory.CreateDirectory(config);
        Directory.CreateDirectory(custom);
        Directory.CreateDirectory(patches);

        return new Rpcs3InstallDescriptor(
            RootPath: caseRoot,
            ExePath: Path.Combine(caseRoot, "rpcs3.exe"),
            CfgPath: config,
            CustPath: custom,
            PatPath: patches,
            PatCfg: Path.Combine(config, "patch_config.yml"),
            DataRoot: caseRoot);
    }

    // Two PPU modules in one BCUS98232 session, each with its own PT_LOAD run
    // followed by its own executable hash, exactly as RPCS3 logs them.
    private static string WriteEligibilityLog(
        string path,
        string smallFileSize,
        string mainFileSize)
    {
        File.WriteAllText(
            path,
            "X SYS: Serial: BCUS98232\n" +
            "X ppu_loader: ** Segment: p_type=0x1, p_vaddr=0x10000, p_filesz=" +
            smallFileSize +
            ", p_memsz=" +
            smallFileSize +
            ", flags=0x400005\n" +
            "X ppu_loader: PPU executable hash: PPU-" +
            SmallModuleHash +
            "\n" +
            "X ppu_loader: ** Segment: p_type=0x1, p_vaddr=0x10000, p_filesz=" +
            mainFileSize +
            ", p_memsz=" +
            mainFileSize +
            ", flags=0x400005\n" +
            "X ppu_loader: PPU executable hash: PPU-" +
            MainExecutableHash +
            "\n",
            new UTF8Encoding(false));

        return path;
    }

    // Byte effects are emitted as an address token immediately followed by its
    // value token, so this reads the emitted YAML without depending on how the
    // serializer wraps the sequence.
    private static bool EmitsWord(
        string patchText,
        uint address,
        uint word)
    {
        for (var offset = 0; offset < 4; offset++)
        {
            var addressToken =
                $"0x{address + (uint)offset:X8}";
            var valueToken =
                $"0x{(byte)((word >> (24 - (offset * 8))) & 0xFFu):X2}";

            var index = patchText.IndexOf(
                addressToken,
                StringComparison.Ordinal);
            if (index < 0)
            {
                return false;
            }

            var next = patchText.IndexOf(
                "0x",
                index + addressToken.Length,
                StringComparison.Ordinal);
            if (next < 0 ||
                next + valueToken.Length > patchText.Length ||
                string.CompareOrdinal(
                    patchText,
                    next,
                    valueToken,
                    0,
                    valueToken.Length) != 0)
            {
                return false;
            }
        }

        return true;
    }

    private static string FindRepoRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "R3Cfg.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException(
            "R3Config repository root was not found for the bundled NCL regression fixture.");
    }

    private static PatchMutationUseCase BuildMutation(
        IRpcs3MutationGuard guard,
        BundledCheatPatchService bundled,
        PatchApplyService apply)
    {
        return new PatchMutationUseCase(
            new PatPlnr(),
            apply,
            new BundledCanaryPatchService(),
            bundled,
            guard);
    }

    private static void Assert(
        bool condition,
        string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(
                message);
        }
    }

    private sealed class AllowGuard :
        IRpcs3MutationGuard
    {
        public int Calls { get; private set; }

        public void CheckCan(
            Rpcs3InstallDescriptor install,
            IReadOnlyList<string> targetPaths,
            IReadOnlyList<string>? workPaths = null)
        {
            ArgumentNullException.ThrowIfNull(install);
            ArgumentNullException.ThrowIfNull(targetPaths);
            _ = workPaths;
            Calls++;
        }
    }
}
