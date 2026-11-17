param(
    [Parameter(Mandatory = $true)][string]$Path,
    [string]$Version = '0.1.1',
    [string]$ExpectedSignerThumbprint
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
    'Wpf.Ui.dll',
    'Wpf.Ui.Abstractions.dll',
    'Data/cat.json',
    'Data/compat-export.json',
    'Data/game-configs.json',
    'Data/wiki-notes.json',
    'Data/rpcs3-builds.json',
    'Data/diagnostics-kb.json',
    'Data/rpcn-evidence.json',
    'Data/rpcs3-knowledge.json',
    'Data/can.yml',
    'Data/patch_requirements.json',
    'Data/art.yml',
    'Data/art_dep.json',
    'Data/ncl.zip',
    'Lang/ar-SA.r3lang',
    'Lang/en-US.r3lang',
    'README.md',
    'LICENSE',
    'Changes.md',
    'KL.md',
    'RN.md',
    'CHEATS.md',
    'CANARY.md',
    'NCL.md',
    'THIRD-PARTY-NOTICES.md',
    'SHA256SUMS.txt'
)
foreach ($relativePath in $requiredFiles) {
    $candidate = Join-Path $releasePath $relativePath
    if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) {
        throw "Release is missing required file: $relativePath"
    }
}

$licenseText = Get-Content -Raw -LiteralPath (Join-Path $releasePath 'LICENSE')
foreach ($requiredLine in @('GNU GENERAL PUBLIC LICENSE', 'Version 3, 29 June 2007')) {
    if ($licenseText.IndexOf($requiredLine, [StringComparison]::Ordinal) -lt 0) {
        throw "LICENSE is missing the GNU GPL v3 text line: $requiredLine"
    }
}

$exe = Get-Item -LiteralPath (Join-Path $releasePath 'R3Config.exe')
if (-not $exe.VersionInfo.ProductVersion.StartsWith($Version, [StringComparison]::OrdinalIgnoreCase)) {
    throw "R3Config.exe product version '$($exe.VersionInfo.ProductVersion)' does not match '$Version'."
}

# A version without a prerelease suffix is a stable release: its executable
# must carry exactly that version (optionally +build metadata).
$stableRelease = $Version.IndexOf('-') -lt 0
if ($stableRelease) {
    $productVersion = $exe.VersionInfo.ProductVersion.Split('+')[0]
    if ($productVersion -ne $Version) {
        throw "Stable release R3Config.exe product version '$($exe.VersionInfo.ProductVersion)' is not exactly '$Version'."
    }
}

$manifestPath = [IO.Path]::GetFullPath((Join-Path $releasePath 'SHA256SUMS.txt'))

if (-not [string]::IsNullOrWhiteSpace($ExpectedSignerThumbprint)) {
    $manifestSignaturePath = [IO.Path]::GetFullPath(
        (Join-Path $releasePath 'SHA256SUMS.p7s'))

    if (-not (Test-Path -LiteralPath $manifestSignaturePath -PathType Leaf)) {
        throw 'Signed release is missing SHA256SUMS.p7s.'
    }

    Assert-ReleaseSignatures `
        -ExecutablePath $exe.FullName `
        -ManifestPath $manifestPath `
        -ManifestSignaturePath $manifestSignaturePath `
        -SignerThumbprint $ExpectedSignerThumbprint
}

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

# Secrets and local user state must never ship: private keys, signing
# material, local databases, environment files, or R3Config user-profile data.
$secretForbiddenExtensions = @('.pem', '.pfx', '.p12', '.p8', '.key', '.snk', '.dpapi', '.sqlite', '.sqlite3', '.db')
$secretForbiddenNames = @('settings.json', 'ui.json', 'hdis.json', 'backup-manifest.json')
$secretForbidden = Get-ChildItem -LiteralPath $releasePath -Recurse -Force |
    Where-Object {
        $_.Name -like '.env*' -or
        $_.Name -ieq 'node_modules' -or
        ((-not $_.PSIsContainer) -and (
            $secretForbiddenExtensions -contains $_.Extension.ToLowerInvariant() -or
            $secretForbiddenNames -contains $_.Name.ToLowerInvariant()))
    } |
    Select-Object -First 1
if ($secretForbidden) {
    throw "Release contains key material, a local database, or user-profile data: $($secretForbidden.FullName)"
}

