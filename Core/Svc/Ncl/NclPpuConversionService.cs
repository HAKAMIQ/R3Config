using R3Config.Mdl;

namespace R3Config.Svc;

public static class NclPpuConversionService
{
    private const int MaxRunKeyCandidates = 64;

    public static NclPpuConversionResult ConvertFile(
        NclPpuConversionFileRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.NclPath) ||
            string.IsNullOrWhiteSpace(request.RpcS3LogPath) ||
            request.Target is null)
        {
            return Failure(
                NclPpuConversionCode.InvalidRequest,
                request.CheatIndex,
                "NCL path, RPCS3 log path, and conversion target are required.");
        }

        if (!ValidateTarget(request.Target, out var targetError))
        {
            return Failure(
                NclPpuConversionCode.InvalidRequest,
                request.CheatIndex,
                targetError);
        }

        if (!ValidateOptionSelection(
                request.OptionSelection,
                request.CheatIndex,
                out var selectionError))
        {
            return Failure(
                NclPpuConversionCode.InvalidRequest,
                request.CheatIndex,
                selectionError);
        }

        NclDoc document;
        try
        {
            document = NclParser.Read(
                request.NclPath,
                request.Dialect,
                request.OptionSelection);
        }
        catch (Exception ex) when (
            ex is IOException or
            UnauthorizedAccessException or
            InvalidDataException or
            ArgumentException)
        {
            return Failure(
                NclPpuConversionCode.ParseRejected,
                request.CheatIndex,
                ex.Message);
        }

        var evidence =
            NclPpuAddressDomainEvidenceCollector.CollectFile(
                request.RpcS3LogPath,
                request.Target.Serial,
                request.Target.RunKey);

        return ConvertParsed(
            document,
            request.CheatIndex,
            request.Target,
            evidence);
    }

    public static NclPpuConversionResult ConvertTextFile(
        NclPpuConversionTextFileRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrEmpty(request.NclText) ||
            string.IsNullOrWhiteSpace(request.RpcS3LogPath) ||
            request.Target is null)
        {
            return Failure(
                NclPpuConversionCode.InvalidRequest,
                request.CheatIndex,
                "NCL text, RPCS3 log path, and conversion target are required.");
        }

        if (!ValidateTarget(request.Target, out var targetError))
        {
            return Failure(
                NclPpuConversionCode.InvalidRequest,
                request.CheatIndex,
                targetError);
        }

        if (!ValidateOptionSelection(
                request.OptionSelection,
                request.CheatIndex,
                out var selectionError))
        {
            return Failure(
                NclPpuConversionCode.InvalidRequest,
                request.CheatIndex,
                selectionError);
        }

        NclDoc document;
        try
        {
            document = NclParser.ReadText(
                request.NclText,
                request.SourcePath,
                request.Dialect,
                request.OptionSelection);
        }
        catch (Exception ex) when (
            ex is InvalidDataException or
            ArgumentException)
        {
            return Failure(
                NclPpuConversionCode.ParseRejected,
                request.CheatIndex,
                ex.Message);
        }

        var evidence =
            NclPpuAddressDomainEvidenceCollector.CollectFile(
                request.RpcS3LogPath,
                request.Target.Serial,
                request.Target.RunKey);

        return ConvertParsed(
            document,
            request.CheatIndex,
            request.Target,
            evidence);
    }

    public static NclPpuConversionResult ConvertText(
        NclPpuConversionTextRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrEmpty(request.NclText) ||
            string.IsNullOrEmpty(request.RpcS3LogText) ||
            request.Target is null)
        {
            return Failure(
                NclPpuConversionCode.InvalidRequest,
                request.CheatIndex,
                "NCL text, RPCS3 log text, and conversion target are required.");
        }

        if (!ValidateTarget(request.Target, out var targetError))
        {
            return Failure(
                NclPpuConversionCode.InvalidRequest,
                request.CheatIndex,
                targetError);
        }

        if (!ValidateOptionSelection(
                request.OptionSelection,
                request.CheatIndex,
                out var selectionError))
        {
            return Failure(
                NclPpuConversionCode.InvalidRequest,
                request.CheatIndex,
                selectionError);
        }

        NclDoc document;
        try
        {
            document = NclParser.ReadText(
                request.NclText,
                request.SourcePath,
                request.Dialect,
                request.OptionSelection);
        }
        catch (Exception ex) when (
            ex is InvalidDataException or
            ArgumentException)
        {
            return Failure(
                NclPpuConversionCode.ParseRejected,
                request.CheatIndex,
                ex.Message);
        }

        var evidence =
            NclPpuAddressDomainEvidenceCollector.CollectText(
                request.RpcS3LogText,
                request.Target.Serial,
                request.Target.RunKey,
                request.LogSourcePath);

        return ConvertParsed(
            document,
            request.CheatIndex,
            request.Target,
            evidence);
    }

    // Which of several candidate PPU executables the selected cheat/option
    // instance can actually be emitted against. The source is parsed, verified
    // and lowered exactly once, and then every candidate is measured by the
    // same NclPpuAddressDomainEvidenceCollector and NclAddressBinder the
    // conversion itself uses: a candidate is eligible only when every concrete
    // write of the resolved instance fits inside one of that executable's
    // proven file-backed address domains. Nothing matches by title, by serial
    // alone, or by list order, and no second PPU matcher exists.
    public static NclRunKeyEligibilityResult DescribeEligibleRunKeys(
        NclPpuConversionTextFileRequest request,
        IReadOnlyList<string> candidateRunKeys)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(candidateRunKeys);

        if (string.IsNullOrEmpty(request.NclText) ||
            string.IsNullOrWhiteSpace(request.RpcS3LogPath) ||
            request.Target is null)
        {
            return EligibilityFailure(
                NclRunKeyEligibilityCode.InvalidRequest,
                "NCL text, RPCS3 log path, and conversion target are required.");
        }

        if (!ValidateEligibilityTarget(request.Target, out var targetError))
        {
            return EligibilityFailure(
                NclRunKeyEligibilityCode.InvalidRequest,
                targetError);
        }

        if (!ValidateOptionSelection(
                request.OptionSelection,
                request.CheatIndex,
                out var selectionError))
        {
            return EligibilityFailure(
                NclRunKeyEligibilityCode.InvalidRequest,
                selectionError);
        }

        NclDoc document;
        try
        {
            document = NclParser.ReadText(
                request.NclText,
                request.SourcePath,
                request.Dialect,
                request.OptionSelection);
        }
        catch (Exception ex) when (
            ex is InvalidDataException or
            ArgumentException)
        {
            return EligibilityFailure(
                NclRunKeyEligibilityCode.ParseRejected,
                ex.Message);
        }

        return DescribeParsedRunKeys(
            document,
            request.CheatIndex,
            candidateRunKeys,
            runKey =>
                NclPpuAddressDomainEvidenceCollector.CollectFile(
                    request.RpcS3LogPath,
                    request.Target.Serial,
                    runKey));
    }

    private static NclRunKeyEligibilityResult DescribeParsedRunKeys(
        NclDoc document,
        int cheatIndex,
        IReadOnlyList<string> candidateRunKeys,
        Func<string, NclPpuDomainEvidenceResult> collectEvidence)
    {
        if (candidateRunKeys.Count == 0)
        {
            return new NclRunKeyEligibilityResult(
                true,
                NclRunKeyEligibilityCode.Success,
                [],
                null,
                null);
        }

        if (candidateRunKeys.Count > MaxRunKeyCandidates)
        {
            return EligibilityFailure(
                NclRunKeyEligibilityCode.CandidateLimit,
                $"PPU eligibility accepts at most {MaxRunKeyCandidates} candidate run keys.");
        }

        if (cheatIndex < 0 ||
            cheatIndex >= document.Cheats.Count)
        {
            return EligibilityFailure(
                NclRunKeyEligibilityCode.CheatSelectionOutOfRange,
                "Selected NCL cheat index is outside the parsed document.");
        }

        var verification = NclSemanticVerifier.Verify(document);
        if (verification.Items.Count != document.Cheats.Count)
        {
            return EligibilityFailure(
                NclRunKeyEligibilityCode.VerificationMismatch,
                "Semantic verification did not preserve one result per parsed NCL cheat.");
        }

        var cheat = document.Cheats[cheatIndex];
        var lowering = NclStaticLowerer.Lower(cheat);
        if (!lowering.IsOk ||
            lowering.Code != NclStaticLowerCode.Success)
        {
            return new NclRunKeyEligibilityResult(
                false,
                NclRunKeyEligibilityCode.LoweringRejected,
                [],
                lowering.Code,
                lowering.Error ??
                "The selected NCL cheat is not a verified offline-static candidate.");
        }

        var candidates = new List<NclRunKeyCandidate>(candidateRunKeys.Count);
        foreach (var runKey in candidateRunKeys)
        {
            var evidence = collectEvidence(runKey);
            if (!evidence.IsOk ||
                evidence.Code != NclPpuDomainEvidenceCode.Success)
            {
                candidates.Add(
                    new NclRunKeyCandidate(
                        runKey,
                        false,
                        evidence.Code,
                        null,
                        evidence.Error));
                continue;
            }

            var binding = NclAddressBinder.Bind(
                lowering,
                evidence.Domains);

            candidates.Add(
                new NclRunKeyCandidate(
                    runKey,
                    binding.IsOk &&
                    binding.Code == NclAddressBindCode.Success,
                    evidence.Code,
                    binding.Code,
                    binding.Error));
        }

        return new NclRunKeyEligibilityResult(
            true,
            NclRunKeyEligibilityCode.Success,
            candidates,
            lowering.Code,
            null);
    }

    private static NclPpuConversionResult ConvertParsed(
        NclDoc document,
        int cheatIndex,
        NclPpuConversionTarget target,
        NclPpuDomainEvidenceResult evidence)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (cheatIndex < 0 ||
            cheatIndex >= document.Cheats.Count)
        {
            return Failure(
                NclPpuConversionCode.CheatSelectionOutOfRange,
                cheatIndex,
                "Selected NCL cheat index is outside the parsed document.");
        }

        var verification =
            NclSemanticVerifier.Verify(document);

        if (verification.Items.Count != document.Cheats.Count)
        {
            return Failure(
                NclPpuConversionCode.VerificationMismatch,
                cheatIndex,
                "Semantic verification did not preserve one result per parsed NCL cheat.");
        }

        var cheat = document.Cheats[cheatIndex];
        var lowering = NclStaticLowerer.Lower(cheat);

        if (!lowering.IsOk ||
            lowering.Code != NclStaticLowerCode.Success)
        {
            return Failure(
                NclPpuConversionCode.LoweringRejected,
                cheatIndex,
                lowering.Error ??
                "The selected NCL cheat is not a verified offline-static candidate.",
                cheat.Name,
                loweringCode: lowering.Code);
        }

        if (!evidence.IsOk ||
            evidence.Code != NclPpuDomainEvidenceCode.Success)
        {
            return Failure(
                NclPpuConversionCode.EvidenceRejected,
                cheatIndex,
                evidence.Error ??
                "Exact PPU address-domain evidence was not established.",
                cheat.Name,
                lowering.Code,
                evidence.Code);
        }

        var binding =
            NclAddressBinder.Bind(
                lowering,
                evidence.Domains);

        if (!binding.IsOk ||
            binding.Code != NclAddressBindCode.Success)
        {
            return Failure(
                NclPpuConversionCode.BindingRejected,
                cheatIndex,
                binding.Error ??
                "The selected static NCL writes could not be bound to the proven PPU domains.",
                cheat.Name,
                lowering.Code,
                evidence.Code,
                binding.Code);
        }

        var emission =
            NclRpcS3Emitter.Emit(
                new NclRpcS3EmitRequest(
                    binding,
                    target.PatchTitle,
                    target.GameTitle,
                    target.Serial,
                    target.Version,
                    cheat.Author,
                    target.Notes));

        if (!emission.IsOk ||
            emission.Code != NclRpcS3EmitCode.Success)
        {
            return Failure(
                NclPpuConversionCode.EmissionRejected,
                cheatIndex,
                emission.Error ??
                "RPCS3 Patch 1.2 emission rejected the verified binding.",
                cheat.Name,
                lowering.Code,
                evidence.Code,
                binding.Code,
                emission.Code);
        }

        return new NclPpuConversionResult(
            true,
            NclPpuConversionCode.Success,
            emission.PatchText,
            emission.RunKey,
            cheatIndex,
            cheat.Name,
            lowering.Code,
            evidence.Code,
            binding.Code,
            emission.Code,
            emission.InstructionCount,
            emission.VerifiedByteCount,
            null);
    }

    // The resolved cheat and the converted cheat must be the same one, so a
    // Preview and its Apply revalidation can never resolve different sources.
    private static bool ValidateOptionSelection(
        NclOptionSelection? selection,
        int cheatIndex,
        out string error)
    {
        if (selection is not null &&
            selection.CheatIndex != cheatIndex)
        {
            error =
                "The supplied NCL option selection does not target the selected cheat.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    // Eligibility deliberately does not require Target.RunKey: the candidate
    // keys are what is being measured, so a stale or absent selection can never
    // pre-decide the answer.
    private static bool ValidateEligibilityTarget(
        NclPpuConversionTarget target,
        out string error)
    {
        if (string.IsNullOrWhiteSpace(target.PatchTitle) ||
            string.IsNullOrWhiteSpace(target.GameTitle) ||
            string.IsNullOrWhiteSpace(target.Serial) ||
            string.IsNullOrWhiteSpace(target.Version))
        {
            error =
                "Patch title, game title, serial, and version are required.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static NclRunKeyEligibilityResult EligibilityFailure(
        NclRunKeyEligibilityCode code,
        string error)
    {
        return new NclRunKeyEligibilityResult(
            false,
            code,
            [],
            null,
            error);
    }

    private static bool ValidateTarget(
        NclPpuConversionTarget target,
        out string error)
    {
        if (string.IsNullOrWhiteSpace(target.RunKey) ||
            string.IsNullOrWhiteSpace(target.PatchTitle) ||
            string.IsNullOrWhiteSpace(target.GameTitle) ||
            string.IsNullOrWhiteSpace(target.Serial) ||
            string.IsNullOrWhiteSpace(target.Version))
        {
            error =
                "Run key, patch title, game title, serial, and version are required.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static NclPpuConversionResult Failure(
        NclPpuConversionCode code,
        int cheatIndex,
        string error,
        string? cheatName = null,
        NclStaticLowerCode? loweringCode = null,
        NclPpuDomainEvidenceCode? evidenceCode = null,
        NclAddressBindCode? bindingCode = null,
        NclRpcS3EmitCode? emissionCode = null)
    {
        return new NclPpuConversionResult(
            false,
            code,
            string.Empty,
            null,
            cheatIndex,
            cheatName,
            loweringCode,
            evidenceCode,
            bindingCode,
            emissionCode,
            0,
            0,
            error);
    }
}
