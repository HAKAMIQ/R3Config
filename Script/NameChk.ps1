[CmdletBinding()]
param()

$root = Split-Path -Parent $PSScriptRoot
$proj = Join-Path $PSScriptRoot 'NameChk.csproj'
$base = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$tmp = Join-Path $base ('R3ContractChk_' + [Guid]::NewGuid().ToString('N'))
$code = 0

try {
    dotnet run `
        --project $proj `
        -c Release `
        --artifacts-path $tmp `
        -- $root
    $code = $LASTEXITCODE
}
finally {
    if (Test-Path -LiteralPath $tmp) {
        $path = [IO.Path]::GetFullPath($tmp)
        $prefix = $base.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
        if (-not $path.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing to remove a contract-gate path outside the system temp directory: $path"
        }

        Remove-Item -LiteralPath $path -Recurse -Force
    }
}

if ($code -ne 0) { exit $code }
