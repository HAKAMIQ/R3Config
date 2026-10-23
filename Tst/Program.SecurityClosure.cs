using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml.Linq;
using R3Config.Application;
using R3Config.Mdl;
using R3Config.Svc;
using R3Config.Tests;

internal static partial class Program
{
    // Regression cases for the v0.1.0 release-readiness security findings.
    // Each case reproduces one defect in isolation; `--security-closure-only`
    // runs every case independently so several failures can be shown at once.
    static readonly (string Name, Action<string> Test)[] SecurityClosureCases =
    [
        (nameof(Sec_PatchYmlJunctionRoot), Sec_PatchYmlJunctionRoot),
        (nameof(Sec_WriterLockThreadAffinity), Sec_WriterLockThreadAffinity),
        (nameof(Sec_CfgPreCommitBackup), Sec_CfgPreCommitBackup),
        (nameof(Sec_PatPreCommitBackup), Sec_PatPreCommitBackup),
        (nameof(Sec_CatExportTarget), Sec_CatExportTarget),
        (nameof(Sec_PortChkPayload), _ => Sec_PortChkPayload()),
        (nameof(Sec_ScriptLocalPaths), _ => Sec_ScriptLocalPaths())
    ];

    static void RunSecurityClosure(
        TestRunner runner,
        string tempRoot)
    {
        foreach (var (name, test) in SecurityClosureCases)
        {
            runner.RunCase(name, () => test(tempRoot));
        }
    }

