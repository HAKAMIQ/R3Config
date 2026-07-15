param(
    [string]$OutputDirectory,
    [string]$ArchiveName
)

$ErrorActionPreference = 'Stop'

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

    $gitCommit = (& git -C $repoRoot rev-parse HEAD 2>$null)
    if ($LASTEXITCODE -ne 0) {
        $gitCommit = 'unavailable'
    }

    $manifest = @(
        'R3Config source handoff package'
        'Generated-UTC: ' + [DateTimeOffset]::UtcNow.ToString('O')
        'Git-Commit: ' + $gitCommit
        'Includes-Working-Tree: yes'
        'File-Count: ' + $sourceFiles.Count
        'Excluded: .git, .vs, bin, obj, artifacts, publish, local user files, logs, temp files, backups, nested ZIP files'
        'Start-Here: docs/ProdAr.md'
        'Architecture-Index: docs/Arch/README.md'
        'Contract-Gate: Script/NameChk.ps1'
    )

    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $stagingRoot 'Script\SrcChk.ps1') -Path $stagingRoot -StrictTree
    if ($LASTEXITCODE -ne 0) {
        throw "Staged source verification failed with exit code $LASTEXITCODE."
    }

    New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
    if (Test-Path -LiteralPath $archivePath) {
        Remove-Item -LiteralPath $archivePath -Force
    }

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [IO.Compression.ZipFile]::CreateFromDirectory(
        $stagingRoot,
        $archivePath,
        [IO.Compression.CompressionLevel]::Optimal,
        $false)

    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $repoRoot 'Script\SrcChk.ps1') -Path $archivePath
    if ($LASTEXITCODE -ne 0) {
        throw "Source ZIP verification failed with exit code $LASTEXITCODE."
    }

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
