using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using R3Config.Mdl;
using R3Config.Svc;

// Read-only real-log acceptance for Log Diagnostics. It parses the given log
// (plain or .gz) with the production parser and prints the structured result,
// and, when the shipped knowledge base is present, the diagnosis. It never
// writes anything.
//
//   R3Test.exe --log-diag-real <log|log.gz> [serial]
internal static class LogDiagRealT
{
    public static void Run(string path, string? serial)
    {
        var full = Path.GetFullPath(path);
        Assert(File.Exists(full), $"Log not found: {full}");
        var info = new FileInfo(full);
        var source = new LogSourceInfo(
            full,
            full.EndsWith(".gz", StringComparison.OrdinalIgnoreCase) ? LogSourceKind.RotatedGzip : LogSourceKind.Current,
            info.Length,
            info.LastWriteTimeUtc);

        var timer = Stopwatch.StartNew();
        var session = Rpcs3LogService.ReadOne(source, serial);
        timer.Stop();

        Console.WriteLine($"LOG_FILE={Path.GetFileName(full)} BYTES={info.Length} LINES={session.TotalLines} MS={timer.ElapsedMilliseconds} TRUNCATED={session.Truncated}");
        Console.WriteLine($"LOG_BUILD={(session.Build is null ? "-" : $"{session.Build.VersionText}-{session.Build.BuildNumber}-{session.Build.Commit} {session.Build.Branch}")}");
        Console.WriteLine($"LOG_CPU={session.CpuLine ?? "-"}");
        Console.WriteLine($"LOG_OS={session.OsLine ?? "-"}");
        Console.WriteLine($"LOG_GAME={session.GameSerial ?? "-"} | {session.GameTitle ?? "-"} | APP_VER={session.AppVersion ?? "-"} | {session.GameCategory ?? "-"} | boots={session.BootCount}");
        var run = session.RunContext;
        Console.WriteLine($"LOG_RUN=fw:{run.FirmwareVersion ?? "-"} driver:{run.GpuDriver ?? "-"} pkg:{run.PackageVersion ?? "-"} config:{run.ConfigMode ?? "-"} media:{run.BootMedia} iso-key:{run.IsoKey} iso-enc:{run.IsoEncryption ?? "-"} patches:{run.AppliedPatches.Count} cfg-blocks:{run.ConfigBlocks} custom-file:{run.CustomConfigFile}");
        Console.WriteLine($"LOG_OBS={string.Join("; ", run.ObservationList.Select(item => $"{item.Kind}='{item.Value}' x{item.Count}"))} WIN32={run.Win32ErrorList.Count} PERF={(run.Performance is null ? "-" : $"n={run.Performance.Samples} med={run.Performance.CpuMedian:0.#}")} COMPLETENESS={run.Completeness}");
        Console.WriteLine($"LOG_MANIFEST={session.Analysis?.SourceType}/{session.Analysis?.ParserVersion}/{session.Analysis?.RuleSetVersion}/{session.Analysis?.LogSha256?[..12]} MS={session.AnalysisDuration?.TotalMilliseconds:0}");
        Console.WriteLine($"LOG_HASHES={string.Join(",", session.PpuHashes.Select(hash => hash[..8]))}");
        Console.WriteLine($"LOG_RENDERER={session.Renderer ?? "-"} GPU={session.GpuName ?? "-"} SETTINGS={session.Settings.Count}");
        Console.WriteLine("LOG_TIMELINE=" + string.Join(" -> ", session.Timeline.Select(step => $"{step.Stage}@{step.Line}")));
        foreach (var item in session.Events.Where(item => item.IsProblem).Take(25))
        {
            Console.WriteLine($"LOG_EVENT line={item.Line} x{item.Count} {item.Level} {item.Channel} [{string.Join(",", item.Categories)}] sig={item.Signature?.Id ?? "-"} :: {item.Message[..Math.Min(item.Message.Length, 110)]}");
        }

        Console.WriteLine($"LOG_EVENT_TOTAL={session.Events.Count} PROBLEMS={session.Problems.Count} RAW={session.RawEventLines.Count}");

        var kbPath = Path.Combine(AppContext.BaseDirectory, "Data", DiagnosticKnowledgeBase.FileName);
        var knowledge = File.Exists(kbPath) ? DiagnosticKnowledgeBase.Load(kbPath) : DiagnosticKnowledgeBase.Empty;
        var report = DiagnosticEngine.Evaluate(
            session,
            new DiagnosticEnvironment(session.GameSerial, session.AppVersion, session.PpuHashes, session.Build?.BuildNumber, "Windows", "x64", null, session.Renderer),
            knowledge);
        Console.WriteLine($"LOG_KB_ENTRIES={knowledge.Entries.Count} FINDINGS={report.Findings.Count} PRIMARY={report.Primary?.Kind.ToString() ?? "-"}");
        foreach (var finding in report.Findings.Where(item => item.Kind != DiagnosisKind.ObservedEvent))
        {
            Console.WriteLine($"LOG_FINDING {finding.Kind} entry={finding.Entry?.Id ?? "-"} quality={finding.Quality} reasons={string.Join(";", finding.Reasons)} solutions={string.Join(";", finding.Solutions.Select(item => item.Kind + (item.Build is { } b ? ":" + b : string.Empty)))}");
        }
    }
}
