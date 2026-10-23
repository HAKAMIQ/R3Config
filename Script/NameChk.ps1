[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$proj = Join-Path $PSScriptRoot 'NameChk.csproj'
$toolchain = Join-Path $PSScriptRoot 'R3Toolchain.ps1'
$tempBase = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$tempRoot = Join-Path $tempBase ('R3NameChk_' + [Guid]::NewGuid().ToString('N'))

. $toolchain

$outputRoot = Get-R3MsBuildDirectoryPropertyValue -Path (Join-Path $tempRoot 'bin')
$intermediateRoot = Get-R3MsBuildDirectoryPropertyValue -Path (Join-Path $tempRoot 'obj')

try {
    Assert-R3DotNetSdk -RepositoryRoot $root | Out-Null
    New-Item -ItemType Directory -Path $tempRoot -Force | Out-Null

    Invoke-R3DotNet `
        -RepositoryRoot $root `
        -Arguments @(
            'build',
            $proj,
            '-c', 'Release',
            '--nologo',
            ('-p:BaseOutputPath=' + $outputRoot),
            ('-p:BaseIntermediateOutputPath=' + $intermediateRoot)
        )

    $dlls = @(
        Get-ChildItem `
            -LiteralPath $outputRoot `
            -Filter 'NameChk.dll' `
            -Recurse `
            -File
    )

    if ($dlls.Count -ne 1) {
        throw "NameChk build must produce exactly one NameChk.dll under the isolated output root; found $($dlls.Count)."
    }

    Invoke-R3DotNet `
        -RepositoryRoot $root `
        -Arguments @(
            'exec',
            $dlls[0].FullName,
            $root
        )
}
finally {
    if (Test-Path -LiteralPath $tempRoot) {
        $resolved = [IO.Path]::GetFullPath($tempRoot)
        $prefix = $tempBase.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
        if (-not $resolved.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing to remove NameChk output outside the system temp directory: $resolved"
        }

        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}
