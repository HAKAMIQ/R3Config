using System;
using R3Config.Lang;
using R3Config.Mdl;
using R3Config.Svc;
using R3Config.Vm;
using static R3Config.Tests.TestAssert;

namespace R3Config.Tests;

// Round-6 import of the reviewed Wiki-note Arabic (R3Config-WikiNotes-ar-reviewed.csv).
// Only ar_approved differing from the English source is imported; a row whose
// approved text equals its English source stays untranslated (RPCN dates, versions).
internal static class ReviewedNotesImportT
{
    // new / no flag.
    private const string NoIssues = "There are no reported issues with this title.";
    // The App gate forbids Arabic diacritics, so the approved text is imported without them.
    private const string NoIssuesAr = "لا توجد مشكلات مبلغ عنها لهذه اللعبة.";

    // new / OnlineNote, shown through GameOnlineVm.Notes.
    private const string OnlineNote = "Requires custom IP/Hosts switches.";
    private const string OnlineNoteAr = "تتطلب إعدادات IP/Hosts Switches مخصصة.";

    // new / flag=skip with approved text different from English.
    private const string SkipDiff = "[https://wiki.aiwarehouse.xyz/ Warehouse Website]";
    private const string SkipDiffAr = "[https://wiki.aiwarehouse.xyz/ موقع Warehouse]";

    // existing rows whose approved Arabic replaced the current one, per source table.
    private const string FromArByEn = "Small performance gain.";
    private const string FromArByEnAr = "يوفر تحسنا بسيطا في الأداء.";
    private const string FromOfficial = "Slightly improves performance.";
    private const string FromOfficialAr = "يحسن الأداء بدرجة طفيفة.";
    private const string FromWiki = "Fixes visual problems in-game.";
    private const string FromWikiAr = "يصلح المشكلات الرسومية داخل اللعبة.";

    // flag=skip with approved == English: not imported.
    private const string SkipSame = "RPCN 1.4.0 - 2025/02/12";

    public static void Run()
    {
        var original = UiText.CurLang;
        try
        {
            NoteSvc.SetSyncedWikiArabic(null);
            UiText.SetLang(UiText.ArabLang);

            AssertExact(NoIssues, NoIssuesAr);
            AssertExact(OnlineNote, OnlineNoteAr);
            AssertExact(SkipDiff, SkipDiffAr);
            AssertExact(FromArByEn, FromArByEnAr);
            AssertExact(FromOfficial, FromOfficialAr);
            AssertExact(FromWiki, FromWikiAr);

            var online = new GameOnlineVm(new WikiOnlineEvidence(
                "Page", WikiOnlineState.Supported, "1.00", null, "N/A",
                "Yes", "Yes", "Yes", "Yes", "Not required", OnlineNote));
            Assert(online.Notes == OnlineNoteAr,
                "An imported Online note must show its reviewed Arabic in the Online table.");

            var same = NoteSvc.Trans(SkipSame);
            Assert(same.Quality != NoteQual.Exact,
                "A row whose approved text equals its English source must not be imported.");

            UiText.SetLang(UiText.EnLang);
            Assert(NoteSvc.Trans(NoIssues).Body == NoIssues,
                "English UI must keep the original Wiki note.");
        }
        finally
        {
            NoteSvc.SetSyncedWikiArabic(null);
            UiText.SetLang(original);
        }
    }

    private static void AssertExact(string english, string arabic)
    {
        var result = NoteSvc.Trans(english);
        Assert(result.Quality == NoteQual.Exact && result.Body == arabic,
            $"Reviewed Arabic was not imported for '{english}'. Quality={result.Quality}, Body='{result.Body}'.");
    }
}