    static bool TryRunSecurityClosureMode(
        string[] args,
        TestContext context)
    {
        if (args.Length != 1 ||
            !args[0].Equals(
                "--security-closure-only",
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var tempRoot = context.CreateTempRoot("R3ConfigSecurityClosure");
        var passed = 0;
        var failed = 0;
        foreach (var (name, test) in SecurityClosureCases)
        {
            try
            {
                test(tempRoot);
                passed++;
                Console.WriteLine($"[PASS] {name}");
            }
            catch (Exception exception)
            {
                failed++;
                TestRunner.ReportFailure(name, exception);
            }
        }

        Console.WriteLine(
            $"Security closure focused suite. Passed: {passed}, Failed: {failed}, Skipped: 0");
        if (failed > 0)
        {
            Environment.ExitCode = 1;
        }

        return true;
    }

    static void Sec_PatchYmlJunctionRoot(string tempRoot)
    {
        if (!OperatingSystem.IsWindows())
        {
            Console.WriteLine("patch.yml junction-root case SKIP (Windows-only).");
            return;
        }

        var suiteRoot = Path.Combine(tempRoot, "patch-yml-junction-root");
        var realRoot = Path.Combine(suiteRoot, "real");
        Directory.CreateDirectory(Path.Combine(realRoot, "config"));
        Directory.CreateDirectory(Path.Combine(realRoot, "patches"));
        File.WriteAllBytes(Path.Combine(realRoot, "rpcs3.exe"), []);
        var officialPath = Path.Combine(realRoot, "patches", "patch.yml");
        var officialBytes = Encoding.UTF8.GetBytes("Version: 1.2\n");
        File.WriteAllBytes(officialPath, officialBytes);

        var linkedRoot = Path.Combine(suiteRoot, "linked");
        MakeLink(linkedRoot, realRoot);
        try
        {
            var linkedConfig = Path.Combine(linkedRoot, "config");
            var linkedPatches = Path.Combine(linkedRoot, "patches");
            var install = new Rpcs3InstallDescriptor(
                linkedRoot,
                Path.Combine(linkedRoot, "rpcs3.exe"),
                linkedConfig,
                Path.Combine(linkedConfig, "custom_configs"),
                linkedPatches,
                Path.Combine(linkedConfig, "patch_config.yml"),
                linkedRoot);
            var guard = new Rpcs3MutationGuard(() => false);

            guard.CheckCan(
                install,
                [Path.Combine(linkedPatches, "imported_patch.yml")]);
            MutFails(
                () => guard.CheckCan(
                    install,
                    [Path.Combine(linkedPatches, "patch.yml")]),
                MutBlock.Tgt,
                "Expected the official patch.yml to stay read-only when the RPCS3 root is reached through a junction.");
            MutFails(
                () => guard.CheckCan(
                    install,
                    [Path.Combine(linkedPatches, "PATCH.YML")]),
                MutBlock.Tgt,
                "Expected the junction-root patch.yml check to ignore file-name case.");
            Assert(File.ReadAllBytes(officialPath).SequenceEqual(officialBytes),
                "Expected the junction-root patch.yml checks to leave the official catalog untouched.");
        }
        finally
        {
            Directory.Delete(linkedRoot);
        }

        Console.WriteLine("patch.yml read-only guard through a junctioned RPCS3 root PASS.");
    }

    static void Sec_WriterLockThreadAffinity(string tempRoot)
    {
        var guard = new Rpcs3MutationGuard(() => false);

        var held = GuardInstall(tempRoot, "writer-lock-owner-exit");
        RunOnThread(() => guard.CheckCan(
            held,
            [Path.Combine(held.PatPath, "imported_patch.yml")]));
        var (acquired, abandoned) = ProbeWriterLock(held.DataRoot!);
        Assert(!abandoned,
            "Expected the cross-process writer lock to survive the exit of the thread that first checked a mutation.");
        Assert(!acquired,
            "Expected another holder to be refused while this process still owns the RPCS3 writer lock.");
        RunOnThread(() => guard.CheckCan(
            held,
            [Path.Combine(held.PatPath, "imported_patch.yml")]));

        var contended = GuardInstall(tempRoot, "writer-lock-contended");
        using var foreign = new Mutex(
            initiallyOwned: false,
            WriterLockName(contended.DataRoot!));
        Assert(foreign.WaitOne(0),
            "Expected the test to own the contended writer lock.");
        try
        {
            Exception? failure = null;
            RunOnThread(() =>
            {
                try
                {
                    guard.CheckCan(
                        contended,
                        [Path.Combine(contended.PatPath, "imported_patch.yml")]);
                }
                catch (Rpcs3MutationBlockedException exception)
                {
                    failure = exception;
                }
            });
            Assert(failure is Rpcs3MutationBlockedException { Reason: MutBlock.Locked },
                "Expected a writer lock held by another owner to block the mutation with MutBlock.Locked.");
        }
        finally
        {
            foreign.ReleaseMutex();
        }

        // Another object type holding the predictable Global\ name makes
        // `new Mutex` throw WaitHandleCannotBeOpenedException; it must block
        // the mutation instead of escaping the lock-holder thread.
        var squatted = GuardInstall(tempRoot, "writer-lock-squatted");
        using var squatter = new EventWaitHandle(
            initialState: false,
            EventResetMode.ManualReset,
            WriterLockName(squatted.DataRoot!));
        MutFails(
            () => guard.CheckCan(
                squatted,
                [Path.Combine(squatted.PatPath, "imported_patch.yml")]),
            MutBlock.Locked,
            "Expected a writer lock name held by a non-mutex object to block the mutation with MutBlock.Locked.");

        Console.WriteLine("Writer lock ownership survives the checking thread and still blocks other owners PASS.");
    }

    static void Sec_CfgPreCommitBackup(string tempRoot)
    {
        var root = Path.Combine(tempRoot, "config-precommit-backup");
        var exePath = Path.Combine(root, "rpcs3.exe");
        Directory.CreateDirectory(Path.Combine(root, "config", "custom_configs"));
        File.WriteAllBytes(exePath, []);
        var install = Rpcs3InstallService.FromExe(exePath);
        var target = Path.Combine(install.CustPath, "config_TEST00003.yml");
        const string original = "Video:\n  Write Color Buffers: false\n";
        File.WriteAllText(target, original);
        var backupRoot = Rpcs3AppPaths.BkpRoot(install);

        var applyChange = new CfgChg(
            "Video",
            "Write Color Buffers",
            "true",
            "Write Color Buffers",
            null,
            R3Config.Mdl.RecRisk.Required);
        var applyPlan = SecCfgPlan(install, target, applyChange);
        Throws(
            () => new ConfigApplyService().Apply(
                applyPlan,
                [applyChange],
                backupRoot,
                FailAtCommit),
            "Expected the simulated commit-phase validation failure to stop config apply.");
        Assert(File.ReadAllText(target) == original,
            "Expected a config apply that failed before commit to keep the target unchanged.");
        Assert(SnapshotCount(backupRoot) == 0,
            "Expected a config apply that failed before commit to leave no orphan backup snapshot.");

        var removeChange = applyChange with
        {
            Value = "false"
        };
        var removePlan = SecCfgPlan(install, target, removeChange);
        Throws(
            () => new ConfigApplyService().Remove(
                removePlan,
                [removeChange],
                backupRoot,
                FailAtCommit),
            "Expected the simulated commit-phase validation failure to stop config removal.");
        Assert(File.ReadAllText(target) == original,
            "Expected a config removal that failed before commit to keep the target unchanged.");
        Assert(SnapshotCount(backupRoot) == 0,
            "Expected a config removal that failed before commit to leave no orphan backup snapshot.");

        new ConfigApplyService().Apply(
            applyPlan,
            [applyChange],
            backupRoot);
        Assert(SnapshotCount(backupRoot) == 1,
            "Expected a committed config apply to keep exactly its backup snapshot.");

        Console.WriteLine("Config apply/remove discard the backup snapshot when nothing was committed PASS.");
    }

    static void Sec_PatPreCommitBackup(string tempRoot)
    {
        var root = Path.Combine(tempRoot, "patch-precommit-backup");
        var exePath = Path.Combine(root, "rpcs3.exe");
        Directory.CreateDirectory(Path.Combine(root, "patches"));
        File.WriteAllBytes(exePath, []);
        var install = Rpcs3InstallService.FromExe(exePath);
        var target = Path.Combine(install.PatPath, "imported_patch.yml");
        var original = TxnCat(
            new string('b', 40),
            "Original Precommit Patch",
            "Precommit Game",
            "BLUS30004",
            "01.00",
            "0x00002040");
        File.WriteAllText(target, original);
        var originalBytes = File.ReadAllBytes(target);
        var backupRoot = Rpcs3AppPaths.BkpRoot(install);
        PatchPreparedFile Prepared()
        {
            return PatchFileTransactionService.CaptureExpectedState(
                new PatchPreparedFile(
                    target,
                    TxnCat(
                        new string('b', 40),
                        "Updated Precommit Patch",
                        "Precommit Game",
                        "BLUS30004",
                        "01.00",
                        "0x00002044"),
                    PrepKind.PatCat,
                    PatSrc.Imported,
                    EntNum: 1));
        }

        Throws(
            () => new PatchFileTransactionService().Commit(
                backupRoot,
                [Prepared()],
                FailAtCommit),
            "Expected the simulated commit-phase validation failure to stop the patch transaction.");
        Assert(File.ReadAllBytes(target).SequenceEqual(originalBytes),
            "Expected a patch transaction that failed before commit to keep the target unchanged.");
        Assert(SnapshotCount(backupRoot) == 0,
            "Expected a patch transaction that failed before commit to leave no orphan backup snapshot.");
        Assert(!Directory.EnumerateFiles(install.PatPath, "*.r3stage").Any(),
            "Expected a patch transaction that failed before commit to clean its stage files.");

        new PatchFileTransactionService().Commit(
            backupRoot,
            [Prepared()]);
        Assert(SnapshotCount(backupRoot) == 1,
            "Expected a committed patch transaction to keep exactly its backup snapshot.");

        Console.WriteLine("Patch transaction discards the backup snapshot when nothing was committed PASS.");
    }

    static void Sec_CatExportTarget(string tempRoot)
    {
        var directory = Path.Combine(tempRoot, "catalog-export");
        Directory.CreateDirectory(directory);
        var catalog = RecSvc.FromRec(
            "RPCS3 Wiki snapshot",
            "2026-06-13",
            [],
            new DateTimeOffset(2026, 6, 13, 0, 0, 0, TimeSpan.Zero),
            new string('a', 64));

        var official = Path.Combine(directory, "patch.yml");
        const string officialText = "Version: 1.2\n";
        File.WriteAllText(official, officialText);
        MustFail<InvalidDataException>(
            () => RecommendationCatalogUseCase.Export(catalog, official),
            "Expected catalog export to refuse a YAML target such as patch.yml.");
        Assert(File.ReadAllText(official) == officialText,
            "Expected a refused catalog export to leave the existing file unchanged.");

        var extensionless = Path.Combine(directory, "patch");
        MustFail<InvalidDataException>(
            () => RecommendationCatalogUseCase.Export(catalog, extensionless),
            "Expected catalog export to refuse a target without the .json extension.");
        Assert(!File.Exists(extensionless),
            "Expected a refused catalog export not to create a file.");

        if (OperatingSystem.IsWindows())
        {
            var hardLink = Path.Combine(directory, "linked-export.json");
            MakeHardLink(hardLink, official);
            RecommendationCatalogUseCase.Export(catalog, hardLink);
            Assert(File.ReadAllText(official) == officialText,
                "Expected catalog export through a .json hardlink to leave the linked patch.yml unchanged.");
            Assert(new FileInfo(hardLink).Length > 0 &&
                   File.ReadAllText(hardLink) != officialText,
                "Expected catalog export to replace the hardlinked .json name with its own file.");
        }

        var exported = Path.Combine(directory, "cat-export.JSON");
        RecommendationCatalogUseCase.Export(catalog, exported);
        Assert(File.Exists(exported) && new FileInfo(exported).Length > 0,
            "Expected catalog export to a .json target to succeed.");

        Console.WriteLine("Catalog export only writes .json targets PASS.");
    }

    static void Sec_PortChkPayload()
    {
        var repoRoot = RepoRoot();
        var project = XDocument.Load(Path.Combine(repoRoot, "App", "R3App.csproj"));
        var shipped = project
            .Descendants()
            .Where(element => element.Name.LocalName is "Content" or "None" &&
                              element.Attribute("CopyToPublishDirectory") is not null)
            .Select(element =>
                (string?)element.Attribute("Link") ??
                (string?)element.Attribute("Include") ??
                string.Empty)
            .Select(path => path.Replace('\\', '/'))
            .Where(path => path.Length > 0)
            .ToArray();
        Assert(shipped.Contains("Data/wiki-notes.json", StringComparer.OrdinalIgnoreCase) &&
               shipped.Contains("LICENSE", StringComparer.OrdinalIgnoreCase),
            "Expected the published payload list to be read from App/R3App.csproj.");

        var portCheck = File.ReadAllText(Path.Combine(repoRoot, "Script", "PortChk.ps1"));
        var requiredBlock = Regex.Match(
            portCheck,
            @"\$requiredFiles = @\((?<body>.*?)\r?\n\)",
            RegexOptions.Singleline);
        Assert(requiredBlock.Success,
            "Expected PortChk.ps1 to declare its $requiredFiles list.");
        var required = Regex.Matches(requiredBlock.Groups["body"].Value, "'([^']+)'")
            .Select(match => match.Groups[1].Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missing = shipped
            .Where(path => !required.Contains(path))
            .ToArray();
        Assert(missing.Length == 0,
            $"PortChk.ps1 does not require every published payload file: {string.Join(", ", missing)}");
        Assert(required.Contains("Wpf.Ui.dll") &&
               required.Contains("Wpf.Ui.Abstractions.dll"),
            "Expected PortChk.ps1 to require the WPF-UI assemblies that ship beside the single-file executable.");
        Assert(portCheck.Contains("GNU GENERAL PUBLIC LICENSE", StringComparison.Ordinal) &&
               portCheck.Contains("Version 3, 29 June 2007", StringComparison.Ordinal),
            "Expected PortChk.ps1 to verify that LICENSE carries the GNU GPL v3 text.");

        Console.WriteLine($"PortChk requires all {shipped.Length} published payload files and the GPL-3.0 LICENSE PASS.");
    }

    static void Sec_ScriptLocalPaths()
    {
        var scriptRoot = Path.Combine(RepoRoot(), "Script");
        var userProfilePath = new Regex(
            @"(?i)\b[A-Z]:[\\/]+Users[\\/]",
            RegexOptions.CultureInvariant);
        var leaks = Directory
            .EnumerateFiles(scriptRoot, "*", SearchOption.AllDirectories)
            .Where(path => !path.Split(Path.DirectorySeparatorChar)
                .Any(segment => segment is "bin" or "obj" or "node_modules"))
            .Where(path => Path.GetExtension(path).ToLowerInvariant() is
                ".ps1" or ".cmd" or ".js" or ".cs" or ".md" or ".json" or ".csv")
            .Where(path => userProfilePath.IsMatch(File.ReadAllText(path)))
            .Select(path => Path.GetRelativePath(scriptRoot, path))
            .ToArray();
        Assert(leaks.Length == 0,
            $"Repository scripts must not hardcode a user-profile path: {string.Join(", ", leaks)}");

        var driveLiteral = new Regex(
            @"['""][A-Za-z]:[\\/]",
            RegexOptions.CultureInvariant);
        var historyLeaks = Directory
            .EnumerateFiles(Path.Combine(scriptRoot, "Rpcs3History"), "*.js")
            .Where(path => driveLiteral.IsMatch(File.ReadAllText(path)))
            .Select(Path.GetFileName)
            .ToArray();
        Assert(historyLeaks.Length == 0,
            $"Rpcs3History scripts must take the frozen RPCS3 repository as an argument instead of a hardcoded drive path: {string.Join(", ", historyLeaks)}");

        Console.WriteLine("Repository scripts carry no hardcoded local profile or drive paths PASS.");
    }

    static Rpcs3InstallDescriptor GuardInstall(
        string tempRoot,
        string name)
    {
        var root = Path.Combine(tempRoot, name);
        Directory.CreateDirectory(Path.Combine(root, "config"));
        Directory.CreateDirectory(Path.Combine(root, "patches"));
        var exePath = Path.Combine(root, "rpcs3.exe");
        File.WriteAllBytes(exePath, []);
        return Rpcs3InstallService.FromExe(exePath);
    }

    static void MakeHardLink(
        string linkPath,
        string targetPath)
    {
        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = Path.Combine(Environment.SystemDirectory, "cmd.exe"),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var argument in new[] { "/d", "/c", "mklink", "/H", linkPath, targetPath })
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = System.Diagnostics.Process.Start(startInfo)
            ?? throw new InvalidOperationException(
                "Could not start cmd.exe to create a test hardlink.");
        var standardOutput = process.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0 || !File.Exists(linkPath))
        {
            throw new InvalidOperationException(
                "Creating a required Windows test hardlink failed. " +
                $"Exit code: {process.ExitCode}. Output: {standardOutput} Error: {standardError}");
        }
    }

    static string WriterLockName(string dataRoot)
    {
        var canonicalRoot = RuntimePathPolicy.RequireLocal(
            CanPath.GetPath(dataRoot),
            "RPCS3 data root");
        var rootHash = Convert.ToHexString(
            SHA256.HashData(
                Encoding.UTF8.GetBytes(canonicalRoot.ToUpperInvariant())));
        return $@"Global\HAKAMIQ.R3Config.Writer.{rootHash}";
    }

    static (bool Acquired, bool Abandoned) ProbeWriterLock(string dataRoot)
    {
        using var probe = new Mutex(
            initiallyOwned: false,
            WriterLockName(dataRoot));
        bool acquired;
        var abandoned = false;
        try
        {
            acquired = probe.WaitOne(0);
        }
        catch (AbandonedMutexException)
        {
            acquired = true;
            abandoned = true;
        }

        if (acquired)
        {
            probe.ReleaseMutex();
        }

        return (acquired, abandoned);
    }

    static void RunOnThread(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.Start();
        thread.Join();
        if (failure is not null)
        {
            throw new InvalidOperationException(
                "A dedicated test thread failed.",
                failure);
        }
    }

    static void FailAtCommit(
        PatPhase phase,
        string backupRootPath,
        IReadOnlyList<string> targetPaths)
    {
        if (phase == PatPhase.Commit)
        {
            throw new IOException("Simulated commit-phase validation failure.");
        }
    }

    static int SnapshotCount(string backupRoot)
    {
        return Directory.Exists(backupRoot)
            ? Directory.EnumerateFileSystemEntries(backupRoot).Count()
            : 0;
    }

    static void Throws(
        Action action,
        string message)
    {
        try
        {
            action();
        }
        catch (Exception)
        {
            return;
        }

        throw new InvalidOperationException(message);
    }

    static CfgPlan SecCfgPlan(
        Rpcs3InstallDescriptor install,
        string target,
        CfgChg change)
    {
        var serial = Path.GetFileNameWithoutExtension(target)["config_".Length..];
        var state = new RecItem(
            serial,
            change.Path,
            "false",
            change.Value,
            change.Risk,
            RecState.Pending,
            IsDiff: true,
            IsAction: true,
            IsSel: true,
            Notes: string.Empty);
        return new CfgPlan(
            serial,
            "Security Closure Game",
            target,
            Path.Combine(install.CfgPath, "config.yml"),
            [new CfgEntry(change, "false", state)],
            new RecSum(
                serial,
                1,
                0,
                1,
                0,
                0,
                0,
                [state]),
            NeedBkp: true);
    }
}
