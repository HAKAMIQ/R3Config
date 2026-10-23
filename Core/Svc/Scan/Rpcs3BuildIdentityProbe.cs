using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using R3Config.Mdl;

namespace R3Config.Svc;

public sealed class Rpcs3BuildIdentityProbe
{
    private const int PrefixByteLimit = 16 * 1024;
    private const int PrefixLineLimit = 8;

    private readonly Func<
        Rpcs3InstallDescriptor,
        IReadOnlyList<string>> _logPaths;

    public Rpcs3BuildIdentityProbe(
        Func<Rpcs3InstallDescriptor, IReadOnlyList<string>>? logPaths = null)
    {
        _logPaths = logPaths ?? Rpcs3LogLocator.PlainLogPaths;
    }

    public Rpcs3BuildIdentity Probe(
        Rpcs3InstallDescriptor install)
    {
        ArgumentNullException.ThrowIfNull(install);

        List<FileInfo> candidates;

        try
        {
            candidates = _logPaths(install)
                .Where(path =>
                    File.Exists(path))
                .Select(path =>
                    new FileInfo(path))
                .OrderByDescending(file =>
                    file.LastWriteTimeUtc)
                .ThenBy(file =>
                    file.FullName,
                    StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex) when (
            ex is IOException or
            UnauthorizedAccessException)
        {
            return Rpcs3BuildIdentity.Unavailable(
                ex.Message);
        }

        if (candidates.Count == 0)
        {
            return Rpcs3BuildIdentity.Unavailable(
                "RPCS3.log was not found.");
        }

        // Every existing candidate log is parsed independently — not just the
        // most recently written one — so a stale or unreadable secondary log
        // (e.g. a root-level RPCS3.log left behind alongside the active
        // log\RPCS3.log for a portable install) can surface as Partial or
        // Conflict evidence instead of being silently discarded.
        var evidence = new List<Rpcs3BuildEvidence>();
        var unparseableCount = 0;

        foreach (var log in candidates)
        {
            IReadOnlyList<string> lines;

            try
            {
                lines = ReadPrefixLines(log.FullName);
            }
            catch (Exception ex) when (
                ex is IOException or
                UnauthorizedAccessException)
            {
                unparseableCount++;
                continue;
            }

            if (!Rpcs3BuildIdentityParser.TryParse(
                    lines,
                    out var banner))
            {
                unparseableCount++;
                continue;
            }

            evidence.Add(new Rpcs3BuildEvidence(
                Kind: Rpcs3BuildEvidenceKind.RuntimeLogBanner,
                SourcePath: log.FullName,
                SourceLastWriteTimeUtc: new DateTimeOffset(
                    log.LastWriteTimeUtc,
                    TimeSpan.Zero),
                VersionText: banner.VersionText,
                BuildNumber: banner.BuildNumber,
                Commit: banner.Commit,
                Stage: banner.Stage,
                Branch: banner.Branch,
                Architecture: banner.Architecture));
        }

        if (evidence.Count == 0)
        {
            return Rpcs3BuildIdentity.Unavailable(
                "RPCS3.log does not contain a supported build banner.");
        }

        // Evidence is already ordered newest-first because `candidates` was.
        var newest = evidence[0];
        var distinctIdentities = evidence
            .Select(item => (
                Version: item.VersionText.ToLowerInvariant(),
                item.BuildNumber,
                Commit: item.Commit.ToLowerInvariant()))
            .Distinct()
            .Count();

        if (distinctIdentities > 1)
        {
            return new Rpcs3BuildIdentity(
                VersionText: newest.VersionText,
                BuildNumber: newest.BuildNumber,
                Commit: newest.Commit,
                Stage: newest.Stage,
                Branch: newest.Branch,
                Architecture: newest.Architecture,
                State: Rpcs3BuildIdentityState.Conflict,
                Evidence: evidence,
                Diagnostic:
                    $"{distinctIdentities} RPCS3 log files reported different build identities.");
        }

        if (unparseableCount > 0)
        {
            return new Rpcs3BuildIdentity(
                VersionText: newest.VersionText,
                BuildNumber: newest.BuildNumber,
                Commit: newest.Commit,
                Stage: newest.Stage,
                Branch: newest.Branch,
                Architecture: newest.Architecture,
                State: Rpcs3BuildIdentityState.Partial,
                Evidence: evidence,
                Diagnostic:
                    $"{unparseableCount} additional RPCS3 log file(s) were found without a supported build banner.");
        }

        return new Rpcs3BuildIdentity(
            VersionText: newest.VersionText,
            BuildNumber: newest.BuildNumber,
            Commit: newest.Commit,
            Stage: newest.Stage,
            Branch: newest.Branch,
            Architecture: newest.Architecture,
            State: Rpcs3BuildIdentityState.Exact,
            Evidence: evidence);
    }

    private static List<string> ReadPrefixLines(
        string path)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);

        var byteCount = (int)Math.Min(
            PrefixByteLimit,
            stream.Length);

        if (byteCount <= 0)
        {
            return [];
        }

        var buffer = new byte[byteCount];
        var offset = 0;

        while (offset < buffer.Length)
        {
            var read = stream.Read(
                buffer,
                offset,
                buffer.Length - offset);

            if (read <= 0)
            {
                break;
            }

            offset += read;
        }

        var text = Encoding.UTF8.GetString(
            buffer,
            0,
            offset);

        using var reader = new StringReader(text);

        var lines = new List<string>(
            PrefixLineLimit);

        while (
            lines.Count < PrefixLineLimit &&
            reader.ReadLine() is { } line)
        {
            lines.Add(line);
        }

        return lines;
    }
}
