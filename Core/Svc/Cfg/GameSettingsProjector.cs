using System;
using System.Collections.Generic;
using System.Linq;
using R3Config.Mdl;

#pragma warning disable IDE0130

namespace R3Config.Svc;

// Turns one game's config plan plus its Wiki page evidence into the model the
// Game Settings screen renders: settings grouped under their real RPCS3 tab,
// free-text Wiki notes with their scope, and online/RPCN evidence. Notes and
// online evidence are never converted into settings, and a setting appears in
// exactly one tab.
public static class GameSettingsProjector
{
    private static readonly Rpcs3SettingsSection[] SectionOrder =
        Enum.GetValues<Rpcs3SettingsSection>();

    public static GameSettingsModel Project(
        CfgPlan? plan,
        WikiPageEvidence? evidence,
        GameSettingsContext context,
        Rpcs3BuildCatalog? builds = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var rows = plan is null
            ? []
            : plan.Items.Select(Row).ToArray();

        var sections = SectionOrder
            .Select(section => new GameSettingsSection(
                section,
                rows.Where(row => row.Section == section).ToArray()))
            .Where(section => section.Rows.Count > 0)
            .ToArray();

        var notes = (evidence?.Notes ?? [])
            .DistinctBy(note => note.Text, StringComparer.Ordinal)
            .Select(note => new GameNote(note, Applicability(note, context)))
            .OrderBy(note => note.Applicability)
            .ToArray();

        var online = evidence?.Online ?? [];
        var applicableOnline = online.Where(item => OnlineApplies(item, context)).ToArray();

        return new GameSettingsModel(
            context.Serial,
            evidence?.Page,
            sections,
            notes,
            online,
            applicableOnline,
            OnlineVersionUnknown: string.IsNullOrWhiteSpace(context.GameVersion),
            ExactBuildUrls: ExactBuildUrls(applicableOnline, context, builds),
            CurrentRpcs3Build: context.CurrentRpcs3Build)
        {
            // Carried straight through: a range the Wiki wrote is evidence, so
            // it is never matched against a section, a row or a current value.
            Guidance = plan?.Hints ?? []
        };
    }

    // Exact-build downloads are resolved only for applicable RPCN evidence and
    // only from the shipped catalog. No catalog, no row, or another platform
    // resolves nothing.
    private static Dictionary<string, string> ExactBuildUrls(
        IEnumerable<WikiOnlineEvidence> applicable,
        GameSettingsContext context,
        Rpcs3BuildCatalog? builds)
    {
        var urls = new Dictionary<string, string>(StringComparer.Ordinal);
        if (builds is null)
        {
            return urls;
        }

        foreach (var text in applicable
                     .Select(item => item.Rpcs3VersionTested)
                     .Where(text => !string.IsNullOrWhiteSpace(text))
                     .Distinct(StringComparer.Ordinal))
        {
            var url = builds.ResolveDownloadUrl(text, context.HostPlatform);
            if (url is not null)
            {
                urls[text!] = url;
            }
        }

        return urls;
    }

    public static GameSettingRow Row(CfgEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return new GameSettingRow(
            entry,
            SettingsSectionMap.Classify(entry.Change, out var fallback),
            SettingsSectionMap.ClassifyRole(entry.Change),
            fallback);
    }

    // A netplay table is for one tested game version. It applies only when that
    // version is the installed one, or when the table states no version. An
    // unknown installed version confirms nothing, so it never applies.
    public static bool OnlineApplies(
        WikiOnlineEvidence evidence,
        GameSettingsContext context)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        ArgumentNullException.ThrowIfNull(context);

