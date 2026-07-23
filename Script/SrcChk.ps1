param(
    [string]$Path,
    [switch]$StrictTree
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($Path)) {
    $Path = Join-Path $PSScriptRoot '..'
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


function Test-TextEncodingContracts {
    param([Parameter(Mandatory = $true)][object[]]$Files)

    $crlfExtensions = @('.cs', '.xaml', '.csproj', '.slnx', '.ps1', '.cmd')
    $lfExtensions = @('.json', '.yml', '.yaml', '.md')
    $utf8 = [Text.UTF8Encoding]::new($false, $true)

    foreach ($file in $Files) {
        $extension = $file.Extension.ToLowerInvariant()
        if ($crlfExtensions -notcontains $extension -and $lfExtensions -notcontains $extension) {
            continue
        }

        $bytes = [IO.File]::ReadAllBytes($file.FullName)
        if ($bytes.Length -ge 3 -and
            $bytes[0] -eq 0xEF -and
            $bytes[1] -eq 0xBB -and
            $bytes[2] -eq 0xBF) {
            throw "UTF-8 BOM is not allowed: $($file.FullName)"
        }

        try {
            $text = $utf8.GetString($bytes)
        }
        catch {
            throw "Source text is not valid UTF-8: $($file.FullName)"
        }

        $withoutCrlf = $text.Replace("`r`n", '')
        $hasLoneLf = $withoutCrlf.IndexOf("`n", [StringComparison]::Ordinal) -ge 0
        $hasLoneCr = $withoutCrlf.IndexOf("`r", [StringComparison]::Ordinal) -ge 0
        $hasCrlf = $text.IndexOf("`r`n", [StringComparison]::Ordinal) -ge 0

        if ($crlfExtensions -contains $extension) {
            if ($hasLoneLf -or $hasLoneCr) {
                throw "Source text must use CRLF only: $($file.FullName)"
            }
        }
        elseif ($hasCrlf -or $hasLoneCr) {
            throw "Source text must use LF only: $($file.FullName)"
        }
    }
}

function Test-SourceTree {
    param(
        [Parameter(Mandatory = $true)][string]$RootPath,
        [Parameter(Mandatory = $true)][bool]$Strict
    )

    $root = (Resolve-Path -LiteralPath $RootPath).Path
    $forbiddenDirectories = @('.git', '.vs', 'bin', 'obj', 'artifacts', 'publish', 'r3configbackups')
    $temporaryInspectionPattern = '^(?:Button-(?:Audit|Groups|Issues)|.+-Audit|.+-Ui-Structure)-\d{8}-\d{6}\.(?:csv|json|txt)$'

    $gitCommand = Get-Command git -ErrorAction SilentlyContinue
    $gitRootOutput = @()
    $gitRootExitCode = 1
    if ($null -ne $gitCommand) {
        $previousErrorAction = $ErrorActionPreference
        try {
            $ErrorActionPreference = 'Continue'
            $gitRootOutput = @(& $gitCommand.Source -C $root rev-parse --show-toplevel 2>$null)
            $gitRootExitCode = $LASTEXITCODE
        }
        finally {
            $ErrorActionPreference = $previousErrorAction
        }
    }

    $isRepositoryRoot = $gitRootExitCode -eq 0 -and
        $gitRootOutput.Count -gt 0 -and
        [IO.Path]::GetFullPath($gitRootOutput[0].Trim()).TrimEnd('\') -ieq $root.TrimEnd('\')

    if ($Strict -and -not $isRepositoryRoot) {
        $forbidden = Get-ChildItem -LiteralPath $root -Recurse -Directory -Force |
            Where-Object { $forbiddenDirectories -contains $_.Name.ToLowerInvariant() } |
            Select-Object -First 1
        if ($forbidden) {
            throw "Source tree contains forbidden directory: $($forbidden.FullName)"
        }

        $temporaryInspectionFile = Get-ChildItem -LiteralPath $root -Recurse -File -Force |
            Where-Object {
                $_.Name -like '*.bak-*' -or
                $_.Name -match $temporaryInspectionPattern
            } |
            Select-Object -First 1
        if ($temporaryInspectionFile) {
            throw "Source tree contains a temporary inspection file: $($temporaryInspectionFile.FullName)"
        }
    }

    if ($isRepositoryRoot) {
        $previousErrorAction = $ErrorActionPreference
        try {
            $ErrorActionPreference = 'Continue'
            $trackedPaths = @(& $gitCommand.Source -C $root -c core.quotepath=false ls-files --cached 2>$null)
            $trackedPathExitCode = $LASTEXITCODE
        }
        finally {
            $ErrorActionPreference = $previousErrorAction
        }

        if ($trackedPathExitCode -ne 0) {
            throw "git ls-files failed for source tree: $root"
        }

        $allFiles = @(
            foreach ($relativePath in $trackedPaths) {
                $fullPath = Join-Path $root ($relativePath.Replace('/', [IO.Path]::DirectorySeparatorChar))
                if (Test-Path -LiteralPath $fullPath -PathType Leaf) {
                    Get-Item -LiteralPath $fullPath -Force
                }
            }
        )
    }
    else {
        $allFiles = @(
            Get-ChildItem -LiteralPath $root -Recurse -File -Force |
                Where-Object {
                    $relative = $_.FullName.Substring($root.TrimEnd('\').Length + 1)
                    -not ($relative -split '[\\/]' | Where-Object { $forbiddenDirectories -contains $_.ToLowerInvariant() }) -and
                    $_.Name -notlike '*.bak-*' -and
                    $_.Name -notmatch $temporaryInspectionPattern
                }
        )
    }

    $nestedZip = $allFiles | Where-Object { $_.Extension -ieq '.zip' } | Select-Object -First 1
    if ($nestedZip) {
        throw "Source tree contains nested ZIP artifact: $($nestedZip.FullName)"
    }

    $obsoleteCbak = $allFiles | Where-Object {
        $_.FullName.Substring($root.TrimEnd('\').Length + 1).Replace('\', '/') -ieq 'App/Data/cbak.json'
    } | Select-Object -First 1
    if ($obsoleteCbak) {
        throw "Source tree contains obsolete App/Data/cbak.json: $($obsoleteCbak.FullName)"
    }

    $cheatPath = Join-Path $root 'App\Data\cheats.yml'
    if (Test-Path -LiteralPath $cheatPath -PathType Leaf) {
        $cheatNoticePath = Join-Path $root 'CHEATS.md'
        if (-not (Test-Path -LiteralPath $cheatNoticePath -PathType Leaf)) {
            throw 'Source tree contains App/Data/cheats.yml without CHEATS.md provenance notice.'
        }

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
    }

    $canaryPath = Join-Path $root 'App\Data\can.yml'
    if (Test-Path -LiteralPath $canaryPath -PathType Leaf) {
        $canaryNoticePath = Join-Path $root 'CANARY.md'
        if (-not (Test-Path -LiteralPath $canaryNoticePath -PathType Leaf)) {
            throw 'Source tree contains App/Data/can.yml without CANARY.md provenance notice.'
        }

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
    }

    $sourceExtensions = @('.cs', '.xaml', '.csproj', '.slnx', '.ps1', '.cmd', '.md', '.json', '.yml', '.yaml')
    Test-TextEncodingContracts -Files @($allFiles)
    foreach ($file in $allFiles | Where-Object { $sourceExtensions -contains $_.Extension.ToLowerInvariant() }) {
        $text = Get-Content -LiteralPath $file.FullName -Raw -ErrorAction SilentlyContinue
        if ($text -match '[A-Za-z]:\\Users\\' -or $text -match '[A-Za-z]:\\source\\') {
            throw "Hardcoded local path found in: $($file.FullName)"
        }
    }
}

function Test-ZipEntryNames {
    param([Parameter(Mandatory = $true)][string]$LiteralPath)

    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::OpenRead($LiteralPath)
    try {
        $invalidEntry = $archive.Entries |
            Where-Object { $_.FullName.IndexOf('\', [StringComparison]::Ordinal) -ge 0 } |
            Select-Object -First 1
        if ($invalidEntry) {
            throw "Source ZIP entry uses a backslash separator: $($invalidEntry.FullName)"
        }
    }
    finally {
        $archive.Dispose()
    }

    Write-Host 'Source ZIP entry separator verification passed.'
}

function Test-ProjectContracts {
    param([Parameter(Mandatory = $true)][string]$RootPath)

    $gate = Join-Path $RootPath 'Script\NameChk.ps1'
    if (-not (Test-Path -LiteralPath $gate -PathType Leaf)) {
        throw "Source tree is missing the architecture and contract gate: $gate"
    }

    & powershell -ExecutionPolicy Bypass -File $gate
    if ($LASTEXITCODE -ne 0) {
        throw "Architecture and contract gate failed for: $RootPath"
    }
}

$resolvedPath = (Resolve-Path -LiteralPath $Path).Path
if ((Test-Path -LiteralPath $resolvedPath -PathType Leaf) -and [IO.Path]::GetExtension($resolvedPath) -ieq '.zip') {
    Test-ZipEntryNames -LiteralPath $resolvedPath
    $tempRoot = Join-Path ([IO.Path]::GetTempPath()) ('R3ConfigSourceVerify_' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $tempRoot -Force | Out-Null
    try {
        Expand-Archive -LiteralPath $resolvedPath -DestinationPath $tempRoot -Force
        Test-SourceTree -RootPath $tempRoot -Strict $true
        Test-ProjectContracts -RootPath $tempRoot
        Write-Host "Source ZIP verification passed: $resolvedPath"
    }
    finally {
        if (Test-Path -LiteralPath $tempRoot) {
            $resolvedTemp = [IO.Path]::GetFullPath($tempRoot)
            $systemTemp = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
            if (-not $resolvedTemp.StartsWith($systemTemp, [StringComparison]::OrdinalIgnoreCase)) {
                throw "Refusing to remove a verification path outside the system temp directory: $resolvedTemp"
            }
            Remove-Item -LiteralPath $resolvedTemp -Recurse -Force
        }
    }
    return
}

Test-SourceTree -RootPath $resolvedPath -Strict ([bool]$StrictTree)
Test-ProjectContracts -RootPath $resolvedPath
if ($StrictTree) {
    Write-Host 'Strict source tree verification passed.'
}
else {
    Write-Host 'Source verification passed; local build directories were ignored.'
}
