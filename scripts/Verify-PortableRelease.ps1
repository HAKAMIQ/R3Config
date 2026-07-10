param(
    [Parameter(Mandatory = $true)][string]$Path,
    [string]$Version = '1.0.0'
)

$ErrorActionPreference = 'Stop'
$releasePath = (Resolve-Path -LiteralPath $Path).Path
if (-not (Test-Path -LiteralPath $releasePath -PathType Container)) {
    throw "Release path is not a directory: $Path"
}

$requiredFiles = @(
    'R3Config.exe',
    'Data/cat.json',
    'Data/can.yml',
    'Lang/ar-SA.json',
    'Lang/en-US.json',
    'README.md',
    'LICENSE',
    'CHANGELOG.md',
    'KL.md',
    'RN.md',
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

$manifestPath = Join-Path $releasePath 'SHA256SUMS.txt'
foreach ($line in Get-Content -LiteralPath $manifestPath) {
    if ($line -notmatch '^(?<hash>[0-9a-f]{64})  (?<path>.+)$') {
        throw "Invalid SHA256SUMS line: $line"
    }
    $candidate = Join-Path $releasePath $Matches.path
    if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) {
        throw "SHA256SUMS references a missing file: $($Matches.path)"
    }
    $actual = (Get-FileHash -Algorithm SHA256 -LiteralPath $candidate).Hash.ToLowerInvariant()
    if ($actual -ne $Matches.hash) {
        throw "SHA256 mismatch: $($Matches.path)"
    }
}

Write-Host "Portable release verification passed for R3Config $Version."
Write-Host "Files: $(@(Get-ChildItem -LiteralPath $releasePath -Recurse -File).Count)"
Write-Host "Language keys: $($arKeys.Count)"
Write-Host "Recommendation entries: $(@($catalog.Recommendations).Count)"
