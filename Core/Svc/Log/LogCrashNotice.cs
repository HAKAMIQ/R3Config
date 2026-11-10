using System;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using R3Config.Mdl;

#pragma warning disable IDE0130

namespace R3Config.Svc;

// Decides whether the diagnosed log session warrants the main-window crash
// notice, and names that session so an acknowledgement can be remembered. Only
// a rule-backed Crash or FatalError event counts (LogRules), only in the
// session of the selected game itself; a missing GameExit, another game's log
// or a log without a game boot never does. Read-only and deterministic.
public static class LogCrashNotice
{
    public static string? Identify(DiagnosticReport report, string? selectedSerial)
    {
        ArgumentNullException.ThrowIfNull(report);

        var session = report.Session;
        if (string.IsNullOrWhiteSpace(selectedSerial) ||
            session.GameSerial is null ||
            !string.Equals(session.GameSerial, selectedSerial.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var crash = report.Findings
            .Select(finding => finding.Event)
            .Where(item => item is not null &&
                           item.Categories.Any(category => category is LogCategory.Crash or LogCategory.FatalError))
            .OrderBy(item => item!.Line)
            .FirstOrDefault();
        return crash is null ? null : SessionId(session, crash);
    }

    // The identity of the crashing session, built only from what precedes and
    // includes its first Crash/FatalError line: the serial, the RPCS3 build
    // banner, and that line's number, timestamp (microseconds since RPCS3
    // started), level, channel and text. Lines RPCS3 appends to the same log
    // afterwards (or the log's rotation to .gz) never change it, so a dismissed
    // crash stays dismissed; a newer session's crash differs at least in its
    // timestamp. The whole-log hash and the file's length or write time are not
    // used because both change on every append.
    public static string SessionId(LogSession session, LogEvent crash)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(crash);

        var identity = string.Join(
            "\n",
            session.GameSerial?.Trim().ToUpperInvariant() ?? string.Empty,
            session.Build?.VersionText ?? string.Empty,
            session.Build?.Commit ?? string.Empty,
            crash.Line.ToString(CultureInfo.InvariantCulture),
            crash.Seconds.ToString("R", CultureInfo.InvariantCulture),
            crash.Level.ToString(),
            crash.Thread ?? string.Empty,
            crash.Channel,
            crash.Message);
        return "sha256:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
    }
}