param(
    [string]$OutputDirectory,
    [string]$ArchiveName
)

$ErrorActionPreference = 'Stop'
$toolchain = Join-Path $PSScriptRoot 'R3Toolchain.ps1'
. $toolchain

function Test-IsLocalOnlySourceFile {
    param(
        [Parameter(Mandatory = $true)][string]$RelativePath
    )

    $localDirectories = @()
    $segments = $RelativePath -split '[\\/]'
    if ($segments | Where-Object { $localDirectories -contains $_.ToLowerInvariant() }) {
        return $true
    }

    $name = $segments[-1]
    foreach ($pattern in @('*.local.md', '*.local.json', '*.local.json.*', '.env', '.env.*', '.dev.vars', '.dev.vars.*')) {
        if ($name -like $pattern) {
            return $true
        }
    }

    return $false
}

function Test-IsExcludedSourceFile {
    param(
        [Parameter(Mandatory = $true)][string]$RelativePath,
        [Parameter(Mandatory = $true)][AllowEmptyString()][string]$Extension
    )

    $excludedDirectories = @('.git', '.vs', 'bin', 'obj', 'artifacts', 'publish')
    $segments = $RelativePath -split '[\\/]'
    if ($segments | Where-Object { $excludedDirectories -contains $_.ToLowerInvariant() }) {
        return $true
    }

    if (Test-IsLocalOnlySourceFile -RelativePath $RelativePath) {
        return $true
    }

    $normalized = $RelativePath.Replace('\', '/')
    if ($Extension -ieq '.zip' -and
        $normalized -ieq 'App/Data/ncl.zip') {
        return $false
    }

    $excludedExtensions = @('.user', '.suo', '.rsuser', '.log', '.tmp', '.bak', '.zip')
    return $excludedExtensions -contains $Extension.ToLowerInvariant()
}

function Assert-SafeArtifactPath {
    param(
        [Parameter(Mandatory = $true)][string]$CandidatePath,
        [Parameter(Mandatory = $true)][string]$RepositoryRoot
    )

    $artifactRoot = [IO.Path]::GetFullPath((Join-Path $RepositoryRoot 'artifacts'))
    $candidate = [IO.Path]::GetFullPath($CandidatePath)
    $prefix = $artifactRoot.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if (-not $candidate.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "The default source export may write only below the repository artifacts directory: $candidate"
    }
}

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = (Resolve-Path (Join-Path $scriptRoot '..')).Path

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repoRoot 'artifacts\source'
}
elseif (-not [IO.Path]::IsPathRooted($OutputDirectory)) {
    $OutputDirectory = Join-Path $repoRoot $OutputDirectory
}

$outputRoot = [IO.Path]::GetFullPath($OutputDirectory)
Assert-SafeArtifactPath -CandidatePath $outputRoot -RepositoryRoot $repoRoot

if ([string]::IsNullOrWhiteSpace($ArchiveName)) {
    $ArchiveName = 'R3Config-Source-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.zip'
}
elseif ([IO.Path]::GetFileName($ArchiveName) -ne $ArchiveName) {
    throw 'ArchiveName must be a file name without directory components.'
}

if ([IO.Path]::GetExtension($ArchiveName) -ine '.zip') {
    throw 'ArchiveName must use the .zip extension.'
}

$archivePath = [IO.Path]::GetFullPath((Join-Path $outputRoot $ArchiveName))
Assert-SafeArtifactPath -CandidatePath $archivePath -RepositoryRoot $repoRoot

$tempBase = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$stagingRoot = Join-Path $tempBase ('R3ConfigSourceExport_' + [Guid]::NewGuid().ToString('N'))

