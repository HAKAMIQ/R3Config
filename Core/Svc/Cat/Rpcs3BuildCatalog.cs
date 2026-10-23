using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using R3Config.Mdl;

#pragma warning disable IDE0130

namespace R3Config.Svc;

// One published RPCS3 binary. Version is "X.Y.Z"; Build is the numeric commit
// counter from the same release tag. Platform names the archive, so a Windows
// archive is never offered to another host.
// SourceClass says where the row came from and is never blurred: an official
// GitHub binary is directly downloadable; a historical Internet Archive row is
// reference evidence only and is never offered as a download. A null
// SourceClass is an official GitHub row (schema 1 data).
public sealed record Rpcs3BuildEntry(
    string Version,
    int Build,
    string Platform,
    string DownloadUrl,
    string? FileName = null,
    string? CommitFull = null,
    long? SizeBytes = null,
    string? Sha256 = null,
    string? SourceClass = null,
    string? MatchMethod = null,
    string? Verification = null,
    bool HistoricalUnavailable = false,
    string? Sha1 = null);

public enum Rpcs3BuildResolutionState
{
    // No exact, unambiguous, policy-approved row: nothing is offered.
    Unresolved,

    // The exact official GitHub binary of this Version+Build.
    ResolvedOfficialGitHub,

    // The build is known only from a verified historical Internet Archive row.
    // It is NOT an official GitHub binary and no download is offered.
    HistoricalArchiveEvidenceOnly
}

public sealed record Rpcs3BuildResolution(
    Rpcs3BuildResolutionState State,
    Rpcs3BuildEntry? Entry,
    string? DownloadUrl,
    string Reason)
{
    public static Rpcs3BuildResolution Unresolved(string reason) =>
        new(Rpcs3BuildResolutionState.Unresolved, null, null, reason);
}

public sealed record Rpcs3BuildDocument(
    int Schema,
    string Source,
    IReadOnlyList<Rpcs3BuildEntry> Builds,
    IReadOnlyDictionary<string, int>? Counts = null);

// Shipped, minimal catalog of published RPCS3 builds (Data/rpcs3-builds.json).
// It resolves the "X.Y.Z-BUILD" text a Wiki table names to the exact official
// archive, or to nothing. It is data-driven and game-agnostic: no URL is ever
// constructed, guessed or read from the network.
public sealed class Rpcs3BuildCatalog
{
    public const int SchemaVersion = 2;
    public const string FileName = "rpcs3-builds.json";
    public const string WindowsPlatform = "Windows";
    public const string GitHubDirectClass = "OFFICIAL_GITHUB_DIRECT";
    public const string ArchivePointerClass = "OFFICIAL_SITE_ARCHIVE_POINTER";
    public const string ArchiveMetadataVerified = "ARCHIVE_METADATA_VERIFIED";
    public const string ArchiveIdentityVerified = "ARCHIVE_IDENTITY_VERIFIED";

    private static readonly Regex BuildText = new(
        @"^\s*v?(?<version>\d+\.\d+\.\d+(?:\.\d+)?)-(?<build>\d+)\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly IReadOnlyList<Rpcs3BuildEntry> _builds;

    public Rpcs3BuildCatalog(IEnumerable<Rpcs3BuildEntry> builds)
    {
        ArgumentNullException.ThrowIfNull(builds);
        _builds = [.. builds];
    }

    public static Rpcs3BuildCatalog Empty { get; } = new([]);

    public int Count => _builds.Count;

    public IReadOnlyList<Rpcs3BuildEntry> Builds => _builds;

    public static bool TryParse(string? text, out string version, out int build)
    {
        version = string.Empty;
        build = 0;

        var match = text is null ? Match.Empty : BuildText.Match(text);
        if (!match.Success ||
            !int.TryParse(
                match.Groups["build"].Value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out build))
        {
            return false;
        }

        version = match.Groups["version"].Value;
        return true;
    }

    // Only the official RPCS3 Windows binaries repository on GitHub, by HTTPS.
    public static bool IsOfficialWindowsBinaryUrl(string? url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
               uri.Scheme == Uri.UriSchemeHttps &&
               uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) &&
               string.IsNullOrEmpty(uri.UserInfo) &&
               uri.IsDefaultPort &&
               string.IsNullOrEmpty(uri.Query) &&
               string.IsNullOrEmpty(uri.Fragment) &&
               uri.AbsolutePath.StartsWith(
                   "/RPCS3/rpcs3-binaries-win/releases/download/",
                   StringComparison.Ordinal);
    }

    private static bool IsGitHubRow(Rpcs3BuildEntry entry) =>
        entry.SourceClass is null ||
        string.Equals(entry.SourceClass, GitHubDirectClass, StringComparison.Ordinal);

