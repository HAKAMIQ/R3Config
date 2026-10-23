using System.IO;
using System.IO.Compression;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;

using System.Text;
using System.Text.Json;
using System.Threading;
using System.Xml;
using System.Windows;
using R3Config;
using R3Config.Svc;
using R3Config.Lang;
using R3Config.Mdl;
using R3Config.Vm;
using R3Config.View;
using R3Config.Application;
using R3Config.ArtImport;
using R3Config.Composition;
using R3Config.Tests;

internal static partial class Program
{
    static void Inp_Lim(string tempRoot)
    {
        Assert(R3Limits.Bytes == 16L * 1024 * 1024 &&
               R3Limits.Entries == 4_096 &&
               R3Limits.SfoKeyBy == 256 &&
               R3Limits.SfoValBy == 4U * 1024 * 1024 &&
               R3Limits.GameByte == 32L * 1024 * 1024 &&
               R3Limits.PatCatBy == 128L * 1024 * 1024 &&
               R3Limits.PatCfgBy == 16L * 1024 * 1024 &&
               R3Limits.CfgByte == 16L * 1024 * 1024 &&
               R3Limits.BkpByte == 4L * 1024 * 1024 &&
               R3Limits.HBytes == 8L * 1024 * 1024 &&
               R3Limits.DirEnt == 100_000 &&
               R3Limits.YamlDoc == 1 &&
               R3Limits.Depth == 64 &&
               R3Limits.Nodes == 250_000 &&
               R3Limits.Aliases == 4_096 &&
               R3Limits.YamlMap == 100_000 &&
               R3Limits.YamlSeq == 100_000 &&
               R3Limits.Diag == 1_000,
            "Expected the untrusted RPCS3 input limits to remain explicit and centrally testable.");

        var suiteRoot = Path.Combine(tempRoot, "input-limits");
        Directory.CreateDirectory(suiteRoot);

        var invalidUtf8 = Path.Combine(suiteRoot, "invalid-utf8.yml");
        File.WriteAllBytes(invalidUtf8, [0xc3, 0x28]);
        MustFail<DecoderFallbackException>(
            () => R3Bound.ReadUtf8(
                invalidUtf8,
                R3Limits.CfgByte,
                "invalid UTF-8"),
            "Expected malformed UTF-8 to be rejected instead of silently replacing bytes.");

        var validSfo = Path.Combine(suiteRoot, "valid.sfo");
        SfoWrite(validSfo, new Dictionary<string, string>
        {
            ["TITLE"] = "Bounded SFO",
            ["TITLE_ID"] = "BLUS40000",
            ["APP_VER"] = "01.00"
        });
        Assert(SfoRead.TryRead(validSfo) is
        { TitleId: "BLUS40000", AppVer: "01.00" },
            "Expected a small valid PARAM.SFO to remain readable.");

        var truncatedSfo = Path.Combine(suiteRoot, "truncated.sfo");
        File.WriteAllBytes(truncatedSfo, [0x00, 0x50, 0x53, 0x46]);
        Assert(SfoRead.TryRead(truncatedSfo) is null,
            "Expected a truncated PARAM.SFO to fail safely.");

        var hugeEntryCountSfo = Path.Combine(
            suiteRoot,
            "huge-entry-count.sfo");
        using (var stream = File.Create(hugeEntryCountSfo))
        using (var writer = new BinaryWriter(stream, Encoding.UTF8))
        {
            writer.Write(0x46535000u);
            writer.Write(0x00000101u);
            writer.Write(20u);
            writer.Write(20u);
            writer.Write(R3Limits.Entries + 1);
        }
        Assert(SfoRead.TryRead(hugeEntryCountSfo) is null,
            "Expected an excessive PARAM.SFO entry count to be rejected before table allocation.");

        var hugeValueSfo = Path.Combine(suiteRoot, "huge-value.sfo");
        OneSfo(
            hugeValueSfo,
            R3Limits.SfoValBy + 1,
            dataOffset: 0);
        Assert(SfoRead.TryRead(hugeValueSfo) is null,
            "Expected an excessive PARAM.SFO value length to be rejected before byte allocation.");

        var overflowOffsetSfo = Path.Combine(
            suiteRoot,
            "overflow-offset.sfo");
        OneSfo(
            overflowOffsetSfo,
            dataLength: 1,
            dataOffset: uint.MaxValue);
        Assert(SfoRead.TryRead(overflowOffsetSfo) is null,
            "Expected PARAM.SFO offset arithmetic outside the stream to fail safely.");

        var oversizedSfo = Path.Combine(suiteRoot, "oversized.sfo");
        using (var stream = File.Create(oversizedSfo))
        {
            stream.SetLength(R3Limits.Bytes + 1);
        }
        Assert(SfoRead.TryRead(oversizedSfo) is null,
            "Expected an oversized sparse PARAM.SFO to be rejected without allocating its declared size.");

        var atLimitRoot = Path.Combine(suiteRoot, "games-at-limit");
        var atLimitInstall = LimInst(atLimitRoot);
        var atLimitGamePath = Path.Combine(suiteRoot, "BLUS40001");
        Directory.CreateDirectory(atLimitGamePath);
        SfoWrite(
            Path.Combine(atLimitGamePath, "PARAM.SFO"),
            new Dictionary<string, string>
            {
                ["TITLE"] = "Games YML At Limit",
                ["TITLE_ID"] = "BLUS40001",
                ["APP_VER"] = "01.00",
                ["CATEGORY"] = "DG"
            });
        var atLimitGamesYml = Path.Combine(
            atLimitInstall.CfgPath,
            "games.yml");
        Utf8Len(
            atLimitGamesYml,
            $"BLUS40001: \"{EscPath(atLimitGamePath)}\"\r\n#",
            R3Limits.GameByte);
        Assert(new FileInfo(atLimitGamesYml).Length ==
               R3Limits.GameByte &&
               new GameScanner().Scan(atLimitInstall)
                   .Any(game => game.Serial == "BLUS40001"),
            "Expected games.yml at the exact byte limit to remain readable.");

        var overLimitRoot = Path.Combine(suiteRoot, "games-over-limit");
        var overLimitInstall = LimInst(overLimitRoot);
        var overLimitGamesYml = Path.Combine(
            overLimitInstall.CfgPath,
            "games.yml");
        using (var stream = File.Create(overLimitGamesYml))
        {
            stream.SetLength(R3Limits.GameByte + 1);
        }
        Assert(new GameScanner().Scan(overLimitInstall).Count == 0,
            "Expected games.yml above the byte limit to be rejected without blocking the scan.");

        var fallbackGamePath = Path.Combine(suiteRoot, "BLUS40002");
        Directory.CreateDirectory(fallbackGamePath);
        SfoWrite(
            Path.Combine(fallbackGamePath, "PARAM.SFO"),
            new Dictionary<string, string>
            {
                ["TITLE"] = "Games YML Fallback",
                ["TITLE_ID"] = "BLUS40002",
                ["APP_VER"] = "01.00",
                ["CATEGORY"] = "DG"
            });
        File.WriteAllText(
            Path.Combine(overLimitRoot, "games.yml"),
            $"BLUS40002: \"{EscPath(fallbackGamePath)}\"{Environment.NewLine}");
        Assert(new GameScanner().Scan(overLimitInstall)
                .Any(game => game.Serial == "BLUS40002"),
            "Expected one oversized games.yml source not to block a valid independent source.");

        var oversizedVfsRoot = Path.Combine(
            suiteRoot,
            "oversized-vfs");
        var oversizedVfsConfig = Path.Combine(
            oversizedVfsRoot,
            "config");
        var oversizedVfsDecoy = Path.Combine(
            oversizedVfsRoot,
            "dev_hdd0",
            "game",
            "NPEB40003");
        Directory.CreateDirectory(oversizedVfsConfig);
        Directory.CreateDirectory(oversizedVfsDecoy);
        var oversizedVfsExe = Path.Combine(
            oversizedVfsRoot,
            "rpcs3.exe");
        File.WriteAllBytes(oversizedVfsExe, []);
        SfoWrite(
            Path.Combine(oversizedVfsDecoy, "PARAM.SFO"),
            new Dictionary<string, string>
            {
                ["TITLE"] = "Oversized VFS Decoy",
                ["TITLE_ID"] = "NPEB40003",
                ["APP_VER"] = "01.00",
                ["CATEGORY"] = "HG"
            });
        using (var stream = File.Create(Path.Combine(
                   oversizedVfsConfig,
                   "vfs.yml")))
        {
            stream.SetLength(R3Limits.CfgByte + 1);
        }
        var oversizedVfsResult = new GameScanner().ScanInfo(
            Rpcs3InstallService.FromExe(oversizedVfsExe));
        Assert(oversizedVfsResult.Games.All(game =>
                   game.Serial != "NPEB40003") &&
               oversizedVfsResult.Diags.Any(diagnostic =>
                   diagnostic.SrcKind == ScanSrc.Yaml &&
                   diagnostic.SrcPath.EndsWith(
                       "vfs.yml",
                       StringComparison.OrdinalIgnoreCase) &&
                   diagnostic.TechMsg.Contains(
                       "input limit",
                       StringComparison.OrdinalIgnoreCase)),
            "Expected oversized vfs.yml to fail closed before unbounded allocation instead of falling back to DataRoot\\dev_hdd0.");

        var oversizedPatch = Path.Combine(suiteRoot, "patch.yml");
        using (var stream = File.Create(oversizedPatch))
        {
            stream.SetLength(
                R3Limits.PatCatBy + 1);
        }
        var patchRead = new R3Config.Svc.ReadSvc().Read(
            oversizedPatch,
            PatSrc.Official);
        Assert(!patchRead.IsOk &&
               patchRead.Error?.Contains(
                   "input limit",
                   StringComparison.OrdinalIgnoreCase) == true,
            "Expected a patch catalog above the byte limit to fail before text allocation.");

        var oversizedPatchConfig = Path.Combine(
            suiteRoot,
            "patch_config.yml");
        using (var stream = File.Create(oversizedPatchConfig))
        {
            stream.SetLength(
                R3Limits.PatCfgBy + 1);
        }
        MustFail<InvalidDataException>(
            () => new CfgRead().Load(oversizedPatchConfig),
            "Expected patch_config.yml above the byte limit to be rejected.");

        var oversizedCustomConfig = Path.Combine(
            suiteRoot,
            "config_BLUS40003.yml");
        using (var stream = File.Create(oversizedCustomConfig))
        {
            stream.SetLength(
                R3Limits.CfgByte + 1);
        }
        Assert(!R3Yaml.CanRead(oversizedCustomConfig),
            "Expected custom config YAML above the byte limit to fail closed.");

        var multiDocumentPatchConfig = Path.Combine(
            suiteRoot,
            "multi-document-patch_config.yml");
        File.WriteAllText(
            multiDocumentPatchConfig,
            "---\r\n{}\r\n---\r\n{}\r\n");
        MustFail<InvalidDataException>(
            () => new CfgRead().Load(multiDocumentPatchConfig),
            "Expected YAML document count to be bounded.");

        var depthAtLimit =
            string.Concat(Enumerable.Repeat(
                "- ",
                R3Limits.Depth)) +
            "value\n";
        R3Bound.VldYaml(
            depthAtLimit,
            "depth-at-limit");
        var depthOverLimit =
            "- " + depthAtLimit;
        MustFail<InvalidDataException>(
            () => R3Bound.VldYaml(
                depthOverLimit,
                "depth-over-limit"),
            "Expected YAML depth above the structural limit to be rejected before representation-model allocation.");

        var aliasAtLimit = YamlIts(
            "base: &base { value: 1 }\nuses:\n",
            "  - *base\n",
            R3Limits.Aliases);
        R3Bound.VldYaml(
            aliasAtLimit,
            "aliases-at-limit");
        var aliasOverLimit = aliasAtLimit + "  - *base\n";
        MustFail<InvalidDataException>(
            () => R3Bound.VldYaml(
                aliasOverLimit,
                "aliases-over-limit"),
            "Expected YAML aliases above the structural limit to be rejected.");

        var mappingsAtLimit = YamlIts(
            "root:\n",
            "  - {}\n",
            R3Limits.YamlMap - 1);
        R3Bound.VldYaml(
            mappingsAtLimit,
            "mappings-at-limit");
        MustFail<InvalidDataException>(
            () => R3Bound.VldYaml(
                mappingsAtLimit + "  - {}\n",
                "mappings-over-limit"),
            "Expected YAML mapping count above the structural limit to be rejected.");

        var sequencesAtLimit = YamlIts(
            "root:\n",
            "  - []\n",
            R3Limits.YamlSeq - 1);
        R3Bound.VldYaml(
            sequencesAtLimit,
            "sequences-at-limit");
        MustFail<InvalidDataException>(
            () => R3Bound.VldYaml(
                sequencesAtLimit + "  - []\n",
                "sequences-over-limit"),
            "Expected YAML sequence count above the structural limit to be rejected.");

        var nodesAtLimit = YamlIts(
            string.Empty,
            "- value\n",
            R3Limits.Nodes - 1);
        R3Bound.VldYaml(
            nodesAtLimit,
            "nodes-at-limit");
        MustFail<InvalidDataException>(
            () => R3Bound.VldYaml(
                nodesAtLimit + "- value\n",
                "nodes-over-limit"),
            "Expected YAML node count above the structural limit to be rejected.");

        var oversizedBackupRoot = Path.Combine(
            suiteRoot,
            "oversized-backup-manifest");
        var oversizedSnapshot = Path.Combine(
            oversizedBackupRoot,
            "snapshot");
        Directory.CreateDirectory(oversizedSnapshot);
        using (var stream = File.Create(Path.Combine(
                   oversizedSnapshot,
                   "manifest.json")))
        {
            stream.SetLength(
                R3Limits.BkpByte + 1);
        }
        Assert(new BackupService().ListBkp(
                   oversizedBackupRoot).Count == 0,
            "Expected an oversized backup manifest to be rejected without full-file allocation.");

        var hCanaryLimitRoot = Path.Combine(
            suiteRoot,
            "hcanary-metadata-limit");
        Directory.CreateDirectory(hCanaryLimitRoot);
        var hCanaryPatch = Path.Combine(
            hCanaryLimitRoot,
            "imported_patch.yml");
        File.WriteAllText(
            hCanaryPatch,
            CanPat(
                new string('a', 40),
                "Metadata Limit Patch",
                "0x00000300"));
        using (var stream = File.Create(Path.Combine(
                   hCanaryLimitRoot,
                   HCanaryPatchAuthoringService.MetaName)))
        {
            stream.SetLength(
                R3Limits.HBytes + 1);
        }
        MustFail<InvalidDataException>(
            () => new HCanaryPatchAuthoringService().Load(
                hCanaryPatch),
            "Expected oversized H-Canary metadata to be rejected before JSON text allocation.");

        var directoryLimitRoot = Path.Combine(
            suiteRoot,
            "directory-limit");
        var directoryLimitInstall = LimInst(
            directoryLimitRoot);
        var hddGameRoot = Path.Combine(
            directoryLimitRoot,
            "dev_hdd0",
            "game");
        for (var index = 0; index < 4; index++)
        {
            var serial =
                $"BLUS{41000 + index:00000}";
            var gameDirectory = Path.Combine(
                hddGameRoot,
                serial);
            Directory.CreateDirectory(
                gameDirectory);
            SfoWrite(
                Path.Combine(
                    gameDirectory,
                    "PARAM.SFO"),
                new Dictionary<string, string>
                {
                    ["TITLE"] = $"Directory Limit Game {index}",
                    ["TITLE_ID"] = serial,
                    ["APP_VER"] = "01.00",
                    ["CATEGORY"] = "HG"
                });
        }
        var directoryLimitResult = new GameScanner(
                directoryEntryLimit: 3)
            .ScanInfo(directoryLimitInstall);
        Assert(directoryLimitResult.Games.Count == 3 &&
               directoryLimitResult.Diags.Any(diagnostic =>
                   diagnostic.Code ==
                   ScanCode.Reached),
            "Expected game directory enumeration to stop at its limit and emit a diagnostic.");

        var diagnosticLimitRoot = Path.Combine(
            suiteRoot,
            "diagnostic-limit");
        var diagnosticInstall = LimInst(
            diagnosticLimitRoot);
        var diagnosticGameRoot = Path.Combine(
            diagnosticLimitRoot,
            "dev_hdd0",
            "game");
        Directory.CreateDirectory(diagnosticGameRoot);
        var diagnosticFolders = Enumerable.Range(
                0,
                R3Limits.Diag + 100)
            .Select(index => Path.Combine(
                diagnosticGameRoot,
                $"BLUS{50000 + index:00000}"))
            .ToArray();
        var diagnosticResult = new GameScanner(
                directoryEntryLimit: diagnosticFolders.Length,
                enumerateDirectories: _ => diagnosticFolders)
            .ScanInfo(diagnosticInstall);
        Assert(diagnosticResult.Diags.Count ==
               R3Limits.Diag,
            "Expected game-scan diagnostics to stop at the central diagnostic count limit.");

        Console.WriteLine(
            "Bounded untrusted RPCS3 input reader tests PASS.");
    }

