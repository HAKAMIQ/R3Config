param(
    [string]$Configuration = 'Release',
    [string]$Rpcs3Path,
    [string]$WikiXml,
    [string]$WikiEvidenceXml,
    [string]$OfficialPatch,
    [switch]$LaunchApp
)

$ErrorActionPreference = 'Stop'

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = (Resolve-Path (Join-Path $scriptRoot '..')).Path
$toolchain = Join-Path $scriptRoot 'R3Toolchain.ps1'
. $toolchain

Assert-R3DotNetSdk -RepositoryRoot $repoRoot | Out-Null

Push-Location $repoRoot
try {
    $blockedGates = New-Object System.Collections.Generic.List[string]

    # Production canonical Wiki source, chosen explicitly by the project owner
    # (previously June 2026 was the intended baseline). Keep in sync with
    # WikiSvc.CanonicalSnapshot*; the identity is re-verified by --wiki-verify.
    $productionWikiXml = 'F:\Downloaded-Websites\rpcs3\Wiki-Snapshots\Candidate\RPCS3+Wiki-20260920160231.xml'
    $productionWikiSha256 = '7977CA26176E12DD167241CDE77D68E6CC64CE24CB39C2682FD5E686CA7936C5'
    if ([string]::IsNullOrWhiteSpace($WikiXml) -and (Test-Path -LiteralPath $productionWikiXml)) {
        $WikiXml = $productionWikiXml
    }
    $verificationProject = '.\Tst\R3Test.csproj'
    Invoke-R3DotNet -RepositoryRoot $repoRoot -Arguments @('restore', $verificationProject)
    Invoke-R3DotNet -RepositoryRoot $repoRoot -Arguments @(
        'build',
        $verificationProject,
        '-c', $Configuration,
        '--no-restore',
        '--nologo'
    )

    $testExe = Join-Path $repoRoot ("Tst\bin\$Configuration\net8.0-windows\R3Test.exe")
    $testArguments = @()
    if (-not [string]::IsNullOrWhiteSpace($Rpcs3Path)) {
        $testArguments += $Rpcs3Path
    }
    Invoke-R3Executable -LiteralPath $testExe -Arguments $testArguments -WorkingDirectory $repoRoot

    if (-not [string]::IsNullOrWhiteSpace($WikiXml)) {
        $wikiSnapshot = (Resolve-Path -LiteralPath $WikiXml).Path
        $sha256 = [System.Security.Cryptography.SHA256]::Create()
        try {
            $wikiStream = [System.IO.File]::OpenRead($wikiSnapshot)
            try {
                $wikiHash = [System.BitConverter]::ToString(
                    $sha256.ComputeHash($wikiStream)).Replace('-', '')
            }
            finally {
                $wikiStream.Dispose()
            }
        }
        finally {
            $sha256.Dispose()
        }
        if ($wikiHash -ne $productionWikiSha256) {
            throw "WIKI_CANONICAL_PARITY_GATE: $wikiSnapshot has SHA-256 $wikiHash; the production canonical source is RPCS3+Wiki-20260920160231.xml / $productionWikiSha256."
        }
        Invoke-R3Executable `
            -LiteralPath $testExe `
            -Arguments @('--wiki-verify', $wikiSnapshot) `
            -WorkingDirectory $repoRoot
        Write-Host 'WIKI_CANONICAL_PARITY_GATE: PASS - pinned production September snapshot matched the shipped cat.json and wiki-notes.json exactly.'
    }
    else {
        Write-Host ('WIKI_CANONICAL_PARITY_GATE: BLOCKED/SKIPPED - the production canonical source ' +
            'RPCS3+Wiki-20260920160231.xml was not found at the default path and no -WikiXml was supplied; ' +
            'a different snapshot is not a substitute.')
        $blockedGates.Add('WIKI_CANONICAL_PARITY_GATE')
    }

    if (-not [string]::IsNullOrWhiteSpace($WikiEvidenceXml)) {
        $wikiEvidence = (Resolve-Path -LiteralPath $WikiEvidenceXml).Path
        Invoke-R3Executable `
            -LiteralPath $testExe `
            -Arguments @('--wiki-delta', $wikiEvidence) `
            -WorkingDirectory $repoRoot
        Write-Host 'WIKI_NEWER_EVIDENCE_DELTA: COMPLETED - canonical provenance was not changed.'
    }
    else {
        Write-Host 'WIKI_NEWER_EVIDENCE_DELTA: SKIPPED - no -WikiEvidenceXml supplied.'
    }

    # REQUIRED: current production guarantees of the Official patch path.
    # Needs no external corpus and never reads the installed RPCS3 patch.yml.
    Invoke-R3Executable `
        -LiteralPath $testExe `
        -Arguments @('--official-runtime-only') `
        -WorkingDirectory $repoRoot
    Write-Host ('OFFICIAL_RUNTIME_COMPATIBILITY_GATE: PASS - RPCS3 patch.yml read-only, schema 1.2 fail-closed parsing, ' +
        'explicit unsupported structures, strict Serial/version/hash applicability, Unknown != Mismatch, ' +
        'patch_config.yml preserved, Official isolated from Wiki/Canary/Artemis/imported patches.')

    # OPTIONAL: replay of the historical 2026-08-25 corpus (897858 bytes,
    # SHA-256 45F746AF...E8AD). Its absence never blocks release, and an
    # installed/latest patch.yml is never promoted to a canonical corpus.
    if (-not [string]::IsNullOrWhiteSpace($OfficialPatch)) {
        $officialSnapshot = (Resolve-Path -LiteralPath $OfficialPatch).Path
        Invoke-R3Executable `
            -LiteralPath $testExe `
            -Arguments @('--official-corpus', $officialSnapshot) `
            -WorkingDirectory $repoRoot
        Write-Host 'OFFICIAL_HISTORICAL_CORPUS_REPLAY: PASS - historical corpus identity and parser regression passed.'
    }
    else {
        Write-Host ('OFFICIAL_HISTORICAL_CORPUS_REPLAY: NOT_AVAILABLE - optional; the historical 2026-08-25 corpus ' +
            '(897858 bytes, SHA-256 45F746AFC3D4EEA2450F32F92769BB555029807497CFF3512B633428EF74E8AD) was not supplied. Not a release blocker.')
    }

    Invoke-R3PowerShellFile `
        -ScriptPath (Join-Path $scriptRoot 'SrcChk.ps1') `
        -Arguments @('-Path', $repoRoot)

    if ($LaunchApp) {
        Write-Host 'Launching R3Config for manual QA. Close the window to finish.'
        $appExe = Join-Path $repoRoot ("App\bin\$Configuration\net8.0-windows\R3Config.exe")
        Invoke-R3Executable -LiteralPath $appExe -WorkingDirectory $repoRoot
    }

    if ($blockedGates.Count -gt 0) {
        Write-Host ('Release verification completed with BLOCKED external corpus gate(s): ' +
            [string]::Join(', ', $blockedGates))
    }
    else {
        Write-Host 'Release verification passed.'
    }
}
finally {
    Pop-Location
}
