[CmdletBinding()]
param()

$root = Split-Path -Parent $PSScriptRoot
dotnet run `
    --project (Join-Path $PSScriptRoot 'NameChk.csproj') `
    -c Release `
    -- $root
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