    private static bool IsVerifiedArchiveRow(Rpcs3BuildEntry entry) =>
        string.Equals(entry.SourceClass, ArchivePointerClass, StringComparison.Ordinal) &&
        (string.Equals(entry.Verification, ArchiveMetadataVerified, StringComparison.Ordinal) ||
         string.Equals(entry.Verification, ArchiveIdentityVerified, StringComparison.Ordinal));

    // Exact Version+Build resolution and nothing else: no nearest/latest/fuzzy
    // build, no constructed URL. A GitHub row always wins over Archive
    // evidence. Ambiguous, conflicting, unapproved or non-Windows input is
    // Unresolved.
    public Rpcs3BuildResolution Resolve(string? testedBuildText, string? hostPlatform)
    {
        if (!string.Equals(hostPlatform?.Trim(), WindowsPlatform, StringComparison.OrdinalIgnoreCase))
        {
            return Rpcs3BuildResolution.Unresolved("UnsupportedPlatform");
        }

        if (!TryParse(testedBuildText, out var version, out var build))
        {
            return Rpcs3BuildResolution.Unresolved("NotABuildText");
        }

        var rows = _builds
            .Where(entry =>
                entry.Build == build &&
                string.Equals(entry.Version, version, StringComparison.Ordinal) &&
                string.Equals(entry.Platform, WindowsPlatform, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (rows.Length == 0)
        {
            return Rpcs3BuildResolution.Unresolved("NotInCatalog");
        }

        var github = rows.Where(IsGitHubRow).ToArray();
        if (github.Length > 0)
        {
            if (github.Select(row => row.DownloadUrl).Distinct(StringComparer.Ordinal).Count() != 1)
            {
                return Rpcs3BuildResolution.Unresolved("ConflictingRows");
            }

            var url = github[0].DownloadUrl;
            return IsOfficialWindowsBinaryUrl(url)
                ? new Rpcs3BuildResolution(
                    Rpcs3BuildResolutionState.ResolvedOfficialGitHub,
                    github[0],
                    url,
                    github[0].MatchMethod ?? "EXACT_VERSION_BUILD")
                : Rpcs3BuildResolution.Unresolved("UrlNotApproved");
        }

        var archive = rows.Where(IsVerifiedArchiveRow).ToArray();
        return archive.Length == 1
            ? new Rpcs3BuildResolution(
                Rpcs3BuildResolutionState.HistoricalArchiveEvidenceOnly,
                archive[0],
                null,
                "HistoricalArchiveEvidenceOnly")
            : Rpcs3BuildResolution.Unresolved(archive.Length == 0 ? "NotEligible" : "AmbiguousArchiveRows");
    }

    // Exact resolution by build number alone, for evidence that documents a
    // build without its version (a fixed or known-good build). A build number
    // is the commit count and unique per commit, so it resolves only when
    // exactly one official GitHub row carries it; anything else is Unresolved.
    public Rpcs3BuildResolution ResolveBuildNumber(int build, string? hostPlatform)
    {
        if (!string.Equals(hostPlatform?.Trim(), WindowsPlatform, StringComparison.OrdinalIgnoreCase))
        {
            return Rpcs3BuildResolution.Unresolved("UnsupportedPlatform");
        }

        var rows = _builds
            .Where(entry => entry.Build == build &&
                            IsGitHubRow(entry) &&
                            string.Equals(entry.Platform, WindowsPlatform, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        return rows.Length == 1
            ? Resolve($"{rows[0].Version}-{rows[0].Build}", hostPlatform)
            : Rpcs3BuildResolution.Unresolved(rows.Length == 0 ? "NotInCatalog" : "AmbiguousBuildNumber");
    }

    // The exact official GitHub archive URL for the tested build, or null. Null
    // covers: text that is not "X.Y.Z-BUILD", an unsupported platform, no row,
    // conflicting rows, an Archive-only build, and any row whose URL is not an
    // official binaries URL.
    public string? ResolveDownloadUrl(string? testedBuildText, string? hostPlatform) =>
        Resolve(testedBuildText, hostPlatform).DownloadUrl;

    public static Rpcs3BuildCatalog Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var bytes = R3Bound.ReadBytes(
            path,
            R3Limits.PatCatBy,
            "RPCS3 build catalog");

        Rpcs3BuildDocument? document;
        try
        {
            document = JsonSerializer.Deserialize<Rpcs3BuildDocument>(bytes, Options);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("RPCS3 build catalog JSON is invalid.", ex);
        }

        if (document is null ||
            (document.Schema != SchemaVersion && document.Schema != 1) ||
            document.Builds is null ||
            document.Builds.Any(entry => entry is null))
        {
            throw new InvalidDataException(
                "RPCS3 build catalog is empty, invalid, or an unsupported schema.");
        }

        return new Rpcs3BuildCatalog(document.Builds);
    }
}

#pragma warning restore IDE0130