    static string YamlIts(
        string header,
        string item,
        int count)
    {
        var text = new StringBuilder(
            header.Length +
            checked(item.Length * count));
        text.Append(header);
        for (var index = 0; index < count; index++)
        {
            text.Append(item);
        }

        return text.ToString();
    }

    static void Scan_Src(string tempRoot)
    {
        var suiteRoot = Path.Combine(
            tempRoot,
            "game-scan-diagnostics");
        var install = LimInst(
            Path.Combine(suiteRoot, "rpcs3"));
        var hddGameRoot = Path.Combine(
            install.RootPath,
            "dev_hdd0",
            "game");
        var validFolder = Path.Combine(hddGameRoot, "BLUS42000");
        var malformedFolder = Path.Combine(hddGameRoot, "BLUS42001");
        Directory.CreateDirectory(validFolder);
        Directory.CreateDirectory(malformedFolder);
        SfoWrite(
            Path.Combine(validFolder, "PARAM.SFO"),
            new Dictionary<string, string>
            {
                ["TITLE"] = "Diagnostic Healthy Game",
                ["TITLE_ID"] = "BLUS42000",
                ["APP_VER"] = "01.00",
                ["CATEGORY"] = "HG"
            });
        File.WriteAllBytes(
            Path.Combine(malformedFolder, "PARAM.SFO"),
            [0x00, 0x50, 0x53, 0x46]);

        var sfoResult = new GameScanner().ScanInfo(install);
        Assert(sfoResult.Games.Any(game =>
                   game.Serial == "BLUS42000") &&
               sfoResult.Diags.Any(diagnostic =>
                   diagnostic.Code == ScanCode.SfoBad &&
                   diagnostic.SrcPath.EndsWith(
                       "PARAM.SFO",
                       StringComparison.OrdinalIgnoreCase)),
            "Expected a healthy game to remain visible while a malformed independent SFO emits a diagnostic.");

        var gamesYmlPath = Path.Combine(
            install.CfgPath,
            "games.yml");
        File.WriteAllText(gamesYmlPath, "BLUS42000: [broken");
        var malformedGamesYmlResult = new GameScanner()
            .ScanInfo(install);
        Assert(malformedGamesYmlResult.Games.Any(game =>
                   game.Serial == "BLUS42000") &&
               malformedGamesYmlResult.Diags.Any(diagnostic =>
                   diagnostic.Code ==
                   ScanCode.GameBad),
            "Expected malformed games.yml not to block dev_hdd0 scanning and to emit a diagnostic.");

        File.WriteAllText(
            gamesYmlPath,
            $"BLUS42000: \"{EscPath(validFolder)}\"{Environment.NewLine}");
        var accessDeniedScanner = new GameScanner(
            enumerateDirectories: _ =>
                throw new UnauthorizedAccessException(
                    "Simulated bounded game-directory access denial."));
        var accessDeniedResult = accessDeniedScanner.ScanInfo(
            install);
        Assert(accessDeniedResult.Games.Any(game =>
                   game.Serial == "BLUS42000") &&
               accessDeniedResult.Diags.Any(diagnostic =>
                   diagnostic.Code ==
                   ScanCode.Denied),
            "Expected directory access denial not to stop valid games.yml discovery.");

        using (var stream = File.Create(gamesYmlPath))
        {
            stream.SetLength(R3Limits.GameByte + 1);
        }
        var tooLargeResult = new GameScanner().ScanInfo(install);
        Assert(tooLargeResult.Games.Any(game =>
                   game.Serial == "BLUS42000") &&
               tooLargeResult.Diags.Any(diagnostic =>
                   diagnostic.Code ==
                   ScanCode.Large),
            "Expected oversized games.yml to emit a clear diagnostic without blocking folder discovery.");

        Directory.CreateDirectory(install.CustPath);
        var customConfigDenied = new GameScanner(
            enumerateCustomConfigFiles: _ =>
                throw new UnauthorizedAccessException(
                    "Simulated custom-config access denial."))
            .ScanInfo(install);
        Assert(customConfigDenied.Diags.Any(diagnostic =>
                diagnostic.Code ==
                ScanCode.NoRead),
            "Expected unreadable custom configs to emit a diagnostic without stopping game discovery.");

        var firstOrder = tooLargeResult.Diags
            .Select(DiagId)
            .ToArray();
        var secondOrder = new GameScanner()
            .ScanInfo(install)
            .Diags
            .Select(DiagId)
            .ToArray();
        Assert(firstOrder.SequenceEqual(secondOrder),
            "Expected game scan diagnostics to have stable deterministic ordering.");

        var diagnosticToFormat = tooLargeResult.Diags.Single(
            diagnostic => diagnostic.Code ==
                ScanCode.Large);
        var formatter = new GmDiag();
        var originalLanguage = UiText.CurLang;
        try
        {
            UiText.SetLang(UiText.EnLang);
            var english = formatter.Format(diagnosticToFormat);
            UiText.SetLang(UiText.ArabLang);
            var arabic = formatter.Format(diagnosticToFormat);
            Assert(!string.IsNullOrWhiteSpace(english) &&
                   !string.IsNullOrWhiteSpace(arabic) &&
                   !string.Equals(
                       english,
                       arabic,
                       StringComparison.Ordinal) &&
                   !english.Contains(suiteRoot, StringComparison.OrdinalIgnoreCase) &&
                   !arabic.Contains(suiteRoot, StringComparison.OrdinalIgnoreCase) &&
                   !english.Contains(" at ", StringComparison.OrdinalIgnoreCase),
                "Expected localized diagnostics without full local paths or stack traces.");
        }
        finally
        {
            UiText.SetLang(originalLanguage);
        }

        Console.WriteLine(
            "Per-source game discovery diagnostics tests PASS.");
    }

