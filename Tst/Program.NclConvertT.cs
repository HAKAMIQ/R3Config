using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using R3Config.Mdl;

internal static partial class Program
{
    private const string NclCliRunKey =
        "PPU-0123456789abcdef0123456789abcdef01234567";

    private const string NclCliSerial = "BLUS12345";

    private const string NclCliVersion = "01.02";

    private const string NclCliGameTitle = "Ncl Cli Fixture Game";

    private const string NclCliFileName =
        "Ncl Cli Fixture BLUS12345 v01.00 av01.02.ncl";

    // Static 32-bit write 0x11223344 at a guest address inside the fixture domain.
    private const string NclCliStaticSource =
        "Static Write Fixture\n0\ntester\n0 00020000 11223344\n#\n";

    // Find/replace requires runtime memory evidence and must fail closed.
    private const string NclCliRuntimeSource =
        "Runtime Fixture\n0\ntester\nB 00010000 04000000\nB 1122 3344\n#\n";

    private const string NclCliLog =
        "·! 0:00:01.000000 SYS: Title: Ncl Cli Fixture Game\n" +
        "·! 0:00:01.000001 SYS: Serial: BLUS12345\n" +
        "·! 0:00:01.000002 SYS: Version: APP_VER=01.02 VERSION=01.00\n" +
        "·! 0:00:02.000000 ppu_loader: ** Segment: p_type=0x1, " +
        "p_vaddr=0x10000, p_filesz=0x20000, p_memsz=0x20000, flags=0x400005\n" +
        "·W 0:00:02.000001 ppu_loader: PPU executable hash: " +
        NclCliRunKey + "\n";

    private static readonly string[] NclConvertMalformedRunKeys =
    [
        "PPU-not-a-hash",
        "PPU-0123456789abcdef",
        "SPU-0123456789abcdef0123456789abcdef01234567",
        "0123456789abcdef0123456789abcdef01234567"
    ];

    private static readonly string[] NclConvertForbiddenSymbols =
    [
        "PatchApplyService",
        "PatchFileTransactionService",
        "PatchMutationUseCase",
        "BundledCheatPatchService",
        "NclManagedPatchUseCase",
        "ApplyTxn",
        "ApplyPreparedManaged",
        "patch_config",
        "File.WriteAllText",
        "File.WriteAllBytes",
        "File.AppendAllText",
        "File.Create",
        "File.Copy",
        "File.Move",
        "File.Delete",
        "Directory.CreateDirectory",
        "Directory.Delete",
        "FileMode.Create",
        "FileMode.Append",
        "StreamWriter",
        "FileAccess.Write"
    ];

    [GeneratedRegex(
        @"byte\s*\r?\n\s*-\s*0x(?<address>[0-9A-Fa-f]+)\s*\r?\n\s*-\s*0x(?<value>[0-9A-Fa-f]+)",
        RegexOptions.CultureInvariant)]
    private static partial Regex NclConvertInstructionRegex();

    internal static void NclConvertCliTests()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "R3ConfigNclConvertCli",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var staticNcl = Path.Combine(root, NclCliFileName);
            File.WriteAllText(staticNcl, NclCliStaticSource);

            var runtimeNcl = Path.Combine(
                root,
                "Runtime Fixture BLUS12345 v01.00 av01.02.ncl");
            File.WriteAllText(runtimeNcl, NclCliRuntimeSource);

            var logPath = Path.Combine(root, "RPCS3.log");
            File.WriteAllText(logPath, NclCliLog);

