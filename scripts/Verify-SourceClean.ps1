param(
    [string]$Path = '.',
    [switch]$StrictTree
)

$ErrorActionPreference = 'Stop'

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