    static string DiagId(GameScanDiagnostic diagnostic)
    {
        return string.Join(
            "|",
            diagnostic.SrcPath,
            diagnostic.Code,
            diagnostic.TechMsg);
    }

    static Rpcs3InstallDescriptor LimInst(string root)
    {
        Directory.CreateDirectory(Path.Combine(root, "config"));
        File.WriteAllBytes(Path.Combine(root, "rpcs3.exe"), []);
        return Rpcs3InstallService.FromExe(
            Path.Combine(root, "rpcs3.exe"));
    }

    static void OneSfo(
        string path,
        uint dataLength,
        uint dataOffset)
    {
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(
            stream,
            Encoding.UTF8,
            leaveOpen: false);
        writer.Write(0x46535000u);
        writer.Write(0x00000101u);
        writer.Write(36u);
        writer.Write(38u);
        writer.Write(1u);
        writer.Write((ushort)0);
        writer.Write((ushort)0x0204);
        writer.Write(dataLength);
        writer.Write(dataLength);
        writer.Write(dataOffset);
        writer.Write((byte)'K');
        writer.Write((byte)0);
        writer.Write((byte)0);
    }

    static void Utf8Len(
        string path,
        string prefix,
        long targetLength)
    {
        var prefixBytes = Encoding.UTF8.GetBytes(prefix);
        if (prefixBytes.LongLength > targetLength)
        {
            throw new ArgumentException(
                "The bounded test-file prefix is longer than its target length.",
                nameof(prefix));
        }

        using var stream = File.Create(path);
        stream.Write(prefixBytes);
        var padding = Enumerable.Repeat((byte)'x', 80 * 1024).ToArray();
        var remaining = targetLength - prefixBytes.LongLength;
        while (remaining > 0)
        {
            var count = (int)Math.Min(padding.Length, remaining);
            stream.Write(padding, 0, count);
            remaining -= count;
        }
    }

}
