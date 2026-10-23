function Get-R3PinnedSdkVersion {
    param([Parameter(Mandatory = $true)][string]$RepositoryRoot)

    $root = [IO.Path]::GetFullPath($RepositoryRoot)
    $globalJsonPath = Join-Path $root 'global.json'
    if (-not (Test-Path -LiteralPath $globalJsonPath -PathType Leaf)) {
        throw "Repository toolchain pin is missing: $globalJsonPath"
    }

    try {
        $globalJson = Get-Content -Raw -LiteralPath $globalJsonPath | ConvertFrom-Json
    }
    catch {
        throw "Repository toolchain pin is not valid JSON: $globalJsonPath"
    }

    $version = [string]$globalJson.sdk.version
    $rollForward = [string]$globalJson.sdk.rollForward

    if ([string]::IsNullOrWhiteSpace($version)) {
        throw 'global.json must declare sdk.version.'
    }

    if ($rollForward -cne 'disable') {
        throw "global.json must set sdk.rollForward to 'disable'; actual='$rollForward'."
    }

    return $version
}

function Assert-R3WorkloadFreeProjectGraph {
    param([Parameter(Mandatory = $true)][string]$RepositoryRoot)

    $root = [IO.Path]::GetFullPath($RepositoryRoot)
    $projects = @(
        Get-ChildItem -LiteralPath $root -Filter '*.csproj' -File -Recurse |
            Where-Object {
                $_.FullName -notmatch '[\\/](?:bin|obj)[\\/]'
            } |
            Sort-Object FullName
    )

    foreach ($project in $projects) {
        $text = [IO.File]::ReadAllText($project.FullName)
        $relative = $project.FullName.Substring($root.Length).TrimStart([char[]]@('\', '/'))

        $usesWorkloadSdk =
            $text -match '(?i)Microsoft\.NET\.Sdk\.(?:Maui|BlazorWebAssembly)' -or
            $text -match '(?i)<UseMaui>\s*true\s*</UseMaui>' -or
            $text -match '(?i)net\d+(?:\.\d+)?-(?:android|ios|maccatalyst|tvos|browser|wasm)(?:[-.]|<)'

        if ($usesWorkloadSdk) {
            throw "Repository workload-free build contract violated by: $relative"
        }
    }
}

function Get-R3MsBuildDirectoryPropertyValue {
    param([Parameter(Mandatory = $true)][string]$Path)

    $fullPath = [IO.Path]::GetFullPath($Path)
    $separator = [string][IO.Path]::DirectorySeparatorChar
    if (-not $fullPath.EndsWith($separator)) {
        $fullPath += $separator
    }

    return $fullPath
}

function Assert-R3DotNetSdk {
    param([Parameter(Mandatory = $true)][string]$RepositoryRoot)

    $root = [IO.Path]::GetFullPath($RepositoryRoot)
    $expected = Get-R3PinnedSdkVersion -RepositoryRoot $root

    Push-Location $root
    try {
        $output = @(& dotnet --version)
        $exitCode = $LASTEXITCODE
    }
    finally {
        Pop-Location
    }

    $versions = @($output | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    if ($exitCode -ne 0 -or $versions.Count -eq 0) {
        throw "Unable to resolve the pinned .NET SDK from $root."
    }

    $actual = $versions[$versions.Count - 1].Trim()
    if ($actual -cne $expected) {
        throw "Pinned .NET SDK mismatch. Expected '$expected', actual '$actual'. Install the pinned SDK or update global.json intentionally."
    }

    Write-Host "DOTNET_SDK=$actual"
    return $actual
}

function Invoke-R3DotNet {
    param(
        [Parameter(Mandatory = $true)][string]$RepositoryRoot,
        [Parameter(Mandatory = $true)][string[]]$Arguments
    )

    $root = [IO.Path]::GetFullPath($RepositoryRoot)
    Assert-R3WorkloadFreeProjectGraph -RepositoryRoot $root

    $hadWorkloadResolver = Test-Path Env:MSBuildEnableWorkloadResolver
    $oldWorkloadResolver = if ($hadWorkloadResolver) {
        $env:MSBuildEnableWorkloadResolver
    }
    else {
        $null
    }

    Write-Host "dotnet $($Arguments -join ' ')"
    Write-Host 'DOTNET_WORKLOAD_RESOLVER=DISABLED_BY_REPOSITORY_CONTRACT'

    Push-Location $root
    try {
        $env:MSBuildEnableWorkloadResolver = 'false'
        & dotnet @Arguments
        $exitCode = $LASTEXITCODE
    }
    finally {
        Pop-Location
        if ($hadWorkloadResolver) {
            $env:MSBuildEnableWorkloadResolver = $oldWorkloadResolver
        }
        else {
            Remove-Item Env:MSBuildEnableWorkloadResolver -ErrorAction SilentlyContinue
        }
    }

    if ($exitCode -ne 0) {
        throw "dotnet failed with exit code ${exitCode}: dotnet $($Arguments -join ' ')"
    }
}

function Invoke-R3PowerShellFile {
    param(
        [Parameter(Mandatory = $true)][string]$ScriptPath,
        [string[]]$Arguments = @()
    )

    $resolvedScript = (Resolve-Path -LiteralPath $ScriptPath -ErrorAction Stop).Path
    Write-Host "powershell.exe -NoProfile -ExecutionPolicy Bypass -File $resolvedScript $($Arguments -join ' ')"

    & powershell.exe `
        -NoProfile `
        -ExecutionPolicy Bypass `
        -File $resolvedScript `
        @Arguments

    if ($LASTEXITCODE -ne 0) {
        throw "PowerShell gate failed with exit code ${LASTEXITCODE}: $resolvedScript"
    }
}

function Invoke-R3Executable {
    param(
        [Parameter(Mandatory = $true)][string]$LiteralPath,
        [string[]]$Arguments = @(),
        [string]$WorkingDirectory
    )

    $resolved = (Resolve-Path -LiteralPath $LiteralPath -ErrorAction Stop).Path
    $work = if ([string]::IsNullOrWhiteSpace($WorkingDirectory)) {
        $null
    }
    else {
        [IO.Path]::GetFullPath($WorkingDirectory)
    }

    Write-Host "$resolved $($Arguments -join ' ')"
    if ($null -ne $work) {
        Push-Location $work
    }

    try {
        & $resolved @Arguments
        $exitCode = $LASTEXITCODE
    }
    finally {
        if ($null -ne $work) {
            Pop-Location
        }
    }

    if ($exitCode -ne 0) {
        throw "Executable failed with exit code ${exitCode}: $resolved"
    }
}
