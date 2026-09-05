param(
    [string]$Configuration = 'Release',
    [string]$Runtime = 'win-x64',
    [string]$Version = '0.1.0-preview',
    [string]$Rpcs3Path,
    [string]$SigningCertificateThumbprint = $env:R3CONFIG_SIGNING_CERT_THUMBPRINT,
    [string]$TimestampServer = 'http://timestamp.digicert.com',
    [string]$SignToolPath
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($Rpcs3Path)) {
    throw 'Final portable publishing requires -Rpcs3Path so the read-only real-install contract is executed.'
}

$signingEnabled = -not [string]::IsNullOrWhiteSpace($SigningCertificateThumbprint)

if ($signingEnabled) {
    if ([string]::IsNullOrWhiteSpace($TimestampServer)) {
        throw 'Signed portable publishing requires an RFC 3161 timestamp server.'
    }

    $timestampUri = $null
    if (-not [Uri]::TryCreate($TimestampServer, [UriKind]::Absolute, [ref]$timestampUri) -or
        @([Uri]::UriSchemeHttp, [Uri]::UriSchemeHttps) -notcontains $timestampUri.Scheme) {
        throw 'The RFC 3161 timestamp server must be an absolute HTTP or HTTPS URL.'
    }
}

$resolvedRpcs3Path = if (Test-Path -LiteralPath $Rpcs3Path -PathType Container) {
    Join-Path $Rpcs3Path 'rpcs3.exe'
}
else {
    $Rpcs3Path
}

if (-not (Test-Path -LiteralPath $resolvedRpcs3Path -PathType Leaf)) {
    throw "RPCS3 executable was not found: $resolvedRpcs3Path"
}

function Invoke-DotNet {
    param([Parameter(Mandatory = $true)][string[]]$Arguments)
    Write-Host "dotnet $($Arguments -join ' ')"
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet failed with exit code ${LASTEXITCODE}: dotnet $($Arguments -join ' ')"
    }
}

function Get-NormalizedThumbprint {
    param([Parameter(Mandatory = $true)][string]$Thumbprint)

    return ($Thumbprint -replace '[^0-9A-Fa-f]', '').ToUpperInvariant()
}

function Get-CodeSigningCertificate {
    param([Parameter(Mandatory = $true)][string]$Thumbprint)

    $normalized = Get-NormalizedThumbprint -Thumbprint $Thumbprint
    foreach ($store in @(
        @{ Path = 'Cert:\CurrentUser\My'; Machine = $false },
        @{ Path = 'Cert:\LocalMachine\My'; Machine = $true }
    )) {
        $certificate = Get-ChildItem -Path $store.Path -CodeSigningCert -ErrorAction SilentlyContinue |
            Where-Object {
                (Get-NormalizedThumbprint -Thumbprint $_.Thumbprint) -eq $normalized -and
                $_.HasPrivateKey -and
                $_.NotBefore -le (Get-Date) -and
                $_.NotAfter -gt (Get-Date)
            } |
            Select-Object -First 1

        if ($certificate) {
            return [PSCustomObject]@{
                Certificate = $certificate
                MachineStore = [bool]$store.Machine
            }
        }
    }

    throw "A valid code-signing certificate with private key was not found for thumbprint: $normalized"
}


function Import-PkcsAssembly {
    if ('Security.Cryptography.Pkcs.SignedCms' -as [type]) {
        return
    }

    foreach ($assemblyName in @(
        'System.Security.Cryptography.Pkcs',
        'System.Security'
    )) {
        try {
            Add-Type -AssemblyName $assemblyName -ErrorAction Stop
        }
        catch {
        }

        if ('Security.Cryptography.Pkcs.SignedCms' -as [type]) {
            return
        }
    }

    throw 'System.Security.Cryptography.Pkcs is unavailable. Install a supported Windows PowerShell/.NET runtime before publishing.'
}