$privateKeyMarker = 'PRIVATE KEY-----'
$developerPathMarkers = @(':\Users\', '\source\repos\')
foreach ($releaseFile in Get-ChildItem -LiteralPath $releasePath -Recurse -File -Force) {
    $bytes = [IO.File]::ReadAllBytes($releaseFile.FullName)
    $ascii = [Text.Encoding]::ASCII.GetString($bytes)
    # UTF-16 text can start at an even or an odd byte offset inside a binary.
    $utf16 = [Text.Encoding]::Unicode.GetString($bytes)
    $utf16Odd = if ($bytes.Length -gt 1) { [Text.Encoding]::Unicode.GetString($bytes, 1, $bytes.Length - 1) } else { '' }
    if ($ascii.IndexOf($privateKeyMarker, [StringComparison]::Ordinal) -ge 0 -or
        $utf16.IndexOf($privateKeyMarker, [StringComparison]::Ordinal) -ge 0 -or
        $utf16Odd.IndexOf($privateKeyMarker, [StringComparison]::Ordinal) -ge 0) {
        throw "Release file contains a private key: $($releaseFile.FullName)"
    }

    foreach ($marker in $developerPathMarkers) {
        if ($ascii.IndexOf($marker, [StringComparison]::OrdinalIgnoreCase) -ge 0 -or
            $utf16.IndexOf($marker, [StringComparison]::OrdinalIgnoreCase) -ge 0 -or
            $utf16Odd.IndexOf($marker, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
            throw "Release file contains a developer absolute path marker '$marker': $($releaseFile.FullName)"
        }
    }
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

$compatibilityPath = Join-Path $releasePath 'Data/compat-export.json'
$configDatabasePath = Join-Path $releasePath 'Data/game-configs.json'
$compatibilityHash = Get-Sha256Hex -LiteralPath $compatibilityPath
$configDatabaseHash = Get-Sha256Hex -LiteralPath $configDatabasePath
if ($compatibilityHash -ne '21d87c3cf1ce354747dedf73e0b0faee918ec7f017ea1a5f40d4fbcff5fc99e8') {
    throw "Data/compat-export.json SHA-256 drifted from the pinned November snapshot: $compatibilityHash"
}
if ($configDatabaseHash -ne 'b08d2fb6a6e8e663e62bb70a0f307c644272bf276a0c855adb49d86e93b1a391') {
    throw "Data/game-configs.json SHA-256 drifted from the pinned November snapshot: $configDatabaseHash"
}
$compatibilityDatabase = Get-Content -Raw -LiteralPath $compatibilityPath | ConvertFrom-Json
$configDatabase = Get-Content -Raw -LiteralPath $configDatabasePath | ConvertFrom-Json
if (@($compatibilityDatabase.results.PSObject.Properties).Count -ne 6798 -or
    @($configDatabase.games.PSObject.Properties).Count -ne 2294) {
    throw 'Bundled RPCS3 November game database counts are invalid.'
}

$forbiddenCbak = Join-Path $releasePath 'Data/cbak.json'
if (Test-Path -LiteralPath $forbiddenCbak) {
    throw 'Release contains obsolete Data/cbak.json.'
}

$legacyCheatPath = Join-Path $releasePath 'Data/cheats.yml'
if (Test-Path -LiteralPath $legacyCheatPath) {
    throw 'Release contains obsolete Data/cheats.yml.'
}

$thirdPartyNotice = Get-Content -Raw -LiteralPath (Join-Path $releasePath 'THIRD-PARTY-NOTICES.md')
foreach ($requiredLine in @(
        '## WPF-UI 4.3.0',
        '- License: MIT',
        'Copyright (c) 2021-2025 Leszek Pomianowski and WPF UI Contributors.',
        'THIRD-PARTY SOFTWARE NOTICES AND INFORMATION',
        '## YamlDotNet 18.1.0',
        'Copyright (c) 2008, 2009, 2010, 2011, 2012, 2013, 2014 Antoine Aubry and contributors',
        'Copyright (c) 2006 Kirill Simonov',
        '## .NET 8 runtime and Windows Desktop runtime (bundled)',
        '- Project: https://github.com/dotnet/runtime',
        '- Project: https://github.com/dotnet/wpf',
        'Copyright (c) .NET Foundation and Contributors',
        'https://github.com/dotnet/runtime/blob/release/8.0/THIRD-PARTY-NOTICES.TXT',
        '## RPCS3 Wiki content',
        'Creative Commons Attribution-ShareAlike 4.0 International (CC BY-SA 4.0)')) {
    if ($thirdPartyNotice.IndexOf($requiredLine, [StringComparison]::Ordinal) -lt 0) {
        throw "THIRD-PARTY-NOTICES.md is missing required notice line: $requiredLine"
    }
}
if (-not (Test-Path -LiteralPath (Join-Path $releasePath 'Wpf.Ui.dll') -PathType Leaf)) {
    throw 'Release is missing Wpf.Ui.dll covered by THIRD-PARTY-NOTICES.md.'
}

$nclPath = Join-Path $releasePath 'Data/ncl.zip'
$nclNoticePath = Join-Path $releasePath 'NCL.md'
$nclHash = Get-Sha256Hex -LiteralPath $nclPath
$expectedNclHash = 'C4C70C6E73A225F1E15096A3AEA06DD05461F7EF0CBB4BE8B106577ABC22D8B8'
if ($nclHash -ne $expectedNclHash) {
    throw "Data/ncl.zip SHA-256 drifted from the pinned bundled corpus: $nclHash"
}
$nclNotice = Get-Content -Raw -LiteralPath $nclNoticePath
$requiredNclNoticeLines = @(
    'R3Config verified merged NCL corpus',
    'Source lineage: 5 records',
    'Source NCL files: 2910',
    "Bundled artifact SHA-256: $expectedNclHash",
    'Embedded provenance schema: 2',
    'Upstream ArtemisPS3 license notice: LICENSE-ArtemisPS3.txt'
)
foreach ($requiredLine in $requiredNclNoticeLines) {
    if ($nclNotice.IndexOf($requiredLine, [StringComparison]::Ordinal) -lt 0) {
        throw "NCL.md is missing required bundled-corpus provenance line: $requiredLine"
    }
}
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$nclArchive = [IO.Compression.ZipFile]::OpenRead($nclPath)
try {
    $nclEntries = @($nclArchive.Entries | Where-Object {
        $_.Name.Length -gt 0 -and $_.FullName.EndsWith('.ncl', [StringComparison]::OrdinalIgnoreCase)
    })
    if ($nclEntries.Count -ne 2910) {
        throw "Data/ncl.zip expected 2910 NCL entries but found $($nclEntries.Count)."
    }
    if ($nclArchive.Entries.Count -ne 2912) {
        throw 'Data/ncl.zip must contain exactly 2910 NCL entries plus LICENSE-ArtemisPS3.txt and PROVENANCE.json.'
    }
    $unsafeNclEntry = $nclEntries | Where-Object {
        -not $_.FullName.StartsWith('codes/', [StringComparison]::Ordinal) -or
        $_.FullName.IndexOf('\', [StringComparison]::Ordinal) -ge 0 -or
        @($_.FullName.Split('/') | Where-Object { $_ -eq '..' }).Count -gt 0
    } | Select-Object -First 1
    if ($unsafeNclEntry) {
        throw "Data/ncl.zip contains an unsafe NCL entry path: $($unsafeNclEntry.FullName)"
    }

    $licenseEntry = $nclArchive.GetEntry('LICENSE-ArtemisPS3.txt')
    $provenanceEntry = $nclArchive.GetEntry('PROVENANCE.json')
    if ($null -eq $licenseEntry -or $null -eq $provenanceEntry) {
        throw 'Data/ncl.zip is missing its embedded license or provenance record.'
    }

    $licenseStream = $licenseEntry.Open()
    try {
        $licenseSha = [Security.Cryptography.SHA256]::Create()
        try {
            $licenseHash = [BitConverter]::ToString($licenseSha.ComputeHash($licenseStream)).Replace('-', '')
        }
        finally {
            $licenseSha.Dispose()
        }
    }
    finally {
        $licenseStream.Dispose()
    }
    if ($licenseHash -ne '6A3E4BA999067B1C412F1740282BC077F8EDF5F7178EA085ACB5F5CCA6FECB46') {
        throw 'Data/ncl.zip ArtemisPS3 license notice does not match the proven upstream license text.'
    }

    $reader = [IO.StreamReader]::new($provenanceEntry.Open(), [Text.Encoding]::UTF8, $true)
    try {
        $provenance = $reader.ReadToEnd() | ConvertFrom-Json
    }
    finally {
        $reader.Dispose()
    }

    if ($provenance.schema -ne 2 -or
        $provenance.artifactKind -ne 'R3Config NCL corpus' -or
        $provenance.nclFileCount -ne 2910 -or
        $provenance.dialect -ne 'ArtemisPs3' -or
        $provenance.generatedBy -ne 'R3Config deterministic NCL corpus builder' -or
        $provenance.licenseNotice -ne 'LICENSE-ArtemisPS3.txt') {
        throw 'Data/ncl.zip embedded schema-2 provenance header is invalid.'
    }

    $expectedSources = @{
        'ALDOSTOOLS_CODELIST' = @('upstreamArchive', 'A1ABC379DD6F8BB65399EACD97D147A33C5907BAFE2C1E0B468BA86A786E8CEA', 2501)
        'BUCANERO_CURRENT' = @('upstreamArchive', '8289EFDB5B5441A579C1ECAC8E608852FAB4D225A92728B5EB8D6227ED89EBA2', 2540)
        'DNAWRKSHP_HISTORICAL' = @('historicalUpstreamArchive', '8AC628D238D0501D699552EECF40C836D417DCADD66419699DC2C56BBE6D01A6', 1287)
        'R3CONFIG_CURRENT' = @('legacyCarryForward', '80AA918A4F98A7491A0D684456D7620486296C54B396FEA0C89D117CBF5DD17D', 2497)
        'AR_SITE_CODELIST_2026' = @('manifestDownloadArchive', '734896CB7DD1EEE2B9E6720E6E75D9D852626F213C8676B03914257DF8555DC3', 2495)
    }
    $sources = @($provenance.sources)
    if ($sources.Count -ne $expectedSources.Count) {
        throw "Data/ncl.zip provenance expected $($expectedSources.Count) source records but found $($sources.Count)."
    }
    $ids = @($sources | ForEach-Object { [string]$_.id })
    if (@($ids | Sort-Object -Unique).Count -ne $ids.Count) {
        throw 'Data/ncl.zip provenance contains duplicate source IDs.'
    }
    foreach ($source in $sources) {
        if ([string]::IsNullOrWhiteSpace([string]$source.id) -or
            [string]::IsNullOrWhiteSpace([string]$source.kind) -or
            [string]::IsNullOrWhiteSpace([string]$source.sha256) -or
            [int]$source.nclFileCount -le 0) {
            throw 'Data/ncl.zip provenance contains an incomplete source record.'
        }
        if (-not $expectedSources.ContainsKey([string]$source.id)) {
            throw "Data/ncl.zip provenance contains an unexpected source ID: $($source.id)"
        }
        $expected = $expectedSources[[string]$source.id]
        if ([string]$source.kind -ne $expected[0] -or
            [string]$source.sha256 -ne $expected[1] -or
            [int]$source.nclFileCount -ne [int]$expected[2]) {
            throw "Data/ncl.zip provenance mismatch for source ID: $($source.id)"
        }
    }
}
finally {
    $nclArchive.Dispose()
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
    $artemisDependency.sourceRevision -ne 'fd267a65759a2f1dcae31e362d318de7a6e16594' -or
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
    'Source artifact: Artemis-Patch-Collection-RPCS3-fd267a6-r3fix1.zip',
    'Source reference: pinned ChiDreams commit fd267a65759a2f1dcae31e362d318de7a6e16594 via Artemis-Patch-Collection-RPCS3-fd267a6-r3fix1.zip; 2 reviewed source corrections',
    'Source archive SHA-256: cefd5b7475a67d3c36f165935c60f611b8ac6e10a095f4524e53c86fe7b430de',
    'Source patch files: 237',
    'Candidate entries: 1956',
    'Accepted entries: 1922',
    'Rejected records: 34',
    "Generated art.yml SHA-256: $artemisHash",
    'Dependency metadata rules: 23',
    'Dependency source revision: fd267a65759a2f1dcae31e362d318de7a6e16594',
    "Generated art_dep.json SHA-256: $artemisDependencyHash",
    'Manager reference artifact: ARTEMIS-RPCS3-Cheat-Manager-2.00.zip',
    'Manager reference archive SHA-256: caaab46ad39c671470f1d2593958da8c2b9111348660cbd3f0f5a53e93c41604',
    'License: MIT'
)
foreach ($requiredLine in $requiredArtemisNoticeLines) {
    if ($artemisNotice.IndexOf($requiredLine, [StringComparison]::Ordinal) -lt 0) {
        throw "CHEATS.md is missing required Artemis collection provenance line: $requiredLine"
    }
}
if ($acceptedEntries -ne 1922) {
    throw "Data/art.yml expected 1922 accepted Artemis definitions but found $acceptedEntries."
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
