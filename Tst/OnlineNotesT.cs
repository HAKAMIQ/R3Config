using System;
using R3Config.Lang;
using R3Config.Mdl;
using R3Config.Svc;
using R3Config.Vm;
using static R3Config.Tests.TestAssert;

namespace R3Config.Tests;

// Online/RPCN table notes (Online[].Notes) must use the same reviewed-translation
// path as free-text Wiki notes: a reviewed Arabic text is shown in Arabic, and
// an untranslated one keeps the original Wiki wording (no machine translation).
internal static class OnlineNotesT
{
    private const string Reviewed = "Requires OpenSpy patch.";
    private const string ReviewedAr = "يتطلب باتش OpenSpy.";
    private const string Untranslated = "RPCN 1.4.0 - 2025/02/12";

    public static void Run()
    {
        var original = UiText.CurLang;
        try
        {
            NoteSvc.SetSyncedWikiArabic([new RecSyncGitHubNote(Reviewed, ReviewedAr)]);

            UiText.SetLang(UiText.ArabLang);
            Assert(Notes(Reviewed) == ReviewedAr,
                "Arabic UI must show the reviewed Arabic text of an online note.");
            Assert(Notes(Untranslated) == Untranslated,
                "An online note without a reviewed translation must keep the original Wiki text.");
            Assert(new GameOnlineVm(Evidence(null)).Notes.Length == 0 &&
                   !new GameOnlineVm(Evidence(null)).HasNotes,
                "A missing online note stays empty.");

            UiText.SetLang(UiText.EnLang);
            Assert(Notes(Reviewed) == Reviewed,
                "English UI must show the original online note.");
        }
        finally
        {
            NoteSvc.SetSyncedWikiArabic(null);
            UiText.SetLang(original);
        }
    }

    private static string Notes(string text) =>
        new GameOnlineVm(Evidence(text)).Notes;

    private static WikiOnlineEvidence Evidence(string? notes) =>
        new("Page", WikiOnlineState.Supported, "1.00", null, "N/A",
            "Yes", "Yes", "Yes", "Yes", "Not required", notes);
}
