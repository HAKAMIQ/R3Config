param(
    [Parameter(Mandatory = $true)][string]$Path,
    [string]$Version = '1.0.1',
    [Parameter(Mandatory = $true)][string]$ExpectedSignerThumbprint
)

function Get-NormalizedThumbprint {
    param([Parameter(Mandatory = $true)][string]$Thumbprint)

    return ($Thumbprint -replace '[^0-9A-Fa-f]', '').ToUpperInvariant()
}


function Import-PkcsAssembly {
    if ('Security.Cryptography.Pkcs.SignedCms' -as [type]) {
        return
    }

    foreach ($assemblyName in @(
        'System.Security.Cryptography.Pkcs',
        'System.Security'
    )) {
        try {
            Add-Type -AssemblyName $assemblyName -ErrorAction Stop
        }
        catch {
        }

        if ('Security.Cryptography.Pkcs.SignedCms' -as [type]) {
            return
        }
    }

    throw 'System.Security.Cryptography.Pkcs is unavailable. Release signature verification cannot continue.'
}

function Assert-ReleaseSignatures {
    param(
        [Parameter(Mandatory = $true)][string]$ExecutablePath,
        [Parameter(Mandatory = $true)][string]$ManifestPath,
        [Parameter(Mandatory = $true)][string]$ManifestSignaturePath,
        [Parameter(Mandatory = $true)][string]$SignerThumbprint
    )

    $expected = Get-NormalizedThumbprint -Thumbprint $SignerThumbprint
    if ([string]::IsNullOrWhiteSpace($expected)) {
        throw 'Expected signer thumbprint is empty.'
    }

    $authenticode = Get-AuthenticodeSignature -FilePath $ExecutablePath
    if ($authenticode.Status -ne [System.Management.Automation.SignatureStatus]::Valid -or
        -not $authenticode.SignerCertificate) {
        throw "R3Config.exe Authenticode signature is not valid: $($authenticode.Status) $($authenticode.StatusMessage)"
    }

    $actualExeSigner = Get-NormalizedThumbprint -Thumbprint $authenticode.SignerCertificate.Thumbprint
    if ($actualExeSigner -ne $expected) {
        throw "R3Config.exe signer thumbprint '$actualExeSigner' does not match '$expected'."
    }

    Import-PkcsAssembly
    $content = [IO.File]::ReadAllBytes($ManifestPath)
    $contentInfo = [Security.Cryptography.Pkcs.ContentInfo]::new($content)
    $cms = [Security.Cryptography.Pkcs.SignedCms]::new($contentInfo, $true)
    $cms.Decode([IO.File]::ReadAllBytes($ManifestSignaturePath))
    $cms.CheckSignature($true)

    if ($cms.SignerInfos.Count -ne 1 -or
        -not $cms.SignerInfos[0].Certificate) {
        throw 'SHA256SUMS detached signature must contain exactly one signer certificate.'
    }

    $actualManifestSigner = Get-NormalizedThumbprint -Thumbprint $cms.SignerInfos[0].Certificate.Thumbprint
    if ($actualManifestSigner -ne $expected) {
        throw "SHA256SUMS signer thumbprint '$actualManifestSigner' does not match '$expected'."
    }
}

function Get-Sha256Hex {
    param([Parameter(Mandatory = $true)][string]$LiteralPath)

    $stream = [IO.File]::OpenRead($LiteralPath)
    try {
        $sha256 = [Security.Cryptography.SHA256]::Create()
        try {
            return [BitConverter]::ToString($sha256.ComputeHash($stream)).Replace('-', '').ToLowerInvariant()
        }
        finally {
            $sha256.Dispose()
        }
    }
    finally {
        $stream.Dispose()
    }
}

$ErrorActionPreference = 'Stop'
$releasePath = (Resolve-Path -LiteralPath $Path).Path
if (-not (Test-Path -LiteralPath $releasePath -PathType Container)) {
    throw "Release path is not a directory: $Path"
}

$requiredFiles = @(
    'R3Config.exe',
    'Data/cat.json',
    'Data/can.yml',
    'Data/art.yml',
    'Data/art_dep.json',
    'Lang/ar-SA.r3lang',
    'Lang/en-US.r3lang',
    'README.md',
    'LICENSE',
    'Changes.md',
    'KL.md',
    'RN.md',
    'CHEATS.md',
    'CANARY.md',
    'SHA256SUMS.txt',
    'SHA256SUMS.p7s'
)
foreach ($relativePath in $requiredFiles) {
    $candidate = Join-Path $releasePath $relativePath
    if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) {
        throw "Release is missing required file: $relativePath"
    }
}

