param(
    [string]$Configuration = 'Release',
    [string]$Rpcs3Path,
    [switch]$LaunchApp
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

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = (Resolve-Path (Join-Path $scriptRoot '..')).Path

Push-Location $repoRoot
try {
    Invoke-DotNet -Arguments @('restore', '.\R3Config.slnx')
    Invoke-DotNet -Arguments @('build', '.\R3Config.slnx', '-c', $Configuration, '--no-restore', '--nologo')

    $testArguments = @('run', '--project', '.\Tst\R3Config.Tests.csproj', '-c', $Configuration, '--no-build', '--')
    if (-not [string]::IsNullOrWhiteSpace($Rpcs3Path)) {
        $testArguments += $Rpcs3Path
    }
    Invoke-DotNet -Arguments $testArguments

    if ($LaunchApp) {
        Write-Host 'Launching R3Config for manual QA. Close the window to finish.'
        Invoke-DotNet -Arguments @('run', '--project', '.\App\R3Config.App.csproj', '-c', $Configuration, '--no-build')
    }
}
finally {
    Pop-Location
}
