using System.IO;
using System.Windows;
using R3Config.Lang;
using R3Config.Mdl;
using R3Config.Svc;
using R3Config.Vm;

internal static partial class Program
{
    // Reviewed Official text (PNote.Official.Ar.cs).
    private const string ReviewedOfficialNote =
        "Requires \"Stretch To Display Area\" checked in the GPU settings. UI elements will be stretched.";

    // Upstream Official text with no reviewed Arabic (a real RPCS3 patch.yml note).
    private const string UpstreamOfficialNote =
        "Requires \"Stretch To Display Area\" checked in the GPU settings. HUD elements stay at 16:9. Sometimes text doesn't show up.";

    private const string UpstreamOfficialDescription =
        "Synthetic upstream description used only by the Official note fallback regression.";

    // Upstream RPCS3 Official notes: reviewed Arabic when it exists, otherwise
    // the upstream text verbatim with an Official provenance caption. The
    // fallback never applies to R3Config-owned text or to non-Official sources.
    static void PatchText_OfficialUpstreamFallback()
    {
        PatchText_WithLang(
            UiText.ArabLang,
            () =>
            {
                var reviewed = OfficialNoteItem(PatSrc.Official, ReviewedOfficialNote);
                Assert(!reviewed.IsOfficialNoteFallback &&
                       !reviewed.ShowOfficialNoteSource &&
                       NoteSvc.ContainsArabic(reviewed.LocMsg) &&
                       reviewed.InsText == reviewed.LocMsg &&
                       reviewed.InsFlow == FlowDirection.RightToLeft,
                    "A reviewed Official note must show its reviewed Arabic text in the interface direction.");

                Assert(NoteSvc.Trans(UpstreamOfficialNote).Quality == NoteQual.Fallback &&
                       !File.ReadAllText(Path.Combine(RepoRoot(), "App", "Lang", "ar-SA.json"))
                           .Contains(UpstreamOfficialNote, StringComparison.Ordinal),
                    "The upstream fixture note must have no reviewed Arabic and must not be copied into ar-SA.");

                var upstream = OfficialNoteItem(PatSrc.Official, UpstreamOfficialNote);
                Assert(upstream.IsOfficialNoteFallback &&
                       string.Equals(upstream.LocMsg, UpstreamOfficialNote, StringComparison.Ordinal) &&
                       upstream.HasIns &&
                       string.Equals(upstream.InsText, UpstreamOfficialNote, StringComparison.Ordinal) &&
                       upstream.InsFlow == FlowDirection.LeftToRight &&
                       upstream.ShowOfficialNoteSource &&
                       NoteSvc.ContainsArabic(upstream.OfficialNoteSourceLabel) &&
                       upstream.OfficialNoteSourceLabel != "Patch.Note.OfficialOriginal",
                    "An Official note without reviewed Arabic must show the upstream text verbatim with its Official provenance caption.");

                Assert(upstream.Match.Patch.Source == PatSrc.Official &&
                       upstream.PatSrcKd == PatSrcKd.Official &&
                       upstream.IsOfcl,
                    "The upstream fallback must keep its Official source and provenance.");

                var description = OfficialNoteItem(PatSrc.Official, UpstreamOfficialDescription);
                Assert(description.IsOfficialNoteFallback &&
                       string.Equals(description.LocMsg, UpstreamOfficialDescription, StringComparison.Ordinal) &&
                       string.Equals(description.NoteBody, UpstreamOfficialDescription, StringComparison.Ordinal) &&
                       description.HasDesc,
                    "An Official description without reviewed Arabic must not be silently hidden.");

                foreach (var empty in new string?[] { null, "   " })
                {
                    var item = OfficialNoteItem(PatSrc.Official, empty);
                    Assert(!item.IsOfficialNoteFallback &&
                           string.IsNullOrEmpty(item.LocMsg) &&
                           !item.HasIns &&
                           !item.ShowOfficialNoteSource,
                        "An Official note is empty only when its upstream source is empty.");
                }

                foreach (var source in new[] { PatSrc.Imported, PatSrc.Artemis, PatSrc.Can })
                {
                    var other = OfficialNoteItem(source, UpstreamOfficialNote);
                    Assert(!other.IsOfficialNoteFallback &&
                           string.IsNullOrEmpty(other.LocMsg) &&
                           !other.ShowOfficialNoteSource,
                        $"The upstream English fallback must stay scoped to Official notes, not {source}.");
                }

                const string missingKey = "Patch.Note.MissingOwnedKeyProbe";
                Assert(UiText.GetText(missingKey) == missingKey,
                    "A missing R3Config-owned key must surface as its raw key for the localization gates, never as English fallback text.");
            });

        PatchText_WithLang(
            UiText.EnLang,
            () =>
            {
                var upstream = OfficialNoteItem(PatSrc.Official, UpstreamOfficialNote);
                Assert(!upstream.IsOfficialNoteFallback &&
                       !upstream.ShowOfficialNoteSource &&
                       string.Equals(upstream.LocMsg, UpstreamOfficialNote, StringComparison.Ordinal) &&
                       upstream.OfficialNoteSourceLabel != "Patch.Note.OfficialOriginal",
                    "English shows the upstream Official note as its own text, and the provenance key exists in en-US.");
            });

        Console.WriteLine("Official upstream note fallback (reviewed Arabic, verbatim upstream, provenance, empty, scope, owned keys) PASS.");
    }