$exe = Get-Item -LiteralPath (Join-Path $releasePath 'R3Config.exe')
if (-not $exe.VersionInfo.ProductVersion.StartsWith($Version, [StringComparison]::OrdinalIgnoreCase)) {
    throw "R3Config.exe product version '$($exe.VersionInfo.ProductVersion)' does not match '$Version'."
}

$manifestPath = [IO.Path]::GetFullPath((Join-Path $releasePath 'SHA256SUMS.txt'))
$manifestSignaturePath = [IO.Path]::GetFullPath((Join-Path $releasePath 'SHA256SUMS.p7s'))
Assert-ReleaseSignatures `
    -ExecutablePath $exe.FullName `
    -ManifestPath $manifestPath `
    -ManifestSignaturePath $manifestSignaturePath `
    -SignerThumbprint $ExpectedSignerThumbprint

$forbiddenDirectoryNames = @('bin', 'obj', '.vs', 'artifacts', 'publish')
$forbiddenDirectory = Get-ChildItem -LiteralPath $releasePath -Recurse -Directory -Force |
    Where-Object { $forbiddenDirectoryNames -contains $_.Name.ToLowerInvariant() } |
    Select-Object -First 1
if ($forbiddenDirectory) {
    throw "Release contains forbidden directory: $($forbiddenDirectory.FullName)"
}

$forbiddenExtensions = @('.cs', '.xaml', '.csproj', '.slnx', '.user', '.suo', '.pdb')
$forbiddenSource = Get-ChildItem -LiteralPath $releasePath -Recurse -File -Force |
    Where-Object { $forbiddenExtensions -contains $_.Extension.ToLowerInvariant() } |
    Select-Object -First 1
if ($forbiddenSource) {
    throw "Release contains forbidden source/debug file: $($forbiddenSource.FullName)"
}

$staleArtImport = Get-ChildItem -LiteralPath $releasePath -Recurse -File -Force |
    Where-Object { $_.Name -match '(?:\.r3artimport\.(?:txn\.json|backup|lock)|\.r3stage)$' } |
    Select-Object -First 1
if ($staleArtImport) {
    throw "Release contains an interrupted Artemis import transaction artifact: $($staleArtImport.FullName)"
}

$forbiddenNames = @('App.exe', 'rpcs3.exe', 'PS3UPDAT.PUP', 'PSP2UPDAT.PUP')
$forbiddenPayloadExtensions = @('.iso', '.pkg', '.chd', '.cso', '.zso', '.cue', '.gdi', '.pup', '.rap', '.rif', '.edat', '.elf', '.self', '.sprx')
$forbiddenPayload = Get-ChildItem -LiteralPath $releasePath -Recurse -File -Force |
    Where-Object { $forbiddenNames -contains $_.Name -or $forbiddenPayloadExtensions -contains $_.Extension.ToLowerInvariant() } |
    Select-Object -First 1
if ($forbiddenPayload) {
    throw "Release contains forbidden emulator, firmware, game, or legacy payload: $($forbiddenPayload.FullName)"
}

$publishedJsonLanguage = Get-ChildItem -LiteralPath (Join-Path $releasePath 'Lang') -File -Force -ErrorAction SilentlyContinue |
    Where-Object { $_.Extension -eq '.json' } |
    Select-Object -First 1
if ($publishedJsonLanguage) {
    throw "Release exposes editable JSON language source: $($publishedJsonLanguage.FullName)"
}

foreach ($languagePack in @('Lang/ar-SA.r3lang', 'Lang/en-US.r3lang')) {
    $languagePackPath = Join-Path $releasePath $languagePack
    $stream = [IO.File]::OpenRead($languagePackPath)
    try {
        $header = New-Object byte[] 8
        if ($stream.Read($header, 0, $header.Length) -ne $header.Length -or
            [Text.Encoding]::ASCII.GetString($header) -ne 'R3LANG01') {
            throw "Release language pack header is invalid: $languagePack"
        }
    }
    finally {
        $stream.Dispose()
    }
}

$catalog = Get-Content -Raw -LiteralPath (Join-Path $releasePath 'Data/cat.json') | ConvertFrom-Json
if (@($catalog.Recommendations).Count -eq 0) {
    throw 'Release recommendation catalog is empty.'
}

$forbiddenCbak = Join-Path $releasePath 'Data/cbak.json'
if (Test-Path -LiteralPath $forbiddenCbak) {
    throw 'Release contains obsolete Data/cbak.json.'
}