        return string.IsNullOrWhiteSpace(evidence.TestedGameVersion) ||
               NoteScope.SameVersion(evidence.TestedGameVersion, context.GameVersion);
    }

    // A note is General only when everything it is scoped to is confirmed by
    // the detected context. Anything else keeps the evidence but is hidden.
    public static WikiNoteApplicability Applicability(
        WikiFreeTextNote note,
        GameSettingsContext context)
    {
        ArgumentNullException.ThrowIfNull(note);
        ArgumentNullException.ThrowIfNull(context);

        if (!string.IsNullOrWhiteSpace(note.PlatformCondition) &&
            !string.Equals(
                NormalizePlatform(note.PlatformCondition),
                NormalizePlatform(context.HostPlatform),
                StringComparison.OrdinalIgnoreCase))
        {
            return WikiNoteApplicability.OtherPlatform;
        }

        if (!string.IsNullOrWhiteSpace(note.SerialCondition) &&
            !string.Equals(
                note.SerialCondition,
                context.Serial,
                StringComparison.OrdinalIgnoreCase))
        {
            return WikiNoteApplicability.OtherSerial;
        }

        // A "version" that is really an RPCS3 build number is a build scope.
        var versionIsBuild = note.VersionCondition is not null &&
            note.VersionCondition.StartsWith("0.0.", StringComparison.Ordinal);
        if (!string.IsNullOrWhiteSpace(note.VersionCondition) && !versionIsBuild && NoteScope.IsGameVersion(note.VersionCondition))
        {
            var versionMatch = NoteScope.VersionApplies(
                note.Text,
                note.VersionCondition,
                context.GameVersion);
            if (versionMatch is null)
            {
                return WikiNoteApplicability.Unresolved;
            }

            if (!versionMatch.Value)
            {
                return WikiNoteApplicability.OtherVersion;
            }
        }

        if (!string.IsNullOrWhiteSpace(note.EditionCondition))
        {
            var editionMatch = NoteScope.EditionApplies(note.EditionCondition, context.Serial);
            if (editionMatch is null)
            {
                return WikiNoteApplicability.Unresolved;
            }

            if (!editionMatch.Value)
            {
                return WikiNoteApplicability.OtherEdition;
            }
        }

        if (!string.IsNullOrWhiteSpace(note.HardwareCondition))
        {
            var hardwareMatch = context.Hardware is not null
                ? NoteScope.HardwareApplies(note.HardwareCondition, context.Hardware)
                : NoteScope.HardwareApplies(note.HardwareCondition, context.GpuName);
            if (hardwareMatch is null)
            {
                return WikiNoteApplicability.Unresolved;
            }

            if (!hardwareMatch.Value)
            {
                return WikiNoteApplicability.OtherHardware;
            }
        }

        var buildCondition = !string.IsNullOrWhiteSpace(note.Rpcs3BuildCondition)
            ? note.Rpcs3BuildCondition
            : versionIsBuild ? note.VersionCondition : null;
        if (buildCondition is not null)
        {
            var buildMatch = NoteScope.Rpcs3BuildApplies(note.Text, buildCondition, context.RuntimeBuild);
            if (buildMatch is null)
            {
                return WikiNoteApplicability.Unresolved;
            }

            if (!buildMatch.Value)
            {
                return WikiNoteApplicability.OtherBuild;
            }
        }

        return WikiNoteApplicability.General;
    }

    private static string NormalizePlatform(string value)
    {
        var text = value.Trim();
        return text.Equals("MacOS", StringComparison.OrdinalIgnoreCase) ||
               text.Equals("Mac OS", StringComparison.OrdinalIgnoreCase) ||
               text.Equals("OS X", StringComparison.OrdinalIgnoreCase)
            ? "macOS"
            : text;
    }

    // Resolves the page evidence for a game. The Wiki page the settings came
    // from wins; a serial lookup is only trusted when exactly one page lists
    // the serial, so a serial mentioned on an unrelated page never leaks notes.
    public static WikiPageEvidence? Find(
        IReadOnlyList<WikiPageEvidence> pages,
        string serial,
        string? wikiPage)
    {
        ArgumentNullException.ThrowIfNull(pages);

        if (!string.IsNullOrWhiteSpace(wikiPage))
        {
            var byPage = pages.FirstOrDefault(page =>
                string.Equals(page.Page, wikiPage, StringComparison.OrdinalIgnoreCase));
            if (byPage is not null)
            {
                return byPage;
            }
        }

        var bySerial = pages
            .Where(page => page.Serials.Contains(serial, StringComparer.OrdinalIgnoreCase))
            .Take(2)
            .ToArray();

        return bySerial.Length == 1 ? bySerial[0] : null;
    }
}

#pragma warning restore IDE0130