function Resolve-SignTool {
    param([string]$ExplicitPath)

    if (-not [string]::IsNullOrWhiteSpace($ExplicitPath)) {
        $resolved = (Resolve-Path -LiteralPath $ExplicitPath -ErrorAction Stop).Path
        if (-not (Test-Path -LiteralPath $resolved -PathType Leaf)) {
            throw "SignTool was not found: $resolved"
        }
        return $resolved
    }

    $command = Get-Command 'signtool.exe' -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    $kitsRoot = ${env:ProgramFiles(x86)}
    if (-not [string]::IsNullOrWhiteSpace($kitsRoot)) {
        $candidate = Get-ChildItem -LiteralPath (Join-Path $kitsRoot 'Windows Kits\10\bin') `
                -Filter 'signtool.exe' -Recurse -File -ErrorAction SilentlyContinue |
            Where-Object { $_.DirectoryName -match '[\\/]x64$' } |
            Sort-Object FullName -Descending |
            Select-Object -First 1
        if ($candidate) {
            return $candidate.FullName
        }
    }

    throw 'signtool.exe was not found. Install the Windows 10/11 SDK or pass -SignToolPath.'
}

function New-DetachedManifestSignature {
    param(
        [Parameter(Mandatory = $true)][string]$ManifestPath,
        [Parameter(Mandatory = $true)][string]$SignaturePath,
        [Parameter(Mandatory = $true)][Security.Cryptography.X509Certificates.X509Certificate2]$Certificate
    )

    Import-PkcsAssembly
    $content = [IO.File]::ReadAllBytes($ManifestPath)
    $contentInfo = [Security.Cryptography.Pkcs.ContentInfo]::new($content)
    $cms = [Security.Cryptography.Pkcs.SignedCms]::new($contentInfo, $true)
    $signer = [Security.Cryptography.Pkcs.CmsSigner]::new($Certificate)
    $signer.IncludeOption = [Security.Cryptography.X509Certificates.X509IncludeOption]::EndCertOnly
    $signer.DigestAlgorithm = [Security.Cryptography.Oid]::new('2.16.840.1.101.3.4.2.1')
    $cms.ComputeSignature($signer)
    [IO.File]::WriteAllBytes($SignaturePath, $cms.Encode())
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

function Assert-ChildPath {
    param(
        [Parameter(Mandatory = $true)][string]$Parent,
        [Parameter(Mandatory = $true)][string]$Child
    )
    $parentFull = [IO.Path]::GetFullPath($Parent).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    $childFull = [IO.Path]::GetFullPath($Child)
    if (-not $childFull.StartsWith($parentFull, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to modify a path outside the artifact directory: $childFull"
    }
}

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = (Resolve-Path (Join-Path $scriptRoot '..')).Path
$artifactsDir = Join-Path $repoRoot 'artifacts'
$publishDir = Join-Path $artifactsDir "publish\R3Config-$Version-$Runtime"
$zipPath = Join-Path $artifactsDir "R3Config-$Version-$Runtime-portable.zip"
$zipHashPath = "$zipPath.sha256"
$normalizedSigningThumbprint = if ($signingEnabled) {
    Get-NormalizedThumbprint -Thumbprint $SigningCertificateThumbprint
}
else {
    ''
}

$preservedLockFiles = @(
    (Join-Path $repoRoot 'Core\packages.lock.json'),
    (Join-Path $repoRoot 'Script\LangPack\packages.lock.json')
)

$lockFileSnapshots = @{}

foreach ($lockFile in $preservedLockFiles) {
    if (Test-Path -LiteralPath $lockFile -PathType Leaf) {
        $lockFileSnapshots[$lockFile] = [IO.File]::ReadAllBytes($lockFile)
    }
}

Assert-ChildPath -Parent $artifactsDir -Child $publishDir
Assert-ChildPath -Parent $artifactsDir -Child $zipPath

if (Test-Path -LiteralPath $publishDir) {
    Remove-Item -LiteralPath $publishDir -Recurse -Force
}
if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}
if (Test-Path -LiteralPath $zipHashPath) {
    Remove-Item -LiteralPath $zipHashPath -Force
}

New-Item -ItemType Directory -Path $publishDir -Force | Out-Null

Push-Location $repoRoot
try {
    Invoke-DotNet -Arguments @('restore', '.\R3Cfg.slnx')
    Invoke-DotNet -Arguments @('restore', '.\App\R3App.csproj', '-r', $Runtime)
    Invoke-DotNet -Arguments @('build', '.\R3Cfg.slnx', '-c', $Configuration, '--no-restore', '--nologo')

    $testArguments = @(
        'run', '--project', '.\Tst\R3Test.csproj',
        '-c', $Configuration, '--no-build', '--',
        $resolvedRpcs3Path
    )
    Invoke-DotNet -Arguments $testArguments

    Invoke-DotNet -Arguments @(
        'publish', '.\App\R3App.csproj',
        '-c', $Configuration,
        '-r', $Runtime,
        '--self-contained', 'true',
        '--no-restore',
        '-p:PublishSingleFile=true',
        '-p:IncludeNativeLibrariesForSelfExtract=true',
        '-p:PublishReadyToRun=false',
        '-p:DebugType=embedded',
        '-o', $publishDir
    )

    $publishedExe = Join-Path $publishDir 'R3Config.exe'
    $certificate = $null

    if ($signingEnabled) {
        $certificateInfo = Get-CodeSigningCertificate -Thumbprint $normalizedSigningThumbprint
        $certificate = $certificateInfo.Certificate
        $resolvedSignTool = Resolve-SignTool -ExplicitPath $SignToolPath
        Write-Host "Signing with: $resolvedSignTool"

        $signArguments = @(
            'sign',
            '/sha1', $normalizedSigningThumbprint,
            '/s', 'My',
            '/fd', 'SHA256',
            '/tr', $TimestampServer,
            '/td', 'SHA256',
            '/v'
        )

        if ($certificateInfo.MachineStore) {
            $signArguments += '/sm'
        }

        $signArguments += $publishedExe
        & $resolvedSignTool @signArguments

        if ($LASTEXITCODE -ne 0) {
            throw "SignTool failed with exit code $LASTEXITCODE."
        }

        $authenticode = Get-AuthenticodeSignature -FilePath $publishedExe

        if ($authenticode.Status -ne [System.Management.Automation.SignatureStatus]::Valid -or
            -not $authenticode.SignerCertificate) {
            throw "Authenticode verification failed after SignTool: $($authenticode.Status) $($authenticode.StatusMessage)"
        }

        $actualSigner = Get-NormalizedThumbprint -Thumbprint $authenticode.SignerCertificate.Thumbprint

        if ($actualSigner -ne $normalizedSigningThumbprint) {
            throw "Signed executable thumbprint '$actualSigner' does not match '$normalizedSigningThumbprint'."
        }
    }
    else {
        Write-Host 'Signing skipped: unsigned Preview package.'
    }

    $manifestPath = Join-Path $publishDir 'SHA256SUMS.txt'
    $manifestSignaturePath = Join-Path $publishDir 'SHA256SUMS.p7s'
    $manifestLines = Get-ChildItem -LiteralPath $publishDir -Recurse -File |
        Where-Object {
            $_.FullName -ne $manifestPath -and
            $_.FullName -ne $manifestSignaturePath
        } |
        Sort-Object FullName |
        ForEach-Object {
            $relative = $_.FullName.Substring($publishDir.TrimEnd('\').Length + 1).Replace('\', '/')
            $hash = Get-Sha256Hex -LiteralPath $_.FullName
            "$hash  $relative"
        }
    Set-Content -LiteralPath $manifestPath -Value $manifestLines -Encoding ascii

    if ($signingEnabled) {
        New-DetachedManifestSignature `
            -ManifestPath $manifestPath `
            -SignaturePath $manifestSignaturePath `
            -Certificate $certificate

        & (Join-Path $scriptRoot 'PortChk.ps1') `
            -Path $publishDir `
            -Version $Version `
            -ExpectedSignerThumbprint $normalizedSigningThumbprint
    }
    else {
        & (Join-Path $scriptRoot 'PortChk.ps1') `
            -Path $publishDir `
            -Version $Version
    }
    if ($LASTEXITCODE -ne 0) {
        throw "Portable release verification failed with exit code $LASTEXITCODE."
    }

    Compress-Archive -Path (Join-Path $publishDir '*') -DestinationPath $zipPath -Force
    $zipHash = Get-Sha256Hex -LiteralPath $zipPath
    Set-Content -LiteralPath $zipHashPath -Value "$zipHash  $([IO.Path]::GetFileName($zipPath))" -Encoding ascii

    Write-Host "Portable ZIP created: $zipPath"
    Write-Host "Portable ZIP SHA256: $zipHash"
}
finally {
    Pop-Location

    foreach ($entry in $lockFileSnapshots.GetEnumerator()) {
        [IO.File]::WriteAllBytes(
            [string]$entry.Key,
            [byte[]]$entry.Value
        )
    }
}
