using System.IO;
using System.Security.Cryptography;
using R3Config.Mdl;
using R3Config.Svc;

namespace R3Config.Application;

public enum NclManagedPatchPreviewCode
{
    Success,
    ConversionRejected,
    GeneratedPatchRejected,
    PlanRejected,
    ManagedPreparationRejected
}

public enum NclManagedPatchInspectionCode
{
    Success,
    InvalidInput,
    ParseRejected,
    VerificationRejected,
    SourceSerialMismatch,
    SourceVersionMismatch,
    NoStaticCandidates
}

// One declared choice of an Artemis option placeholder. Label is what the user
// reads; Value is the canonical raw source value that is substituted.
public sealed record NclManagedPatchOptionChoice(
    string Value,
    string Label);

public sealed record NclManagedPatchOptionSet(
    int PlaceholderId,
    IReadOnlyList<NclManagedPatchOptionChoice> Choices);

public sealed record NclManagedPatchCheatChoice(
    int Index,
    string Name,
    string? Author,
    string? Notes,
    NclCorpusCheatCandidate? BundledCandidate = null,
    IReadOnlyList<NclManagedPatchOptionSet>? OptionSets = null,
    int DuplicateOrdinal = 0)
{
    // Index is the internal parsed-cheat index and is never shown: leaking it
    // beside the name reads as a version or a code, and under RTL it lands on
    // the wrong side of the title. A cheat is listed under its own name, and
    // only a name that genuinely occurs more than once in the same source is
    // disambiguated - by its 1-based occurrence, not by the internal index.
    public string Display =>
        DuplicateOrdinal > 0
            ? $"{Name} ({DuplicateOrdinal})"
            : Name;

    // Only placeholder ids the executable code actually references. A declared
    // but unreferenced option set produces no selector.
    public IReadOnlyList<NclManagedPatchOptionSet> RequiredOptions =>
        OptionSets ?? [];

    public bool RequiresOptionSelection => RequiredOptions.Count > 0;

    public NclVerifyStat Status =>
        BundledCandidate?.Status ??
        (RequiresOptionSelection
            ? NclVerifyStat.OptionSelectionRequired
            : NclVerifyStat.OfflineStaticCandidate);

    public bool CanConvert => BundledCandidate?.CanConvert ?? true;

    // False only for a same-serial, different-version discovery candidate.
    // Apply stays blocked at the use-case layer regardless of this value;
    // it exists so presentation can label the candidate honestly.
    public bool VersionProven => BundledCandidate?.VersionProven ?? true;

    // The application version this candidate's source actually declares.
    // Equal to the requested version for every proven candidate; only
    // differs for a same-serial, different-version discovery candidate.
    public string? SourceVersion => BundledCandidate?.Version;

    // Presentation-only inverse of VersionProven, for a plain Visibility
    // binding on the version-mismatch block of the item template.
    public bool IsVersionMismatch => !VersionProven;
}

public sealed record NclManagedPatchInspection(
    bool IsOk,
    NclManagedPatchInspectionCode Code,
    NclSourceIdentity? SourceIdentity,
    IReadOnlyList<NclManagedPatchCheatChoice> StaticCheats,
    int TotalCheats,
    bool SourceHasExactTarget,
    string? Error,
    string? SourceSha256 = null);

public enum NclManagedPatchSourceKind
{
    ExternalFile,
    BundledCorpus
}

public sealed record NclManagedPatchApplyEvidence(
    NclManagedPatchSourceKind SourceKind,
    string RpcS3LogPath,
    NclPpuConversionTarget Target,
    NclDialect Dialect,
    int CheatIndex,
    string? ExternalPath = null,
    string? ExternalSha256 = null,
    NclCorpusCheatCandidate? BundledCandidate = null,
    NclOptionSelection? OptionSelection = null);

public sealed record NclManagedPatchPreview(
    bool IsOk,
    NclManagedPatchPreviewCode Code,
    NclPpuConversionResult? Conversion,
    PatMatch? Match,
    PatchApplyPlan? Plan,
    LibStRes? ManagedPreparation,
    string? Error,
    NclManagedPatchApplyEvidence? ApplyEvidence = null);

public sealed record NclBundledManagedPatchRequest(
    NclCorpusCheatCandidate Candidate,
    string RpcS3LogPath,
    NclPpuConversionTarget Target,
    NclOptionSelection? OptionSelection = null);

