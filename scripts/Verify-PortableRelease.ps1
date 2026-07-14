param(
    [Parameter(Mandatory = $true)][string]$Path,
    [string]$Version = '1.0.1'
)

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
    'Data/cheats.yml',
    'Lang/ar-SA.json',
    'Lang/en-US.json',
    'README.md',
    'LICENSE',
    'CHANGELOG.md',
    'KL.md',
    'RN.md',
    'CHEATS.md',
    'CANARY.md',
    'SHA256SUMS.txt'
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

$forbiddenNames = @('App.exe', 'rpcs3.exe', 'PS3UPDAT.PUP', 'PSP2UPDAT.PUP')
$forbiddenPayloadExtensions = @('.iso', '.pkg', '.chd', '.cso', '.zso', '.cue', '.gdi', '.pup', '.rap', '.rif', '.edat', '.elf', '.self', '.sprx')
$forbiddenPayload = Get-ChildItem -LiteralPath $releasePath -Recurse -File -Force |
    Where-Object { $forbiddenNames -contains $_.Name -or $forbiddenPayloadExtensions -contains $_.Extension.ToLowerInvariant() } |
    Select-Object -First 1
if ($forbiddenPayload) {
    throw "Release contains forbidden emulator, firmware, game, or legacy payload: $($forbiddenPayload.FullName)"
}

$ar = Get-Content -Raw -LiteralPath (Join-Path $releasePath 'Lang/ar-SA.json') | ConvertFrom-Json
$en = Get-Content -Raw -LiteralPath (Join-Path $releasePath 'Lang/en-US.json') | ConvertFrom-Json
$arKeys = @($ar.PSObject.Properties.Name | Sort-Object)
$enKeys = @($en.PSObject.Properties.Name | Sort-Object)
if (Compare-Object $arKeys $enKeys) {
    throw 'Arabic and English release language keys do not match.'
}

$catalog = Get-Content -Raw -LiteralPath (Join-Path $releasePath 'Data/cat.json') | ConvertFrom-Json
if (@($catalog.Recommendations).Count -eq 0) {
    throw 'Release recommendation catalog is empty.'
}

$forbiddenCbak = Join-Path $releasePath 'Data/cbak.json'
if (Test-Path -LiteralPath $forbiddenCbak) {
    throw 'Release contains obsolete Data/cbak.json.'
}

$cheatPath = Join-Path $releasePath 'Data/cheats.yml'
$cheatNoticePath = Join-Path $releasePath 'CHEATS.md'
$cheatHash = Get-Sha256Hex -LiteralPath $cheatPath
$cheatNotice = Get-Content -Raw -LiteralPath $cheatNoticePath
$requiredCheatNoticeLines = @(
    "Catalog-SHA256: $cheatHash",
    'License-Status: upstream-license-not-provided',
    'Provenance-Status: aggregate-source-not-recorded'
)
foreach ($requiredLine in $requiredCheatNoticeLines) {
    if ($cheatNotice.IndexOf($requiredLine, [StringComparison]::Ordinal) -lt 0) {
        throw "CHEATS.md is missing required provenance line: $requiredLine"
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

$manifestPath = Join-Path $releasePath 'SHA256SUMS.txt'
foreach ($line in Get-Content -LiteralPath $manifestPath) {
    if ($line -notmatch '^(?<hash>[0-9a-f]{64})  (?<path>.+)$') {
        throw "Invalid SHA256SUMS line: $line"
    }
    $candidate = Join-Path $releasePath $Matches.path
    if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) {
        throw "SHA256SUMS references a missing file: $($Matches.path)"
    }
    $actual = Get-Sha256Hex -LiteralPath $candidate
    if ($actual -ne $Matches.hash) {
        throw "SHA256 mismatch: $($Matches.path)"
    }
}

Write-Host "Portable release verification passed for R3Config $Version."
Write-Host "Files: $(@(Get-ChildItem -LiteralPath $releasePath -Recurse -File).Count)"
Write-Host "Language keys: $($arKeys.Count)"
Write-Host "Recommendation entries: $(@($catalog.Recommendations).Count)"