New-Item -ItemType Directory -Path $stagingRoot -Force | Out-Null
try {
    $repoPrefixLength = $repoRoot.TrimEnd([IO.Path]::DirectorySeparatorChar).Length + 1
    $sourceFiles = Get-ChildItem -LiteralPath $repoRoot -Recurse -File -Force |
        Where-Object {
            $relativePath = $_.FullName.Substring($repoPrefixLength)
            -not (Test-IsExcludedSourceFile -RelativePath $relativePath -Extension $_.Extension)
        } |
        Sort-Object FullName

    foreach ($file in $sourceFiles) {
        $relativePath = $file.FullName.Substring($repoPrefixLength)
        $destinationPath = Join-Path $stagingRoot $relativePath
        $destinationDirectory = Split-Path -Parent $destinationPath
        New-Item -ItemType Directory -Path $destinationDirectory -Force | Out-Null
        Copy-Item -LiteralPath $file.FullName -Destination $destinationPath -Force
    }

    $gitCommit = @(& git -C $repoRoot rev-parse HEAD 2>$null)
    if ($LASTEXITCODE -ne 0 -or $gitCommit.Count -eq 0) {
        $gitCommit = 'unavailable'
    }
    else {
        $gitCommit = $gitCommit[0].Trim()
    }

    $gitBranch = @(& git -C $repoRoot branch --show-current 2>$null)
    if ($LASTEXITCODE -ne 0) {
        $gitBranch = 'unavailable'
    }
    elseif ($gitBranch.Count -eq 0 -or [string]::IsNullOrWhiteSpace($gitBranch[0])) {
        $gitBranch = 'detached'
    }
    else {
        $gitBranch = $gitBranch[0].Trim()
    }

    $gitStatusLines = @(& git -C $repoRoot status --porcelain=v1 --untracked-files=all 2>$null)
    if ($LASTEXITCODE -ne 0) {
        $workingTree = 'unavailable'
        $workingTreeEntryCount = 'unavailable'
    }
    else {
        $workingTreeEntryCount = $gitStatusLines.Count
        $workingTree = if ($gitStatusLines.Count -eq 0) { 'clean' } else { 'modified' }
    }

    $manifest = @(
        'R3Config source handoff package'
        'Generated-UTC: ' + [DateTimeOffset]::UtcNow.ToString('O')
        'Git-Commit: ' + $gitCommit
        'Git-Branch: ' + $gitBranch
        'Working-Tree: ' + $workingTree
        'Working-Tree-Entry-Count: ' + $workingTreeEntryCount
        'Includes-Working-Tree: yes'
        'File-Count: ' + $sourceFiles.Count
        'Excluded: .git, .vs, bin, obj, artifacts, publish, local user files, local user files (*.local.*), .env/.dev.vars, logs, temp files, backups, ZIP files except pinned App/Data/ncl.zip'
        'Start-Here: docs/ProdAr.md'
        'Developer-Guide: docs/Arch/README.md'
        'Architecture-Index: docs/Arch/README.md'
        'Contract-Gate: Script/NameChk.ps1'
        'Source-Gate: Script/SrcChk.ps1 -StrictTree runs before archive creation'
    )
    $handoffText = ($manifest -join "`n") + "`n"
    [IO.File]::WriteAllText(
        $handoffPath,
        $handoffText,
        [Text.UTF8Encoding]::new($false))

    $stagingPrefix = $stagingRoot.TrimEnd([IO.Path]::DirectorySeparatorChar).Length + 1
    $localLeak = Get-ChildItem -LiteralPath $stagingRoot -Recurse -File -Force |
        Where-Object { Test-IsLocalOnlySourceFile -RelativePath $_.FullName.Substring($stagingPrefix) } |
        Select-Object -First 1
    if ($null -ne $localLeak) {
        throw "Source package contains a local-only settings file: $($localLeak.FullName.Substring($stagingPrefix))"
    }

    Invoke-R3PowerShellFile `
        -ScriptPath (Join-Path $stagingRoot 'Script\SrcChk.ps1') `
        -Arguments @('-Path', $stagingRoot, '-StrictTree')

    New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
    if (Test-Path -LiteralPath $archivePath) {
        Remove-Item -LiteralPath $archivePath -Force
    }

    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::Open(
        $archivePath,
        [IO.Compression.ZipArchiveMode]::Create)
    try {
        $stagingPrefixLength = $stagingRoot.TrimEnd([IO.Path]::DirectorySeparatorChar).Length + 1
        $stagedFiles = Get-ChildItem -LiteralPath $stagingRoot -Recurse -File -Force |
            Sort-Object FullName
        foreach ($file in $stagedFiles) {
            $relativePath = $file.FullName.Substring($stagingPrefixLength)
            $entryName = $relativePath.Replace('\', '/')
            [IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
                $archive,
                $file.FullName,
                $entryName,
                [IO.Compression.CompressionLevel]::Optimal) | Out-Null
        }
    }
    finally {
        $archive.Dispose()
    }

    Invoke-R3PowerShellFile `
        -ScriptPath (Join-Path $repoRoot 'Script\SrcChk.ps1') `
        -Arguments @('-Path', $archivePath)

    $hash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
    $hashPath = $archivePath + '.sha256'
    Set-Content -LiteralPath $hashPath -Value "$hash  $ArchiveName" -Encoding ASCII

    Write-Host "Source package: $archivePath"
    Write-Host "SHA-256: $hash"
    Write-Host "Checksum file: $hashPath"
}
finally {
    if (Test-Path -LiteralPath $stagingRoot) {
        $resolvedStaging = [IO.Path]::GetFullPath($stagingRoot)
        $tempPrefix = $tempBase.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
        if (-not $resolvedStaging.StartsWith($tempPrefix, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing to remove a staging path outside the system temp directory: $resolvedStaging"
        }

        Remove-Item -LiteralPath $resolvedStaging -Recurse -Force
    }
}