$legacyCheatPath = Join-Path $releasePath 'Data/cheats.yml'
if (Test-Path -LiteralPath $legacyCheatPath) {
    throw 'Release contains obsolete Data/cheats.yml.'
}

$artemisPath = Join-Path $releasePath 'Data/art.yml'
$artemisNoticePath = Join-Path $releasePath 'CHEATS.md'
$artemisHash = Get-Sha256Hex -LiteralPath $artemisPath
$artemisText = Get-Content -Raw -LiteralPath $artemisPath
$artemisNotice = Get-Content -Raw -LiteralPath $artemisNoticePath
$artemisDependencyPath = Join-Path $releasePath 'Data/art_dep.json'
$artemisDependencyHash = Get-Sha256Hex -LiteralPath $artemisDependencyPath
$artemisDependency = Get-Content -Raw -LiteralPath $artemisDependencyPath | ConvertFrom-Json
$artemisDependencyRules = @($artemisDependency.rules)
if ($artemisDependency.schemaVersion -ne '1' -or
    $artemisDependency.sourceIdentity -ne 'Artemis-Patch-Collection-RPCS3' -or
    $artemisDependency.sourceRevision -ne 'acfd45b61f8295298523548a9ff3406488736d68' -or
    $artemisDependency.artemisSha256 -ne $artemisHash -or
    $artemisDependencyRules.Count -ne 23) {
    throw 'Data/art_dep.json provenance, corpus hash, schema, or reviewed rule count is invalid.'
}
foreach ($rule in $artemisDependencyRules) {
    if ([string]::IsNullOrWhiteSpace($rule.runKey) -or
        [string]::IsNullOrWhiteSpace($rule.serial) -or
        [string]::IsNullOrWhiteSpace($rule.version) -or
        [string]::IsNullOrWhiteSpace($rule.patchTitle) -or
        [string]::IsNullOrWhiteSpace($rule.evidence) -or
        @('All', 'Any') -notcontains $rule.mode -or
        @($rule.requires).Count -eq 0 -or
        @($rule.requires | Where-Object { [string]::IsNullOrWhiteSpace($_) }).Count -ne 0) {
        throw "Data/art_dep.json contains an invalid reviewed dependency rule for '$($rule.patchTitle)'."
    }
}
$acceptedEntries = [regex]::Matches(
    $artemisText,
    '(?m)^(?:(?:[A-Z0-9]{9})-)?(?:PPU|SPU|PRX|OVL)-[^:\r\n]+:\s*$').Count
$requiredArtemisNoticeLines = @(
    'Canonical cheat source: Artemis Patch Collection for RPCS3 by ChiDreams',
    'Source URL: https://github.com/chidreams/Artemis-Patch-Collection-RPCS3',
    'Source artifact: Artemis-Patch-Collection-RPCS3-acfd45b-r3fix2.zip',
    'Source reference: pinned ChiDreams commit acfd45b61f8295298523548a9ff3406488736d68 via Artemis-Patch-Collection-RPCS3-acfd45b-r3fix2.zip; 13 reviewed source corrections',
    'Source archive SHA-256: 26a72f769fb1f7cb51daa8502fbc39b817a904fa1073dfa2c602a8c5f2d76ac9',
    'Source patch files: 178',
    'Candidate entries: 1496',
    'Accepted entries: 1471',
    'Rejected records: 25',
    "Generated art.yml SHA-256: $artemisHash",
    'Dependency metadata rules: 23',
    'Dependency source revision: acfd45b61f8295298523548a9ff3406488736d68',
    "Generated art_dep.json SHA-256: $artemisDependencyHash",
    'Manager reference artifact: ARTEMIS-RPCS3-Cheat-Manager-1.0.1-RC4(1).zip',
    'Manager reference archive SHA-256: 372a033655d9c40bbf306a1d56dc5e6749f63400522a577bd15eeadfe175780f',
    'License: MIT'
)
foreach ($requiredLine in $requiredArtemisNoticeLines) {
    if ($artemisNotice.IndexOf($requiredLine, [StringComparison]::Ordinal) -lt 0) {
        throw "CHEATS.md is missing required Artemis collection provenance line: $requiredLine"
    }
}
if ($acceptedEntries -ne 1471) {
    throw "Data/art.yml expected 1471 accepted Artemis definitions but found $acceptedEntries."
}
if (-not $artemisText.StartsWith("Version: 1.2`n", [StringComparison]::Ordinal)) {
    throw 'Data/art.yml must begin with Version: 1.2.'
}
if ($artemisText -match '(?m)^Anchors(?:_\d+)?:') {
    throw 'Data/art.yml must contain expanded definitions without a top-level Anchors mapping.'
}
foreach ($obsoleteSidecar in @('artm.json', 'artb.json', 'artr.json.gz')) {
    $obsoletePath = Join-Path $releasePath ("Data/" + $obsoleteSidecar)
    if (Test-Path -LiteralPath $obsoletePath -PathType Leaf) {
        throw "Release contains removed Artemis sidecar: Data/$obsoleteSidecar"
    }
}

