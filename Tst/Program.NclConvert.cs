using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using R3Config.Mdl;
using R3Config.Svc;

internal static partial class Program
{
    internal const string NclConvertFlag = "--ncl-convert";

    internal const string NclConvertSeparator =
        "----- BEGIN GENERATED RPCS3 PATCH 1.2 -----";

    internal const string NclConvertEndSeparator =
        "----- END GENERATED RPCS3 PATCH 1.2 -----";

    private static bool TryRunNclConvertMode(string[] args)
    {
        if (args.Length is < 8 or > 9 ||
            !args[0].Equals(
                NclConvertFlag,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        Environment.ExitCode = RunNclConvert(
            args[1],
            args[2],
            args[3],
            args[4],
            args[5],
            args[6],
            args[7],
            args.Length == 9 ? args[8] : nameof(NclDialect.ArtemisPs3),
            Console.Out);

        return true;
    }

    // Read-only diagnostic entry point.
    // Calls the production NCL conversion implementation unchanged.
    // Never mutates RPCS3 patch files, activation config, art.yml, ncl.zip or any catalog.
    internal static int RunNclConvert(
        string nclPath,
        string rpcS3LogPath,
        string cheatIndexText,
        string runKey,
        string serial,
        string version,
        string gameTitle,
        string dialectText,
        TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(output);

        var declaredSerial = "-";
        var declaredVersion = "-";
        var cheatName = "-";
        var parseStatus = "NotReached";
        var verifyStatus = "NotReached";
        var lowerStatus = "NotReached";
        var evidenceStatus = "NotReached";
        var bindingStatus = "NotReached";
        var emitStatus = "NotReached";
        var concreteWrites = 0;
        var concreteBytes = 0;
        var instructionCount = 0;
        var patchSha256 = "-";
        var patchText = string.Empty;

        int Report(int exitCode, string reason, string detail)
        {
            output.WriteLine("NCL_CONVERT_RESULT");
            output.WriteLine($"Dialect={dialectText}");
            output.WriteLine($"DeclaredSerial={declaredSerial}");
            output.WriteLine($"DeclaredVersion={declaredVersion}");
            output.WriteLine($"SelectedSerial={serial}");
            output.WriteLine($"SelectedVersion={version}");
            output.WriteLine($"RunKey={runKey}");
            output.WriteLine($"CheatIndex={cheatIndexText}");
            output.WriteLine($"CheatName={cheatName}");
            output.WriteLine($"ParseStatus={parseStatus}");
            output.WriteLine($"VerifyStatus={verifyStatus}");
            output.WriteLine($"LowerStatus={lowerStatus}");
            output.WriteLine($"EvidenceStatus={evidenceStatus}");
            output.WriteLine($"BindingStatus={bindingStatus}");
            output.WriteLine($"EmitStatus={emitStatus}");
            output.WriteLine($"ConcreteWrites={concreteWrites}");
            output.WriteLine($"ConcreteBytes={concreteBytes}");
            output.WriteLine($"InstructionCount={instructionCount}");
            output.WriteLine($"GeneratedPatchSha256={patchSha256}");
            output.WriteLine($"Outcome={(exitCode == 0 ? "SUCCESS" : "FAIL_CLOSED")}");
            output.WriteLine($"ReasonCode={reason}");
            output.WriteLine($"Detail={Flatten(detail)}");

            if (exitCode == 0)
            {
                output.WriteLine(NclConvertSeparator);
                output.Write(patchText);
                if (!patchText.EndsWith('\n'))
                {
                    output.WriteLine();
                }

                output.WriteLine(NclConvertEndSeparator);
            }

            return exitCode;
        }

        if (!Enum.TryParse<NclDialect>(
                dialectText,
                ignoreCase: true,
                out var dialect))
        {
            return Report(
                2,
                "INVALID_DIALECT",
                $"Unknown NCL dialect '{dialectText}'. Expected NetCheat453 or ArtemisPs3.");
        }

        if (string.IsNullOrWhiteSpace(nclPath) ||
            !File.Exists(nclPath))
        {
            return Report(
                2,
                "NCL_FILE_NOT_FOUND",
                $"NCL source file does not exist: {nclPath}");
        }

        if (string.IsNullOrWhiteSpace(rpcS3LogPath) ||
            !File.Exists(rpcS3LogPath))
        {
            return Report(
                2,
                "RPCS3_LOG_NOT_FOUND",
                $"RPCS3 log file does not exist: {rpcS3LogPath}");
        }

        if (!int.TryParse(
                cheatIndexText,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var cheatIndex) ||
            cheatIndex < 0)
        {
            return Report(
                2,
                "INVALID_CHEAT_INDEX",
                $"Cheat index must be a non-negative integer: '{cheatIndexText}'.");
        }

        // Declared source identity comes from the production parser.
        var identity = NclParser.ParseIdentity(nclPath);
        declaredSerial = identity.Serials.Count > 0
            ? string.Join("|", identity.Serials)
            : "-";
        var identityVersions = identity.VersionsForSerial(serial);
        declaredVersion = identityVersions.Count > 0
            ? string.Join("|", identityVersions)
            : (identity.Versions.Count > 0
                ? string.Join("|", identity.Versions)
                : "-");

        // Exact identity gate. No override, no fallback, title never used.
        if (identity.Serials.Count > 0 &&
            !identity.DeclaresSerial(serial))
        {
            return Report(
                3,
                "SOURCE_SERIAL_MISMATCH",
                "The NCL filename declares a different title ID than the selected game.");
        }

        if (identityVersions.Count > 0 &&
            !identityVersions.Any(candidate =>
                PatVer.Equals(candidate, version)))
        {
            return Report(
                3,
                "SOURCE_VERSION_MISMATCH",
                "The NCL filename declares a different application version for the selected game.");
        }

        NclDoc document;
        try
        {
            document = NclParser.Read(nclPath, dialect);
        }
        catch (Exception ex) when (
            ex is IOException or
            UnauthorizedAccessException or
            InvalidDataException or
            ArgumentException)
        {
            parseStatus = "Rejected";
            return Report(4, "PARSE_REJECTED", ex.Message);
        }

        parseStatus = document.HasErr
            ? "ParsedWithErrors"
            : "Success";

        if (cheatIndex >= document.Cheats.Count)
        {
            return Report(
                5,
                "CHEAT_INDEX_OUT_OF_RANGE",
                $"Cheat index {cheatIndex} is outside the parsed document " +
                $"({document.Cheats.Count} cheat(s)).");
        }

        var cheat = document.Cheats[cheatIndex];
        cheatName = cheat.Name;

        var verification = NclSemanticVerifier.Verify(document);
        verifyStatus = verification.Items.Count == document.Cheats.Count
            ? verification.Items[cheatIndex].Status.ToString()
            : "VerificationMismatch";

        var lowering = NclStaticLowerer.Lower(cheat);
        lowerStatus = lowering.Code.ToString();
        concreteWrites = lowering.Writes.Count;
        concreteBytes = lowering.Writes.Sum(write => write.Bytes.Count);

        if (string.IsNullOrWhiteSpace(cheatName))
        {
            return Report(
                5,
                "EMPTY_CHEAT_TITLE",
                "The selected NCL cheat has no name and cannot become an RPCS3 patch title.");
        }

        // Production conversion. Nothing is re-implemented here.
        var conversion = NclPpuConversionService.ConvertFile(
            new NclPpuConversionFileRequest(
                nclPath,
                rpcS3LogPath,
                dialect,
                cheatIndex,
                new NclPpuConversionTarget(
                    runKey,
                    cheatName,
                    gameTitle,
                    serial,
                    version)));

        if (conversion.LoweringCode is { } loweringCode)
        {
            lowerStatus = loweringCode.ToString();
        }

        evidenceStatus = conversion.EvidenceCode?.ToString() ?? "NotReached";
        bindingStatus = conversion.BindingCode?.ToString() ?? "NotReached";
        emitStatus = conversion.EmissionCode?.ToString() ?? "NotReached";

        if (!conversion.IsOk ||
            conversion.Code != NclPpuConversionCode.Success)
        {
            return Report(
                6,
                $"CONVERSION_REJECTED_{conversion.Code}",
                conversion.Error ?? "Conversion was rejected without a message.");
        }

        patchText = conversion.PatchText;
        instructionCount = conversion.InstructionCount;
        patchSha256 = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(patchText)));

        if (conversion.RunKey is null ||
            !conversion.RunKey.Equals(
                runKey,
                StringComparison.OrdinalIgnoreCase))
        {
            return Report(
                7,
                "RUNKEY_IDENTITY_DRIFT",
                $"Requested run key '{runKey}' does not match emitted run key " +
                $"'{conversion.RunKey ?? "-"}'.");
        }

        return Report(0, "OK", "Conversion produced a verified RPCS3 Patch 1.2 document.");
    }

    private static string Flatten(string value)
    {
        return value
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Trim();
    }
}
