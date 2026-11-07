using System;
using System.Linq;
using R3Config.Lang;
using R3Config.Mdl;
using R3Config.Vm;
using static R3Config.Tests.TestAssert;

namespace R3Config.Tests;

// The rejection/manual reason is shown inside the existing row ToolTip only.
// It must be localized, empty for rows without a reason, and never raw codes.
internal static class PlanReasonT
{
    private static PlanItVm Row(RecState state, SettingAction action, params string[] reasons)
    {
        var change = new CfgChg("Video", "Resolution Scale", "150", "Resolution Scale", null, RecRisk.Rec);
        var decision = new SettingDecision(
            action,
            ApplicabilityLevel.Unknown,
            EvidenceKind.CurrentWikiConfig,
            EvidenceAuthority.Wiki,
            reasons);
        var item = new RecItem(
            "BLUS00001", change.Path, "100", "150", RecRisk.Rec, state,
            IsDiff: true, IsAction: state != RecState.Blocked, IsSel: false, Notes: string.Empty,
            Decision: decision);
        return new PlanItVm(new CfgEntry(change, "100", item), isSelected: false);
    }

    public static void Run()
    {
        var original = UiText.CurLang;
        try
        {
            string[] texts = [];
            foreach (var language in new[] { UiText.EnLang, UiText.ArabLang })
            {
                UiText.SetLang(language);
                var mismatch = Row(RecState.Blocked, SettingAction.Blocked, "Serial:Match", "Version:Mismatch");
                var conflict = Row(RecState.Blocked, SettingAction.Blocked, "EvidenceConflict");
                var identity = Row(RecState.Blocked, SettingAction.Blocked, "IdentityNotMatched");
                var manual = Row(RecState.Manual, SettingAction.Suggest, "Serial:Match");
                var none = Row(RecState.Pending, SettingAction.Apply, "Serial:Match");

                Assert(mismatch.HasReason && conflict.HasReason && identity.HasReason && manual.HasReason &&
                       !none.HasReason && none.ReasonText.Length == 0,
                    "Blocked and manual rows must expose a reason; auto-applicable rows must not.");
                Assert(new[] { mismatch, conflict, identity, manual }
                           .Select(row => row.ReasonText).Distinct().Count() == 4 &&
                       !mismatch.ReasonText.Contains("Mismatch", StringComparison.Ordinal) &&
                       !mismatch.ReasonText.Contains("Plan.Reason", StringComparison.Ordinal),
                    "Each reason must be a distinct localized sentence, not a code or a missing key.");
                texts = [.. texts, mismatch.ReasonText];
            }

            Assert(texts[0] != texts[1], "English and Arabic reasons must differ.");
        }
        finally
        {
            UiText.SetLang(original);
        }
    }
}
