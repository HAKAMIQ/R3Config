using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using R3Config.Mdl;
using R3Config.Svc;

namespace R3Config.NclAudit;

internal static class Program
{
    private static readonly UTF8Encoding Utf8NoBom = new(
        encoderShouldEmitUTF8Identifier: false);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--source-integration")
        {
            return SourceIntegration.Run(args);
        }

        if (args.Length > 0 && args[0] == "--full-scan")
        {
            return FullScan.Run(args);
        }

        if (args.Length == 0)
        {
            Console.Error.WriteLine(
                "Usage: R3NclAudit <directory-or-zip> [--expect-files N] [--json path]");
            return 2;
        }

        var sourcePath = args[0];
        int? expectedFiles = null;
        string? jsonPath = null;

        for (var index = 1; index < args.Length; index++)
        {
            if (string.Equals(
                    args[index],
                    "--expect-files",
                    StringComparison.Ordinal))
            {
                if (index + 1 >= args.Length ||
                    !int.TryParse(args[index + 1], out var value) ||
                    value <= 0)
                {
                    Console.Error.WriteLine(
                        "--expect-files requires a positive integer.");
                    return 2;
                }

                expectedFiles = value;
                index++;
                continue;
            }

            if (string.Equals(
                    args[index],
                    "--json",
                    StringComparison.Ordinal))
            {
                if (index + 1 >= args.Length ||
                    string.IsNullOrWhiteSpace(args[index + 1]))
                {
                    Console.Error.WriteLine(
                        "--json requires a file path.");
                    return 2;
                }

                jsonPath = args[index + 1];
                index++;
                continue;
            }

            Console.Error.WriteLine(
                $"Unknown argument: {args[index]}");
            return 2;
        }

        try
        {
            var report = NclCorpusReader.Audit(
                sourcePath,
                NclDialect.ArtemisPs3);

            if (expectedFiles.HasValue &&
                report.FileCount != expectedFiles.Value)
            {
                Console.Error.WriteLine(
                    $"Expected {expectedFiles.Value} NCL files but found {report.FileCount}.");
                return 3;
            }

            if (!string.IsNullOrWhiteSpace(jsonPath))
            {
                WriteJson(jsonPath, report);
            }

            PrintReport(report, expectedFiles);
            return 0;
        }
        catch (IOException ex)
        {
            return PrintError(ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            return PrintError(ex);
        }
        catch (ArgumentException ex)
        {
            return PrintError(ex);
        }
    }

    private static int PrintError(Exception ex)
    {
        Console.Error.WriteLine(
            $"{ex.GetType().Name}: {ex.Message}");
        return 1;
    }

    private static void WriteJson(
        string path,
        NclCorpusReport report)
    {
        var fullPath = Path.GetFullPath(path);
        var parent = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(parent))
        {
            Directory.CreateDirectory(parent);
        }

        var json = JsonSerializer.Serialize(
            report,
            JsonOptions);
        File.WriteAllText(
            fullPath,
            json,
            Utf8NoBom);
    }

    private static void PrintReport(
        NclCorpusReport report,
        int? expectedFiles)
    {
        Console.WriteLine("NCL_CORPUS_AUDIT=PASS");
        Console.WriteLine($"SOURCE_KIND={report.SourceKind}");
        Console.WriteLine($"SOURCE_PATH={report.SourcePath}");
        Console.WriteLine(
            $"SOURCE_SHA256={report.SourceSha256 ?? "-"}");
        Console.WriteLine($"DIALECT={report.Dialect}");
        Console.WriteLine($"FILES={report.FileCount}");
        Console.WriteLine($"MODERN_FILES={report.ModernFiles}");
        Console.WriteLine($"LEGACY_FILES={report.LegacyFiles}");
        Console.WriteLine($"SOURCE_ENTRIES={report.CheatCount}");
        Console.WriteLine(
            $"OFFLINE_STATIC_CANDIDATE={report.StaticCandidates}");
        Console.WriteLine(
            $"STATIC_LOWERED={report.StaticLowered}");
        Console.WriteLine(
            $"STATIC_LOWER_REJECTED={report.StaticLowerRejected}");
        Console.WriteLine(
            $"STATIC_LOWER_UNVERIFIED_VALUE_INCREMENT={report.StaticLowerUnverifiedValueIncrement}");
        Console.WriteLine(
            $"STATIC_LOWER_EXPANSION_LIMIT={report.StaticLowerExpansionLimit}");
        Console.WriteLine(
            $"STATIC_LOWER_ADDRESS_OVERFLOW={report.StaticLowerAddressOverflow}");
        Console.WriteLine(
            $"STATIC_LOWER_OTHER_REJECTED={report.StaticLowerOtherRejected}");
        Console.WriteLine(
            $"LOWERED_WRITE_COUNT={report.LoweredWriteCount}");
        Console.WriteLine(
            $"LOWERED_BYTE_COUNT={report.LoweredByteCount}");
        Console.WriteLine(
            $"LOWERED_WRITES_OUTSIDE_UINT32={report.LoweredWritesOutsideUInt32}");
        Console.WriteLine(
            $"LOWERED_CHEATS_OUTSIDE_UINT32={report.LoweredCheatsOutsideUInt32}");
        Console.WriteLine(
            $"RUNTIME_EVIDENCE_REQUIRED={report.RuntimeEvidenceRequired}");
        Console.WriteLine(
            $"CONSTANT_RUNTIME_REQUIRED={report.ConstantRuntimeRequired}");
        Console.WriteLine(
            $"OPTION_SELECTION_REQUIRED={report.OptionSelectionRequired}");
        Console.WriteLine(
            $"NON_EXECUTABLE_METADATA={report.NonExecutableMetadata}");
        Console.WriteLine($"UNSUPPORTED={report.Unsupported}");
        Console.WriteLine($"INVALID={report.Invalid}");
        Console.WriteLine(
            $"EXACT_IDENTITY_FILES={report.ExactIdentityFiles}");
        Console.WriteLine(
            $"MISSING_IDENTITY_FILES={report.MissingIdentityFiles}");
        Console.WriteLine(
            $"FILES_WITH_PARSER_ERRORS={report.FilesWithParserErrors}");
        Console.WriteLine(
            $"FILES_WITH_WINDOWS1252_FALLBACK={report.FilesWithWindows1252Fallback}");
        Console.WriteLine(
            $"CHEATS_WITH_OPTIONS={report.CheatsWithOptions}");
        Console.WriteLine(
            $"OPTION_SETS={report.OptionSetCount}");
        Console.WriteLine(
            $"TOTAL_NCL_BYTES={report.TotalNclBytes}");

        if (expectedFiles.HasValue)
        {
            Console.WriteLine(
                $"EXPECTED_FILE_COUNT={expectedFiles.Value}");
            Console.WriteLine("EXPECTED_FILE_COUNT_CHECK=PASS");
        }
    }
}
