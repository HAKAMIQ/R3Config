using R3Config.Mdl;

namespace R3Config.Svc;

public static class NclSemanticVerifier
{
    public static NclVerifyResult Verify(NclDoc document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var items = document.Cheats
            .Select(VerifyCheat)
            .ToArray();

        return new NclVerifyResult(
            items,
            items.Count(item => item.Status == NclVerifyStat.OfflineStaticCandidate),
            items.Count(item => item.Status == NclVerifyStat.RuntimeEvidenceRequired),
            items.Count(item => item.Status == NclVerifyStat.ConstantRuntimeRequired),
            items.Count(item => item.Status == NclVerifyStat.OptionSelectionRequired),
            items.Count(item => item.Status == NclVerifyStat.NonExecutableMetadata),
            items.Count(item => item.Status == NclVerifyStat.Unsupported),
            items.Count(item => item.Status == NclVerifyStat.Invalid));
    }

    private static NclVerifyItem VerifyCheat(NclCheat cheat)
    {
        var kinds = cheat.Operations
            .Select(item => item.Kind)
            .Distinct()
            .ToArray();
        var reasons = new List<string>();

        if (cheat.HasErr)
        {
            reasons.Add("NCL structure or operation parsing is invalid.");
            return new NclVerifyItem(
                cheat.Name,
                NclVerifyStat.Invalid,
                kinds,
                reasons);
        }

        if (cheat.Options.Count > 0)
        {
            reasons.Add(
                "The source contains Artemis/NetCheat option placeholders; an explicit option selection must be resolved before executable semantics can be verified.");
            return new NclVerifyItem(
                cheat.Name,
                NclVerifyStat.OptionSelectionRequired,
                kinds,
                reasons);
        }

        if (cheat.Operations.Count == 0)
        {
            reasons.Add(
                "The source segment contains no executable NCL operation after dialect-specific filtering and is retained as metadata only.");
            return new NclVerifyItem(
                cheat.Name,
                NclVerifyStat.NonExecutableMetadata,
                kinds,
                reasons);
        }

        if (cheat.Operations.Any(item => item is NclUnsupportedOp))
        {
            reasons.Add("At least one executable opcode has no verified semantic model.");
            return new NclVerifyItem(
                cheat.Name,
                NclVerifyStat.Unsupported,
                kinds,
                reasons);
        }

        if (cheat.ConstantWrite)
        {
            reasons.Add("The NCL block requests constant runtime execution.");
            return new NclVerifyItem(
                cheat.Name,
                NclVerifyStat.ConstantRuntimeRequired,
                kinds,
                reasons);
        }

        var runtimeOps = cheat.Operations
            .Where(NeedsRuntime)
            .Select(item => item.Kind)
            .Distinct()
            .ToArray();
        if (runtimeOps.Length > 0)
        {
            reasons.Add(
                "Runtime memory evidence is required by: " +
                string.Join(", ", runtimeOps));
            return new NclVerifyItem(
                cheat.Name,
                NclVerifyStat.RuntimeEvidenceRequired,
                kinds,
                reasons);
        }

        reasons.Add(
            "Semantics are deterministic offline, but target identity, PPU/module hash, address mapping, and RPCS3-equivalent validation are still required before conversion.");
        return new NclVerifyItem(
            cheat.Name,
            NclVerifyStat.OfflineStaticCandidate,
            kinds,
            reasons);
    }

    private static bool NeedsRuntime(NclOp operation) =>
        operation switch
        {
            NclDirectWrite direct => direct.Mode != NclWriteMode.Replace,
            NclPointerResolve => true,
            NclCopyBuffer => true,
            NclFindReplace => true,
            NclConditionalEqual => true,
            NclConditionalMaskUnset => true,
            NclCopyBytes => true,
            _ => false
        };
}