            NclConvertValidConversionIsBigEndianExact(staticNcl, logPath);
            NclConvertInvalidCheatIndexFailsClosed(staticNcl, logPath);
            NclConvertWrongSerialFailsClosed(staticNcl, logPath);
            NclConvertWrongVersionFailsClosed(staticNcl, logPath);
            NclConvertMalformedRunKeyFailsClosed(staticNcl, logPath);
            NclConvertMissingFilesFailClosed(staticNcl, logPath, root);
            NclConvertRuntimeCheatIsRejected(runtimeNcl, logPath);
            NclConvertPerformsNoMutation(staticNcl, logPath, root);
            NclConvertOpensNoWritableTransactionPath();
        }
        finally
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch (Exception ex) when (
                ex is IOException or UnauthorizedAccessException)
            {
                // Diagnostic fixture cleanup is best effort.
            }
        }
    }

    private static (int Exit, string Output) NclConvertRun(
        string nclPath,
        string logPath,
        string cheatIndex,
        string runKey,
        string serial,
        string version)
    {
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        var exit = RunNclConvert(
            nclPath,
            logPath,
            cheatIndex,
            runKey,
            serial,
            version,
            NclCliGameTitle,
            nameof(NclDialect.ArtemisPs3),
            writer);

        return (exit, writer.ToString());
    }

    private static string NclConvertField(string output, string key)
    {
        foreach (var line in output.Split('\n'))
        {
            var trimmed = line.TrimEnd('\r');
            if (trimmed.StartsWith(key + "=", StringComparison.Ordinal))
            {
                return trimmed[(key.Length + 1)..];
            }
        }

        return string.Empty;
    }

    private static void NclConvertValidConversionIsBigEndianExact(
        string nclPath,
        string logPath)
    {
        var (exit, output) = NclConvertRun(
            nclPath,
            logPath,
            "0",
            NclCliRunKey,
            NclCliSerial,
            NclCliVersion);

        Assert(exit == 0,
            $"--ncl-convert must succeed on the static fixture. Output: {output}");
        Assert(NclConvertField(output, "ReasonCode") == "OK",
            "A successful conversion must report ReasonCode=OK.");
        Assert(NclConvertField(output, "DeclaredSerial") == NclCliSerial,
            "DeclaredSerial must come from the NCL filename identity.");
        Assert(NclConvertField(output, "DeclaredVersion") == NclCliVersion,
            "DeclaredVersion must resolve to the declared application version.");
        Assert(NclConvertField(output, "LowerStatus") == "Success",
            "The static fixture must lower successfully.");
        Assert(NclConvertField(output, "EvidenceStatus") == "Success",
            "The fixture log must produce exact PPU address-domain evidence.");
        Assert(NclConvertField(output, "BindingStatus") == "Success",
            "The static write must bind to the proven PT_LOAD domain.");
        Assert(NclConvertField(output, "EmitStatus") == "Success",
            "Emission must produce a verified RPCS3 Patch 1.2 document.");
        Assert(NclConvertField(output, "ConcreteWrites") == "1",
            "The static fixture declares exactly one concrete write.");
        Assert(NclConvertField(output, "ConcreteBytes") == "4",
            "The static fixture writes exactly four bytes.");
        Assert(NclConvertField(output, "InstructionCount") == "4",
            "Byte-exact emission must produce four RPCS3 instructions.");
        Assert(NclConvertField(output, "GeneratedPatchSha256").Length == 64,
            "A successful conversion must report a SHA-256 of the generated patch.");
        Assert(output.Contains(NclConvertSeparator, StringComparison.Ordinal) &&
               output.Contains(NclConvertEndSeparator, StringComparison.Ordinal),
            "The generated patch must be printed between explicit separators.");

        // Byte-for-byte, order-sensitive endianness proof.
        // Source literal 0x11223344 must reach memory as 11 22 33 44.
        var instructions = NclConvertInstructionRegex().Matches(output);

        Assert(instructions.Count == 4,
            $"Expected four emitted byte instructions, found {instructions.Count}.");

        var expected = new (uint Address, byte Value)[]
        {
            (0x00020000u, 0x11),
            (0x00020001u, 0x22),
            (0x00020002u, 0x33),
            (0x00020003u, 0x44)
        };

        for (var index = 0; index < expected.Length; index++)
        {
            var address = uint.Parse(
                instructions[index].Groups["address"].Value,
                NumberStyles.HexNumber,
                CultureInfo.InvariantCulture);
            var value = byte.Parse(
                instructions[index].Groups["value"].Value,
                NumberStyles.HexNumber,
                CultureInfo.InvariantCulture);

            Assert(address == expected[index].Address,
                $"Instruction {index + 1} address must be 0x{expected[index].Address:X8}, " +
                $"was 0x{address:X8}.");
            Assert(value == expected[index].Value,
                $"Instruction {index + 1} value must be 0x{expected[index].Value:X2}, " +
                $"was 0x{value:X2}. Reversed endianness would produce " +
                $"0x{expected[expected.Length - 1 - index].Value:X2}.");
        }
    }

    private static void NclConvertInvalidCheatIndexFailsClosed(
        string nclPath,
        string logPath)
    {
        var (highExit, highOutput) = NclConvertRun(
            nclPath, logPath, "99", NclCliRunKey, NclCliSerial, NclCliVersion);
        Assert(highExit != 0 &&
               NclConvertField(highOutput, "ReasonCode") == "CHEAT_INDEX_OUT_OF_RANGE",
            "An out-of-range cheat index must fail closed.");

        var (badExit, badOutput) = NclConvertRun(
            nclPath, logPath, "-1", NclCliRunKey, NclCliSerial, NclCliVersion);
        Assert(badExit != 0 &&
               NclConvertField(badOutput, "ReasonCode") == "INVALID_CHEAT_INDEX",
            "A negative cheat index must fail closed.");

        var (textExit, textOutput) = NclConvertRun(
            nclPath, logPath, "one", NclCliRunKey, NclCliSerial, NclCliVersion);
        Assert(textExit != 0 &&
               NclConvertField(textOutput, "ReasonCode") == "INVALID_CHEAT_INDEX",
            "A non-numeric cheat index must fail closed.");
    }

    private static void NclConvertWrongSerialFailsClosed(
        string nclPath,
        string logPath)
    {
        var (exit, output) = NclConvertRun(
            nclPath, logPath, "0", NclCliRunKey, "BLES99999", NclCliVersion);

        Assert(exit != 0 &&
               NclConvertField(output, "ReasonCode") == "SOURCE_SERIAL_MISMATCH",
            "A serial that the NCL source does not declare must fail closed.");
        Assert(!output.Contains(NclConvertSeparator, StringComparison.Ordinal),
            "A rejected conversion must not print a generated patch.");
    }

    private static void NclConvertWrongVersionFailsClosed(
        string nclPath,
        string logPath)
    {
        var (exit, output) = NclConvertRun(
            nclPath, logPath, "0", NclCliRunKey, NclCliSerial, "09.09");

        Assert(exit != 0 &&
               NclConvertField(output, "ReasonCode") == "SOURCE_VERSION_MISMATCH",
            "A version that the NCL source does not declare must fail closed.");
        Assert(!output.Contains(NclConvertSeparator, StringComparison.Ordinal),
            "A rejected conversion must not print a generated patch.");
    }

    private static void NclConvertMalformedRunKeyFailsClosed(
        string nclPath,
        string logPath)
    {
        foreach (var runKey in NclConvertMalformedRunKeys)
        {
            var (exit, output) = NclConvertRun(
                nclPath, logPath, "0", runKey, NclCliSerial, NclCliVersion);

            Assert(exit != 0,
                $"Run key '{runKey}' must fail closed.");
            Assert(NclConvertField(output, "ReasonCode")
                    .StartsWith("CONVERSION_REJECTED_", StringComparison.Ordinal),
                $"Run key '{runKey}' must be rejected by the production conversion service.");
            Assert(!output.Contains(NclConvertSeparator, StringComparison.Ordinal),
                "A rejected conversion must not print a generated patch.");
        }
    }

    private static void NclConvertMissingFilesFailClosed(
        string nclPath,
        string logPath,
        string root)
    {
        var absentNcl = Path.Combine(root, "absent BLUS12345 av01.02.ncl");
        var absentLog = Path.Combine(root, "absent-RPCS3.log");

        var (nclExit, nclOutput) = NclConvertRun(
            absentNcl, logPath, "0", NclCliRunKey, NclCliSerial, NclCliVersion);
        Assert(nclExit != 0 &&
               NclConvertField(nclOutput, "ReasonCode") == "NCL_FILE_NOT_FOUND",
            "A missing NCL file must fail closed.");

        var (logExit, logOutput) = NclConvertRun(
            nclPath, absentLog, "0", NclCliRunKey, NclCliSerial, NclCliVersion);
        Assert(logExit != 0 &&
               NclConvertField(logOutput, "ReasonCode") == "RPCS3_LOG_NOT_FOUND",
            "A missing RPCS3 log must fail closed.");
    }

    private static void NclConvertRuntimeCheatIsRejected(
        string nclPath,
        string logPath)
    {
        var (exit, output) = NclConvertRun(
            nclPath, logPath, "0", NclCliRunKey, NclCliSerial, NclCliVersion);

        Assert(exit != 0,
            "A runtime-dependent NCL cheat must not convert.");
        Assert(NclConvertField(output, "ReasonCode") ==
               "CONVERSION_REJECTED_LoweringRejected",
            $"A find/replace cheat must be rejected by static lowering. Output: {output}");
        Assert(NclConvertField(output, "VerifyStatus") ==
               nameof(NclVerifyStat.RuntimeEvidenceRequired),
            "A find/replace cheat must verify as RuntimeEvidenceRequired.");
        Assert(!output.Contains(NclConvertSeparator, StringComparison.Ordinal),
            "A rejected conversion must not print a generated patch.");
    }

    private static void NclConvertPerformsNoMutation(
        string nclPath,
        string logPath,
        string root)
    {
        var rpcs3Root = Path.Combine(root, "rpcs3");
        var patches = Path.Combine(rpcs3Root, "patches");
        var config = Path.Combine(rpcs3Root, "config");
        Directory.CreateDirectory(patches);
        Directory.CreateDirectory(config);

        File.WriteAllText(
            Path.Combine(patches, "patch.yml"),
            "Version: 1.2\n");
        File.WriteAllText(
            Path.Combine(patches, "imported_patch.yml"),
            "Version: 1.2\n");
        File.WriteAllText(
            Path.Combine(config, "patch_config.yml"),
            "PPU-0123456789abcdef0123456789abcdef01234567:\n");

        var before = NclConvertSnapshot(rpcs3Root);
        var nclBefore = NclConvertSnapshot(Path.GetDirectoryName(nclPath)!);

        var (exit, _) = NclConvertRun(
            nclPath, logPath, "0", NclCliRunKey, NclCliSerial, NclCliVersion);
        Assert(exit == 0, "The mutation probe requires a successful conversion.");

        var after = NclConvertSnapshot(rpcs3Root);
        var nclAfter = NclConvertSnapshot(Path.GetDirectoryName(nclPath)!);

        Assert(before.Count == after.Count,
            "--ncl-convert must not add or remove RPCS3 files.");
        foreach (var (path, hash) in before)
        {
            Assert(after.TryGetValue(path, out var current) && current == hash,
                $"--ncl-convert must not modify RPCS3 file '{path}'.");
        }

        Assert(nclBefore.Count == nclAfter.Count,
            "--ncl-convert must not add or remove NCL source files.");
        foreach (var (path, hash) in nclBefore)
        {
            Assert(nclAfter.TryGetValue(path, out var current) && current == hash,
                $"--ncl-convert must not modify NCL source file '{path}'.");
        }
    }

    private static Dictionary<string, string> NclConvertSnapshot(string root)
    {
        var snapshot = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var file in Directory.GetFiles(
                     root,
                     "*",
                     SearchOption.AllDirectories))
        {
            snapshot[Path.GetRelativePath(root, file)] =
                Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file)));
        }

        return snapshot;
    }

    private static void NclConvertOpensNoWritableTransactionPath()
    {
        var source = Path.Combine(
            RepoRoot(),
            "Tst",
            "Program.NclConvert.cs");

        Assert(File.Exists(source),
            $"The --ncl-convert entry point source must exist at {source}.");

        var text = File.ReadAllText(source);

        foreach (var forbidden in NclConvertForbiddenSymbols)
        {
            Assert(!text.Contains(forbidden, StringComparison.Ordinal),
                $"--ncl-convert must not reference the writable path '{forbidden}'.");
        }
    }
}