    // Owner decision 2026-09-25, applied to the unknown-requirement fixture of
    // PatchText_UnknownAndDiagnosticTextDoesNotLeak: an upstream Official note
    // with no reviewed Arabic is shown verbatim under its Official provenance
    // caption; every other source keeps the reviewed-only surface, and R3Config
    // internal hash diagnostics are never shown through the fallback.
    static void PatchText_AssertUnknownOfficialRequirement(
        PatItVm requiredItem,
        string requiredRaw)
    {
        Assert(requiredItem.HasIns &&
               string.Equals(requiredItem.InsText, requiredRaw, StringComparison.Ordinal) &&
               requiredItem.IsOfficialNoteFallback &&
               requiredItem.ShowOfficialNoteSource &&
               requiredItem.InsFlow == FlowDirection.LeftToRight,
            "An unknown upstream Official requirement must be shown verbatim with its Official provenance, not hidden.");

        var requiredLocal = OfficialNoteItem(PatSrc.Imported, requiredRaw);
        Assert(!requiredLocal.HasIns &&
               string.IsNullOrEmpty(requiredLocal.InsText),
            "An unknown required instruction from a non-Official source exposed untranslated content on the primary Arabic surface.");

        var diagnosticItem = OfficialNoteItem(
            PatSrc.Official,
            "Requires HASH_UNKNOWN: HASHMISMATCH review");
        Assert(!diagnosticItem.IsOfficialNoteFallback &&
               !diagnosticItem.LocMsg.Contains("HASH", StringComparison.OrdinalIgnoreCase),
            "R3Config internal hash diagnostics must never be shown through the upstream Official fallback.");
    }

    private static PatItVm OfficialNoteItem(
        PatSrc source,
        string? note)
    {
        var notes = note is null
            ? string.Empty
            : "    Notes: '" + note.Replace("'", "''", StringComparison.Ordinal) + "'\n";
        var yaml =
            "Version: 1.2\n" +
            "PPU-1111111111111111111111111111111111111111:\n" +
            "  \"Fallback Fixture\":\n" +
            "    Games:\n" +
            "      \"Patch Localization Fixture\":\n" +
            "        BLUS00001: [ 01.00 ]\n" +
            "    Author: Upstream\n" +
            notes +
            "    Patch Version: 1.0\n" +
            "    Patch:\n" +
            "      - [ be32, 0x00010000, 0x60000000 ]\n";
        var read = new ReadSvc().ReadText(yaml, source, source + ".yml");
        Assert(read.IsOk && read.Catalog.Entries.Count == 1,
            "Expected the Official note fallback fixture to parse: " + read.Error);
        return new PatItVm(PatchText_Match(read.Catalog.Entries[0]));
    }
}
