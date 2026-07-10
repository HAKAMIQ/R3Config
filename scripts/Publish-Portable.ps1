param(
    [string]$Configuration = 'Release',
    [string]$Runtime = 'win-x64',
    [string]$Version = '1.0.0',
    [string]$Rpcs3Path
)

$ErrorActionPreference = 'Stop'

function Invoke-DotNet {
    param([Parameter(Mandatory = $true)][string[]]$Arguments)
    Write-Host "dotnet $($Arguments -join ' ')"
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet failed with exit code ${LASTEXITCODE}: dotnet $($Arguments -join ' ')"
    }
}

function Assert-ChildPath {
    param(
        [Parameter(Mandatory = $true)][string]$Parent,
        [Parameter(Mandatory = $true)][string]$Child
    )
    $parentFull = [IO.Path]::GetFullPath($Parent).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    $childFull = [IO.Path]::GetFullPath($Child)
    if (-not $childFull.StartsWith($parentFull, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to modify a path outside the artifact directory: $childFull"
    }
}

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = (Resolve-Path (Join-Path $scriptRoot '..')).Path
$artifactsDir = Join-Path $repoRoot 'artifacts'
$publishDir = Join-Path $artifactsDir "publish\R3Config-$Version-$Runtime"
$zipPath = Join-Path $artifactsDir "R3Config-$Version-$Runtime-portable.zip"
$zipHashPath = "$zipPath.sha256"

Assert-ChildPath -Parent $artifactsDir -Child $publishDir
Assert-ChildPath -Parent $artifactsDir -Child $zipPath

if (Test-Path -LiteralPath $publishDir) {
    Remove-Item -LiteralPath $publishDir -Recurse -Force
}
if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}
if (Test-Path -LiteralPath $zipHashPath) {
    Remove-Item -LiteralPath $zipHashPath -Force
}

New-Item -ItemType Directory -Path $publishDir -Force | Out-Null

Push-Location $repoRoot
try {
    Invoke-DotNet -Arguments @('restore', '.\R3Config.slnx')
    Invoke-DotNet -Arguments @('restore', '.\App\R3Config.App.csproj', '-r', $Runtime)
    Invoke-DotNet -Arguments @('build', '.\R3Config.slnx', '-c', $Configuration, '--no-restore', '--nologo')

    $testArguments = @('run', '--project', '.\Tst\R3Config.Tests.csproj', '-c', $Configuration, '--no-build', '--')
    if (-not [string]::IsNullOrWhiteSpace($Rpcs3Path)) {
        $testArguments += $Rpcs3Path
    }
    Invoke-DotNet -Arguments $testArguments

    Invoke-DotNet -Arguments @(
        'publish', '.\App\R3Config.App.csproj',
        '-c', $Configuration,
        '-r', $Runtime,
        '--self-contained', 'true',
        '--no-restore',
        '-p:PublishSingleFile=true',
        '-p:IncludeNativeLibrariesForSelfExtract=true',
        '-p:PublishReadyToRun=false',
        '-p:DebugType=embedded',
        '-o', $publishDir
    )

    $manifestPath = Join-Path $publishDir 'SHA256SUMS.txt'
    $manifestLines = Get-ChildItem -LiteralPath $publishDir -Recurse -File |
        Where-Object { $_.FullName -ne $manifestPath } |
        Sort-Object FullName |
        ForEach-Object {
            $relative = $_.FullName.Substring($publishDir.TrimEnd('\').Length + 1).Replace('\', '/')
            $hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $_.FullName).Hash.ToLowerInvariant()
            "$hash  $relative"
        }
    Set-Content -LiteralPath $manifestPath -Value $manifestLines -Encoding ascii

    & (Join-Path $scriptRoot 'Verify-PortableRelease.ps1') -Path $publishDir -Version $Version
    if ($LASTEXITCODE -ne 0) {
        throw "Portable release verification failed with exit code $LASTEXITCODE."
    }

    Compress-Archive -Path (Join-Path $publishDir '*') -DestinationPath $zipPath -Force
    $zipHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $zipPath).Hash.ToLowerInvariant()
    Set-Content -LiteralPath $zipHashPath -Value "$zipHash  $([IO.Path]::GetFileName($zipPath))" -Encoding ascii

    Write-Host "Portable ZIP created: $zipPath"
    Write-Host "Portable ZIP SHA256: $zipHash"
}
finally {
    Pop-Location
}