public sealed class NclManagedPatchUseCase(
    BundledCheatPatchService bundledCheats,
    PatchMutationUseCase patchMutation,
    string? bundledCorpusPath = null,
    string? discoveryCorpusSha256 = null)
{
    public const string BundledCorpusName = "ncl.zip";
    public const string BundledCorpusSha256 =
        "C4C70C6E73A225F1E15096A3AEA06DD05461F7EF0CBB4BE8B106577ABC22D8B8";
    public const int BundledCorpusFileCount = 2910;

    private readonly ReadSvc _reader = new();
    private readonly string? _bundledCorpusPath = bundledCorpusPath;
    // An explicitly pinned experimental corpus can exercise read-only discovery
    // before shipment. Conversion and apply rereads retain the production pin.
    private readonly string _discoveryCorpusSha256 =
        discoveryCorpusSha256 ?? BundledCorpusSha256;

    public NclCorpusLookupResult DiscoverBundled(
        string serial,
        string version,
        string? title = null)
    {
        if (string.IsNullOrWhiteSpace(_bundledCorpusPath))
        {
            return new NclCorpusLookupResult(
                false,
                NclCorpusLookupCode.SourceMissing,
                null,
                [],
                [],
                [],
                0,
                0,
                "Bundled NCL corpus services are not configured.");
        }

        return NclCorpusReader.LookupZip(
            _bundledCorpusPath,
            serial,
            version,
            _discoveryCorpusSha256,
            NclDialect.ArtemisPs3,
            title);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Performance",
        "CA1822:Mark members as static",
        Justification =
            "Inspection is intentionally exposed through the composed NCL use-case instance so AppFlow and the UI retain one integration boundary.")]
    public NclManagedPatchInspection Inspect(
        string nclPath,
        NclDialect dialect,
        string serial,
        string version)
    {
        if (string.IsNullOrWhiteSpace(nclPath) ||
            string.IsNullOrWhiteSpace(serial) ||
            string.IsNullOrWhiteSpace(version))
        {
            return InspectionFailure(
                NclManagedPatchInspectionCode.InvalidInput,
                "NCL path, serial, and version are required.");
        }

        NclSourceIdentity identity;
        NclDoc document;
        string sourceSha256;

        try
        {
            identity = NclParser.ParseIdentity(nclPath);
            var source = ReadSource(nclPath);
            sourceSha256 = source.Sha256;
            document = NclParser.ReadText(source.Text, nclPath, dialect);
        }
        catch (Exception ex) when (
            ex is IOException or
            UnauthorizedAccessException or
            InvalidDataException or
            ArgumentException)
        {
            return InspectionFailure(
                NclManagedPatchInspectionCode.ParseRejected,
                ex.Message);
        }

        if (!ValidateDeclaredTarget(
                identity,
                serial,
                version,
                out var mismatchCode,
                out var mismatchError))
        {
            return InspectionFailure(
                mismatchCode,
                mismatchError,
                identity,
                document.Cheats.Count);
        }

        var verification = NclSemanticVerifier.Verify(document);
        if (verification.Items.Count != document.Cheats.Count)
        {
            return InspectionFailure(
                NclManagedPatchInspectionCode.VerificationRejected,
                "Semantic verification did not preserve one result per parsed NCL cheat.",
                identity,
                document.Cheats.Count);
        }

        var choices = new List<NclManagedPatchCheatChoice>();

        for (var index = 0; index < document.Cheats.Count; index++)
        {
            var status = verification.Items[index].Status;

            // An option cheat is offered so the user can make the explicit
            // choice its source demands. Its verification status stays
            // OptionSelectionRequired: only the resolved instance is ever
            // lowered, bound and emitted.
            if (status == NclVerifyStat.OptionSelectionRequired)
            {
                var optionCheat = document.Cheats[index];
                var optionSets = DescribeRequiredOptions(optionCheat);
                if (optionSets.Count == 0)
                {
                    continue;
                }

                choices.Add(
                    new NclManagedPatchCheatChoice(
                        index,
                        optionCheat.Name,
                        optionCheat.Author,
                        optionCheat.Notes.Count == 0
                            ? null
                            : string.Join(
                                Environment.NewLine,
                                optionCheat.Notes),
                        null,
                        optionSets));
                continue;
            }

            if (status != NclVerifyStat.OfflineStaticCandidate)
            {
                continue;
            }

            var cheat = document.Cheats[index];
            var lowering = NclStaticLowerer.Lower(cheat);
            if (!lowering.IsOk ||
                lowering.Code != NclStaticLowerCode.Success)
            {
                continue;
            }

            var notes = cheat.Notes.Count == 0
                ? null
                : string.Join(Environment.NewLine, cheat.Notes);

            choices.Add(
                new NclManagedPatchCheatChoice(
                    index,
                    cheat.Name,
                    cheat.Author,
                    notes));
        }

        if (choices.Count == 0)
        {
            return InspectionFailure(
                NclManagedPatchInspectionCode.NoStaticCandidates,
                "The selected NCL file contains no cheat that passes the verified offline-static lowering path.",
                identity,
                document.Cheats.Count);
        }

        return new NclManagedPatchInspection(
            true,
            NclManagedPatchInspectionCode.Success,
            identity,
            choices.ToArray(),
            document.Cheats.Count,
            identity.DeclaresSerial(serial) &&
            identity.VersionsForSerial(serial).Any(candidate =>
                PatVer.Equals(candidate, version)),
            null,
            sourceSha256);
    }

    public NclManagedPatchPreview Prepare(
        NclPpuConversionFileRequest request,
        Rpcs3InstallDescriptor install,
        IReadOnlyList<PatMatch>? enabledMatches = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(install);

        if (request.Target is null)
        {
            return Failure(
                NclManagedPatchPreviewCode.ConversionRejected,
                "An exact NCL conversion target is required.");
        }

        if (!string.IsNullOrWhiteSpace(request.NclPath))
        {
            var identity = NclParser.ParseIdentity(request.NclPath);
            if (!ValidateDeclaredTarget(
                    identity,
                    request.Target.Serial,
                    request.Target.Version,
                    out _,
                    out var declaredTargetError))
            {
                return Failure(
                    NclManagedPatchPreviewCode.ConversionRejected,
                    declaredTargetError);
            }
        }

        NclPpuConversionResult conversion;
        NclManagedPatchApplyEvidence applyEvidence;
        try
        {
            var sourcePath = Path.GetFullPath(request.NclPath);
            var source = ReadSource(sourcePath);
            if (request.ExpectedSourceSha256 is not null &&
                !source.Sha256.Equals(
                    request.ExpectedSourceSha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                return Failure(
                    NclManagedPatchPreviewCode.ConversionRejected,
                    "The external NCL source changed after inspection. Inspect it again before preview.");
            }

            // Convert the exact snapshot just validated, never reopen the path.
            conversion = NclPpuConversionService.ConvertTextFile(
                new NclPpuConversionTextFileRequest(
                    source.Text,
                    request.RpcS3LogPath,
                    request.Dialect,
                    request.CheatIndex,
                    request.Target,
                    sourcePath,
                    request.OptionSelection));
            applyEvidence = new NclManagedPatchApplyEvidence(
                NclManagedPatchSourceKind.ExternalFile,
                request.RpcS3LogPath,
                request.Target,
                request.Dialect,
                request.CheatIndex,
                ExternalPath: sourcePath,
                ExternalSha256: source.Sha256,
                OptionSelection: request.OptionSelection);
        }
        catch (Exception ex) when (
            ex is IOException or
            UnauthorizedAccessException or
            InvalidDataException or
            ArgumentException or
            NotSupportedException)
        {
            return Failure(
                NclManagedPatchPreviewCode.ConversionRejected,
                ex.Message);
        }

        return PrepareConverted(
            conversion,
            request.Target,
            request.RpcS3LogPath,
            install,
            enabledMatches,
            applyEvidence);
    }

    // Which PPU executables the selected bundled cheat/option instance can
    // actually be emitted against. This revalidates the pinned archive exactly
    // as Preview does and then delegates to the single existing address-domain
    // evidence contract; it never mutates anything and never selects a key.
    public NclRunKeyEligibilityResult DescribeBundledRunKeys(
        NclBundledManagedPatchRequest request,
        IReadOnlyList<string> candidateRunKeys)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(candidateRunKeys);

        if (string.IsNullOrWhiteSpace(_bundledCorpusPath))
        {
            return EligibilityFailure(
                "Bundled NCL corpus services are not configured.");
        }

        if (!request.Candidate.CanConvert)
        {
            return EligibilityFailure(
                "The selected bundled NCL cheat is discoverable but is not eligible for verified offline conversion.");
        }

        if (request.Target is null ||
            request.Candidate.Dialect != NclDialect.ArtemisPs3 ||
            !request.Target.PatchTitle.Equals(
                request.Candidate.Name,
                StringComparison.Ordinal))
        {
            return EligibilityFailure(
                "The bundled NCL candidate changed its pinned dialect or cheat identity.");
        }

        var primary = request.Candidate.PrimarySource;
        if (request.OptionSelection is not null &&
            request.OptionSelection.CheatIndex != primary.CheatIndex)
        {
            return EligibilityFailure(
                "The supplied NCL option selection does not target the selected bundled cheat.");
        }

        if (request.Candidate.RequiresOptionSelection &&
            request.OptionSelection is null)
        {
            return EligibilityFailure(
                "The selected bundled NCL cheat requires an explicit option selection.");
        }

        var read = NclCorpusReader.ReadZipCandidate(
            _bundledCorpusPath,
            BundledCorpusSha256,
            request.Candidate,
            request.Target.Serial,
            request.Target.Version);

        if (!read.IsOk ||
            read.Code != NclCorpusCandidateReadCode.Success)
        {
            return EligibilityFailure(
                read.Error ??
                "The selected bundled NCL candidate changed after discovery.");
        }

        return NclPpuConversionService.DescribeEligibleRunKeys(
            new NclPpuConversionTextFileRequest(
                read.Text,
                request.RpcS3LogPath,
                NclDialect.ArtemisPs3,
                primary.CheatIndex,
                request.Target,
                string.Concat(
                    BundledCorpusName,
                    "/",
                    primary.RelativePath),
                request.OptionSelection),
            candidateRunKeys);
    }

    // The external equivalent. The source snapshot is read and its declared
    // identity revalidated exactly as Preview does, so eligibility can never be
    // computed from a file that Preview itself would reject.
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Performance",
        "CA1822:Mark members as static",
        Justification =
            "Run-key eligibility is intentionally exposed through the composed NCL use-case instance so AppFlow and the UI retain one integration boundary.")]
    public NclRunKeyEligibilityResult DescribeRunKeys(
        NclPpuConversionFileRequest request,
        IReadOnlyList<string> candidateRunKeys)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(candidateRunKeys);

        if (request.Target is null ||
            string.IsNullOrWhiteSpace(request.NclPath))
        {
            return EligibilityFailure(
                "An exact NCL conversion target and source path are required.");
        }

        try
        {
            var identity = NclParser.ParseIdentity(request.NclPath);
            if (!ValidateDeclaredTarget(
                    identity,
                    request.Target.Serial,
                    request.Target.Version,
                    out _,
                    out var declaredTargetError))
            {
                return EligibilityFailure(declaredTargetError);
            }

            var sourcePath = Path.GetFullPath(request.NclPath);
            var source = ReadSource(sourcePath);
            if (request.ExpectedSourceSha256 is not null &&
                !source.Sha256.Equals(
                    request.ExpectedSourceSha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                return EligibilityFailure(
                    "The external NCL source changed after inspection. Inspect it again before preview.");
            }

            return NclPpuConversionService.DescribeEligibleRunKeys(
                new NclPpuConversionTextFileRequest(
                    source.Text,
                    request.RpcS3LogPath,
                    request.Dialect,
                    request.CheatIndex,
                    request.Target,
                    sourcePath,
                    request.OptionSelection),
                candidateRunKeys);
        }
        catch (Exception ex) when (
            ex is IOException or
            UnauthorizedAccessException or
            InvalidDataException or
            ArgumentException or
            NotSupportedException)
        {
            return EligibilityFailure(ex.Message);
        }
    }

    private static NclRunKeyEligibilityResult EligibilityFailure(
        string error)
    {
        return new NclRunKeyEligibilityResult(
            false,
            NclRunKeyEligibilityCode.InvalidRequest,
            [],
            null,
            error);
    }

    public NclManagedPatchPreview PrepareBundled(
        NclBundledManagedPatchRequest request,
        Rpcs3InstallDescriptor install,
        IReadOnlyList<PatMatch>? enabledMatches = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(install);

        if (string.IsNullOrWhiteSpace(_bundledCorpusPath))
        {
            return Failure(
                NclManagedPatchPreviewCode.ConversionRejected,
                "Bundled NCL corpus services are not configured.");
        }

        if (!request.Candidate.CanConvert)
        {
            return Failure(
                NclManagedPatchPreviewCode.ConversionRejected,
                "The selected bundled NCL cheat is discoverable but is not eligible for verified offline conversion.");
        }

        if (request.Candidate.Dialect != NclDialect.ArtemisPs3 ||
            !request.Target.PatchTitle.Equals(
                request.Candidate.Name,
                StringComparison.Ordinal))
        {
            return Failure(
                NclManagedPatchPreviewCode.ConversionRejected,
                "The bundled NCL candidate changed its pinned dialect or cheat identity.");
        }

        var primaryIndex = request.Candidate.PrimarySource.CheatIndex;
        if (request.OptionSelection is not null &&
            request.OptionSelection.CheatIndex != primaryIndex)
        {
            return Failure(
                NclManagedPatchPreviewCode.ConversionRejected,
                "The supplied NCL option selection does not target the selected bundled cheat.");
        }

        if (request.Candidate.RequiresOptionSelection &&
            request.OptionSelection is null)
        {
            return Failure(
                NclManagedPatchPreviewCode.ConversionRejected,
                "The selected bundled NCL cheat requires an explicit option selection.");
        }

        var read = NclCorpusReader.ReadZipCandidate(
            _bundledCorpusPath,
            BundledCorpusSha256,
            request.Candidate,
            request.Target.Serial,
            request.Target.Version);

        if (!read.IsOk ||
            read.Code != NclCorpusCandidateReadCode.Success)
        {
            return Failure(
                NclManagedPatchPreviewCode.ConversionRejected,
                read.Error ??
                "The selected bundled NCL candidate changed after discovery.");
        }

        var primary = request.Candidate.PrimarySource;
        var verifiedCheat = read.Cheat!;
        var verifiedNotes = verifiedCheat.Notes.Count == 0
            ? null
            : string.Join(
                Environment.NewLine,
                verifiedCheat.Notes);
        var verifiedTarget = request.Target with
        {
            // The candidate's listed name, which is the source name unless two
            // proven-different definitions in this target share it. Taking the
            // raw source name here would put both variants under one patch
            // title and the second would be rejected as a duplicate identity.
            PatchTitle = request.Candidate.Name,
            Author = verifiedCheat.Author,
            Notes = verifiedNotes
        };
        var conversion =
            NclPpuConversionService.ConvertTextFile(
                new NclPpuConversionTextFileRequest(
                    read.Text,
                    request.RpcS3LogPath,
                    NclDialect.ArtemisPs3,
                    primary.CheatIndex,
                    verifiedTarget,
                    string.Concat(
                        BundledCorpusName,
                        "/",
                        primary.RelativePath),
                    request.OptionSelection));

        return PrepareConverted(
            conversion,
            verifiedTarget,
            request.RpcS3LogPath,
            install,
            enabledMatches,
            new NclManagedPatchApplyEvidence(
                NclManagedPatchSourceKind.BundledCorpus,
                request.RpcS3LogPath,
                verifiedTarget,
                NclDialect.ArtemisPs3,
                primary.CheatIndex,
                BundledCandidate: request.Candidate,
                OptionSelection: request.OptionSelection));
    }

    private NclManagedPatchPreview PrepareConverted(
        NclPpuConversionResult conversion,
        NclPpuConversionTarget target,
        string hashSource,
        Rpcs3InstallDescriptor install,
        IReadOnlyList<PatMatch>? enabledMatches,
        NclManagedPatchApplyEvidence applyEvidence)
    {
        if (!conversion.IsOk ||
            conversion.Code != NclPpuConversionCode.Success)
        {
            return Failure(
                NclManagedPatchPreviewCode.ConversionRejected,
                conversion.Error ??
                "NCL PPU conversion was rejected.",
                conversion);
        }

        var read = _reader.ReadText(
            conversion.PatchText,
            PatSrc.ManagedArtemis,
            sourcePath: null);

        if (!read.IsOk ||
            read.Catalog.Entries.Count != 1)
        {
            return Failure(
                NclManagedPatchPreviewCode.GeneratedPatchRejected,
                read.Error ??
                "The converted NCL output did not round-trip as exactly one managed RPCS3 definition.",
                conversion);
        }

        if (!TryBuildExactMatch(
                read.Catalog.Entries[0],
                target,
                hashSource,
                conversion,
                out var match,
                out var matchError))
        {
            return Failure(
                NclManagedPatchPreviewCode.GeneratedPatchRejected,
                matchError,
                conversion);
        }

        PatchApplyPlan plan;
        try
        {
            plan = patchMutation.BuildOn(
                install,
                [match],
                enabledMatches ?? [],
                expertMode: false);
        }
        catch (Exception ex) when (
            ex is IOException or
            InvalidDataException or
            InvalidOperationException or
            ArgumentException)
        {
            return Failure(
                NclManagedPatchPreviewCode.PlanRejected,
                ex.Message,
                conversion,
                match);
        }

        if (plan.Changes.Count != 1 ||
            !PlanMatches(
                plan.Changes[0],
                target,
                conversion.RunKey))
        {
            return Failure(
                NclManagedPatchPreviewCode.PlanRejected,
                "The generated NCL activation plan did not preserve the exact converted RPCS3 identity.",
                conversion,
                match,
                plan);
        }

        var prepared = bundledCheats.PrepGenerated(
            install.PatPath,
            conversion.PatchText);

        if (!prepared.IsOk)
        {
            return Failure(
                NclManagedPatchPreviewCode.ManagedPreparationRejected,
                prepared.Error ??
                prepared.Message,
                conversion,
                match,
                plan,
                prepared);
        }

        return new NclManagedPatchPreview(
            true,
            NclManagedPatchPreviewCode.Success,
            conversion,
            match,
            plan,
            prepared,
            null,
            applyEvidence);
    }

    public PatRes Apply(
        NclManagedPatchPreview preview,
        Rpcs3InstallDescriptor install)
    {
        ArgumentNullException.ThrowIfNull(preview);
        ArgumentNullException.ThrowIfNull(install);

        if (!preview.IsOk ||
            preview.Code != NclManagedPatchPreviewCode.Success ||
            preview.Conversion is null ||
            preview.Match is null ||
            preview.Plan is null ||
            preview.ManagedPreparation is null ||
            preview.ApplyEvidence is null)
        {
            throw new InvalidOperationException(
                preview.Error ??
                "The NCL managed patch preview is not applicable.");
        }

        RevalidateApplyEvidence(preview);

        return patchMutation.ApplyPreparedManaged(
            preview.Plan,
            install,
            preview.Match,
            preview.ManagedPreparation);
    }

    private void RevalidateApplyEvidence(
        NclManagedPatchPreview preview)
    {
        var conversion = preview.Conversion!;
        var match = preview.Match!;
        var plan = preview.Plan!;
        var evidence = preview.ApplyEvidence!;
        var target = evidence.Target;

        if (conversion.RunKey is null ||
            !conversion.RunKey.Equals(
                target.RunKey,
                StringComparison.OrdinalIgnoreCase) ||
            !match.Patch.RunKey.Equals(
                conversion.RunKey,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                match.MatSer,
                target.Serial,
                StringComparison.OrdinalIgnoreCase) ||
            !PatVer.Equals(
                match.MatVer,
                target.Version) ||
            !string.Equals(
                match.TgtName,
                target.GameTitle,
                StringComparison.Ordinal) ||
            plan.Changes.Count != 1 ||
            !PlanMatches(
                plan.Changes[0],
                target,
                conversion.RunKey))
        {
            throw new InvalidOperationException(
                "The NCL preview identity changed before Apply.");
        }

        NclPpuConversionResult current;
        switch (evidence.SourceKind)
        {
            case NclManagedPatchSourceKind.ExternalFile:
                current = RevalidateExternalEvidence(evidence);
                break;

            case NclManagedPatchSourceKind.BundledCorpus:
                current = RevalidateBundledEvidence(evidence);
                break;

            default:
                throw new InvalidOperationException(
                    "The NCL preview has an unsupported source-evidence kind.");
        }

        if (!current.IsOk ||
            current.Code != NclPpuConversionCode.Success)
        {
            throw new IOException(
                "The NCL source or PPU evidence changed after Preview. " +
                (current.Error ??
                 "Preview the selected cheat again before Apply."));
        }

        if (!EquivalentConversion(
                conversion,
                current))
        {
            throw new IOException(
                "The NCL source or PPU evidence changed after Preview. Preview the selected cheat again before Apply.");
        }
    }

    private static NclPpuConversionResult RevalidateExternalEvidence(
        NclManagedPatchApplyEvidence evidence)
    {
        if (string.IsNullOrWhiteSpace(evidence.ExternalPath) ||
            string.IsNullOrWhiteSpace(evidence.ExternalSha256))
        {
            throw new InvalidOperationException(
                "The external NCL preview has no source fingerprint.");
        }

        var source = ReadSource(evidence.ExternalPath);
        if (!source.Sha256.Equals(
                evidence.ExternalSha256,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new IOException(
                "The external NCL source changed after Preview. Preview it again before Apply.");
        }

        var identity = NclParser.ParseIdentity(
            evidence.ExternalPath);
        if (!ValidateDeclaredTarget(
                identity,
                evidence.Target.Serial,
                evidence.Target.Version,
                out _,
                out var targetError))
        {
            throw new IOException(targetError);
        }

        return NclPpuConversionService.ConvertTextFile(
            new NclPpuConversionTextFileRequest(
                source.Text,
                evidence.RpcS3LogPath,
                evidence.Dialect,
                evidence.CheatIndex,
                evidence.Target,
                evidence.ExternalPath,
                evidence.OptionSelection));
    }

    private NclPpuConversionResult RevalidateBundledEvidence(
        NclManagedPatchApplyEvidence evidence)
    {
        if (string.IsNullOrWhiteSpace(_bundledCorpusPath) ||
            evidence.BundledCandidate is null)
        {
            throw new InvalidOperationException(
                "The bundled NCL preview has no pinned corpus evidence.");
        }

        var candidate = evidence.BundledCandidate;
        if (candidate.Sources.Count == 0 ||
            candidate.Dialect != NclDialect.ArtemisPs3 ||
            evidence.Dialect != NclDialect.ArtemisPs3 ||
            evidence.CheatIndex != candidate.PrimarySource.CheatIndex ||
            !evidence.Target.PatchTitle.Equals(
                candidate.Name,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The bundled NCL preview identity changed before Apply.");
        }

        var read = NclCorpusReader.ReadZipCandidate(
            _bundledCorpusPath,
            BundledCorpusSha256,
            candidate,
            evidence.Target.Serial,
            evidence.Target.Version);

        if (!read.IsOk ||
            read.Code != NclCorpusCandidateReadCode.Success ||
            read.Cheat is null)
        {
            throw new IOException(
                read.Error ??
                "The bundled NCL source changed after Preview. Preview it again before Apply.");
        }

        var verifiedNotes = read.Cheat.Notes.Count == 0
            ? null
            : string.Join(
                Environment.NewLine,
                read.Cheat.Notes);
        var verifiedTarget = evidence.Target with
        {
            // Same reason as Preview: the listed name is the patch identity.
            PatchTitle = candidate.Name,
            Author = read.Cheat.Author,
            Notes = verifiedNotes
        };

        return NclPpuConversionService.ConvertTextFile(
            new NclPpuConversionTextFileRequest(
                read.Text,
                evidence.RpcS3LogPath,
                NclDialect.ArtemisPs3,
                candidate.PrimarySource.CheatIndex,
                verifiedTarget,
                string.Concat(
                    BundledCorpusName,
                    "/",
                    candidate.PrimarySource.RelativePath),
                evidence.OptionSelection));
    }

    private static bool EquivalentConversion(
        NclPpuConversionResult preview,
        NclPpuConversionResult current)
    {
        return current.IsOk == preview.IsOk &&
               current.Code == preview.Code &&
               string.Equals(
                   current.PatchText,
                   preview.PatchText,
                   StringComparison.Ordinal) &&
               string.Equals(
                   current.RunKey,
                   preview.RunKey,
                   StringComparison.OrdinalIgnoreCase) &&
               current.CheatIndex == preview.CheatIndex &&
               string.Equals(
                   current.CheatName,
                   preview.CheatName,
                   StringComparison.Ordinal) &&
               current.LoweringCode == preview.LoweringCode &&
               current.EvidenceCode == preview.EvidenceCode &&
               current.BindingCode == preview.BindingCode &&
               current.EmissionCode == preview.EmissionCode &&
               current.InstructionCount == preview.InstructionCount &&
               current.VerifiedByteCount == preview.VerifiedByteCount;
    }

    private static (string Text, string Sha256) ReadSource(string path)
    {
        const long maxBytes = 2L * 1024 * 1024;
        using var stream = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var bytes = R3Bound.ReadBytes(stream, maxBytes, "NCL file");
        using var snapshot = new MemoryStream(bytes, writable: false);
        return (
            R3Bound.ReadUtf2(snapshot, maxBytes, "NCL file"),
            Convert.ToHexString(SHA256.HashData(bytes)));
    }

    private static bool ValidateDeclaredTarget(
        NclSourceIdentity identity,
        string serial,
        string version,
        out NclManagedPatchInspectionCode code,
        out string error)
    {
        var normalizedVersion = PatVer.Norm(version);
        if (string.IsNullOrWhiteSpace(serial) ||
            normalizedVersion is null ||
            PatVer.IsAll(normalizedVersion))
        {
            code = NclManagedPatchInspectionCode.InvalidInput;
            error =
                "NCL conversion requires an exact selected title ID and application version.";
            return false;
        }

        if (identity.Serials.Count == 0)
        {
            code = NclManagedPatchInspectionCode.InvalidInput;
            error =
                "The external NCL filename does not declare an exact title ID.";
            return false;
        }

        if (!identity.DeclaresSerial(serial))
        {
            code = NclManagedPatchInspectionCode.SourceSerialMismatch;
            error =
                "The NCL filename declares a different title ID than the selected game.";
            return false;
        }

        var declaredVersions = identity.VersionsForSerial(serial);
        if (declaredVersions.Count == 0)
        {
            code = NclManagedPatchInspectionCode.InvalidInput;
            error =
                "The external NCL filename does not establish an exact application version for the selected game.";
            return false;
        }

        if (!declaredVersions.Any(candidate =>
                PatVer.Equals(candidate, normalizedVersion)))
        {
            code = NclManagedPatchInspectionCode.SourceVersionMismatch;
            error =
                "The NCL filename declares a different application version for the selected game.";
            return false;
        }

        code = NclManagedPatchInspectionCode.Success;
        error = string.Empty;
        return true;
    }

    private static bool TryBuildExactMatch(
        Rpcs3PatchEntry entry,
        NclPpuConversionTarget target,
        string hashSource,
        NclPpuConversionResult conversion,
        out PatMatch match,
        out string error)
    {
        match = null!;
        error = string.Empty;

        if (conversion.RunKey is null ||
            !entry.RunKey.Equals(
                conversion.RunKey,
                StringComparison.OrdinalIgnoreCase) ||
            !entry.RunKey.Equals(
                target.RunKey,
                StringComparison.OrdinalIgnoreCase) ||
            !entry.Title.Equals(
                target.PatchTitle,
                StringComparison.Ordinal))
        {
            error =
                "The generated managed definition changed its RPCS3 run key or patch title after conversion.";
            return false;
        }

        var targets = entry.GameTgt
            .Where(gameTarget =>
                gameTarget.Serial.Equals(
                    target.Serial,
                    StringComparison.OrdinalIgnoreCase) &&
                gameTarget.Title.Equals(
                    target.GameTitle,
                    StringComparison.Ordinal))
            .ToArray();

        if (targets.Length != 1 ||
            !targets[0].Versions.Any(version =>
                PatVer.Equals(
                    version,
                    target.Version)))
        {
            error =
                "The generated managed definition changed its exact game serial/version target after conversion.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(entry.DefPrint))
        {
            error =
                "The generated managed definition has no exact definition fingerprint.";
            return false;
        }

        var patch = entry with
        {
            Source = PatSrc.ManagedArtemis,
            Risk = PatRisk.Cheat,
            PatType = "Cheat"
        };

        match = new PatMatch(
            Patch: patch,
            Status: MatStat.Match,
            MatSer: target.Serial,
            MatVer: target.Version,
            HasMeta: true,
            Reason:
                "Exact NCL PPU conversion with RPCS3 runtime hash and file-backed PT_LOAD address evidence.",
            TgtName: target.GameTitle,
            TgtSer: target.Serial,
            TgtVer: target.Version,
            HashSrc: hashSource,
            HashEvi: HashEvi.PlainLog);

        if (!PatAct.Can(match))
        {
            error =
                "The generated NCL managed definition is not actionable under the existing RPCS3 patch compatibility policy.";
            match = null!;
            return false;
        }

        return true;
    }

    private static bool PlanMatches(
        PatChg change,
        NclPpuConversionTarget target,
        string? runKey)
    {
        return runKey is not null &&
               change.HashKey.Equals(
                   runKey,
                   StringComparison.OrdinalIgnoreCase) &&
               change.PatTitle.Equals(
                   target.PatchTitle,
                   StringComparison.Ordinal) &&
               change.GameName.Equals(
                   target.GameTitle,
                   StringComparison.Ordinal) &&
               change.Serial.Equals(
                   target.Serial,
                   StringComparison.OrdinalIgnoreCase) &&
               PatVer.Equals(
                   change.Version,
                   target.Version) &&
               change.Risk == PatRisk.Cheat;
    }

    // The parser records which placeholder ids the raw segment body references,
    // so requirement discovery reads that recorded contract instead of trying to
    // reconstruct the body from presentation notes. CA1859: the concrete List is
    // the real result type of this private helper.
    private static List<NclManagedPatchOptionSet> DescribeRequiredOptions(
        NclCheat cheat)
    {
        var required = cheat.RequiredOptionPlaceholderIds;
        if (required.Count == 0)
        {
            return [];
        }

        var declared = new Dictionary<int, NclOptionSet>();
        foreach (var set in cheat.Options)
        {
            if (!declared.TryAdd(set.PlaceholderId, set))
            {
                return [];
            }
        }

        var sets = new List<NclManagedPatchOptionSet>();
        foreach (var placeholderId in required)
        {
            if (!declared.TryGetValue(placeholderId, out var declaredSet) ||
                declaredSet.Values.Count == 0)
            {
                return [];
            }

            sets.Add(
                new NclManagedPatchOptionSet(
                    placeholderId,
                    [.. declaredSet.Values.Select(item =>
                        new NclManagedPatchOptionChoice(
                            item.Value,
                            item.Label))]));
        }

        return sets;
    }

    private static NclManagedPatchInspection InspectionFailure(
        NclManagedPatchInspectionCode code,
        string error,
        NclSourceIdentity? identity = null,
        int totalCheats = 0)
    {
        return new NclManagedPatchInspection(
            false,
            code,
            identity,
            [],
            totalCheats,
            false,
            error);
    }

    private static NclManagedPatchPreview Failure(
        NclManagedPatchPreviewCode code,
        string error,
        NclPpuConversionResult? conversion = null,
        PatMatch? match = null,
        PatchApplyPlan? plan = null,
        LibStRes? preparation = null)
    {
        return new NclManagedPatchPreview(
            false,
            code,
            conversion,
            match,
            plan,
            preparation,
            error);
    }
}