$canaryPath = Join-Path $releasePath 'Data/can.yml'
$canaryNoticePath = Join-Path $releasePath 'CANARY.md'
$canaryHash = Get-Sha256Hex -LiteralPath $canaryPath
$canaryNotice = Get-Content -Raw -LiteralPath $canaryNoticePath
$requiredCanaryNoticeLines = @(
    "Catalog-SHA256: $canaryHash",
    'Runtime-Key-Count: 86',
    'License-Status: CC-BY-SA-4.0-unless-otherwise-noted',
    'Provenance-Status: rpcs3-wiki-cargo-snapshot-user-supplied'
)
foreach ($requiredLine in $requiredCanaryNoticeLines) {
    if ($canaryNotice.IndexOf($requiredLine, [StringComparison]::Ordinal) -lt 0) {
        throw "CANARY.md is missing required provenance line: $requiredLine"
    }
}

$releasePrefix = $releasePath.TrimEnd([IO.Path]::DirectorySeparatorChar) +
    [IO.Path]::DirectorySeparatorChar
$manifestEntries = [System.Collections.Generic.HashSet[string]]::new(
    [StringComparer]::OrdinalIgnoreCase)
foreach ($line in Get-Content -LiteralPath $manifestPath) {
    if ($line -notmatch '^(?<hash>[0-9a-f]{64})  (?<path>.+)$') {
        throw "Invalid SHA256SUMS line: $line"
    }
    if ([IO.Path]::IsPathRooted($Matches.path)) {
        throw "SHA256SUMS path must be relative: $($Matches.path)"
    }
    $relativePath = $Matches.path.Replace(
        '/',
        [IO.Path]::DirectorySeparatorChar)
    $candidate = [IO.Path]::GetFullPath((Join-Path $releasePath $relativePath))
    if (-not $candidate.StartsWith(
            $releasePrefix,
            [StringComparison]::OrdinalIgnoreCase)) {
        throw "SHA256SUMS path escapes the release directory: $($Matches.path)"
    }
    if ([string]::Equals(
            $candidate,
            $manifestPath,
            [StringComparison]::OrdinalIgnoreCase)) {
        throw 'SHA256SUMS must not contain a hash for itself.'
    }
    $normalizedPath = $candidate.Substring($releasePrefix.Length).Replace('\', '/')
    if (-not $manifestEntries.Add($normalizedPath)) {
        throw "SHA256SUMS contains a duplicate path: $normalizedPath"
    }
    if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) {
        throw "SHA256SUMS references a missing file: $($Matches.path)"
    }
    $actual = Get-Sha256Hex -LiteralPath $candidate
    if ($actual -ne $Matches.hash) {
        throw "SHA256 mismatch: $($Matches.path)"
    }
}

$releaseFiles = @(
    Get-ChildItem -LiteralPath $releasePath -Recurse -File -Force |
        Where-Object {
            -not [string]::Equals(
                $_.FullName,
                $manifestPath,
                [StringComparison]::OrdinalIgnoreCase) -and
            -not [string]::Equals(
                $_.FullName,
                $manifestSignaturePath,
                [StringComparison]::OrdinalIgnoreCase)
        } |
        ForEach-Object {
            $_.FullName.Substring($releasePrefix.Length).Replace('\', '/')
        }
)
$unlistedFiles = @(
    $releaseFiles |
        Where-Object { -not $manifestEntries.Contains($_) } |
        Sort-Object
)
if ($unlistedFiles.Count -gt 0) {
    throw "Release contains files not listed in SHA256SUMS: $($unlistedFiles -join ', ')"
}
if ($manifestEntries.Count -ne $releaseFiles.Count) {
    throw "SHA256SUMS entry count '$($manifestEntries.Count)' does not match release file count '$($releaseFiles.Count)'."
}

& (Join-Path $PSScriptRoot 'PublishedLocalizationSmoke.ps1') -PublishDir $releasePath

Write-Host "Portable release verification passed for R3Config $Version."
Write-Host "Files: $(@(Get-ChildItem -LiteralPath $releasePath -Recurse -File).Count)"
Write-Host "Compiled language packs: 2"
Write-Host "Recommendation entries: $(@($catalog.Recommendations).Count)"
