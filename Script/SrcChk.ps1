param(
    [string]$Path = '.',
    [switch]$StrictTree
)

$ErrorActionPreference = 'Stop'

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

function Test-SourceTree {
    param(
        [Parameter(Mandatory = $true)][string]$RootPath,
        [Parameter(Mandatory = $true)][bool]$Strict
    )

    $root = (Resolve-Path -LiteralPath $RootPath).Path
    $forbiddenDirectories = @('bin', 'obj', '.vs', 'artifacts', 'publish')

    if ($Strict) {
        $forbidden = Get-ChildItem -LiteralPath $root -Recurse -Directory -Force |
            Where-Object { $forbiddenDirectories -contains $_.Name.ToLowerInvariant() } |
            Select-Object -First 1
        if ($forbidden) {
            throw "Source tree contains forbidden directory: $($forbidden.FullName)"
        }
    }

    $allFiles = Get-ChildItem -LiteralPath $root -Recurse -File -Force |
        Where-Object {
            $relative = $_.FullName.Substring($root.TrimEnd('\').Length + 1)
            -not ($relative -split '[\\/]' | Where-Object { $forbiddenDirectories -contains $_.ToLowerInvariant() })
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
    foreach ($file in $allFiles | Where-Object { $sourceExtensions -contains $_.Extension.ToLowerInvariant() }) {
        $text = Get-Content -LiteralPath $file.FullName -Raw -ErrorAction SilentlyContinue
        if ($text -match '[A-Za-z]:\\Users\\' -or $text -match '[A-Za-z]:\\source\\') {
            throw "Hardcoded local path found in: $($file.FullName)"
        }
    }
}

$resolvedPath = (Resolve-Path -LiteralPath $Path).Path
if ((Test-Path -LiteralPath $resolvedPath -PathType Leaf) -and [IO.Path]::GetExtension($resolvedPath) -ieq '.zip') {
    $tempRoot = Join-Path ([IO.Path]::GetTempPath()) ('R3ConfigSourceVerify_' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $tempRoot -Force | Out-Null
    try {
        Expand-Archive -LiteralPath $resolvedPath -DestinationPath $tempRoot -Force
        Test-SourceTree -RootPath $tempRoot -Strict $true
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
if ($StrictTree) {
    Write-Host 'Strict source tree verification passed.'
}
else {
    Write-Host 'Source verification passed; local build directories were ignored.'
}
