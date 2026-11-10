param(
    [string]$Path,
    [switch]$StrictTree
)

$ErrorActionPreference = 'Stop'
$toolchain = Join-Path $PSScriptRoot 'R3Toolchain.ps1'
. $toolchain

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

        if ($crlfExtensions -contains $extension -or
            $file.Name -ieq 'packages.lock.json') {
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
    [void](Get-R3PinnedSdkVersion -RepositoryRoot $root)
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
            $workingTreePaths = @(& $gitCommand.Source -C $root -c core.quotepath=false ls-files --cached --others --exclude-standard 2>$null)
            $workingTreePathExitCode = $LASTEXITCODE
        }
        finally {
            $ErrorActionPreference = $previousErrorAction
        }

        if ($workingTreePathExitCode -ne 0) {
            throw "git ls-files failed for source tree: $root"
        }

        $allFiles = @(
            foreach ($relativePath in @($workingTreePaths | Sort-Object -Unique)) {
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

    $nestedZip = $allFiles | Where-Object {
        if ($_.Extension -ine '.zip') {
            return $false
        }

        $relative = $_.FullName.Substring($root.TrimEnd('\').Length + 1).Replace('\', '/')
        return $relative -ine 'App/Data/ncl.zip'
    } | Select-Object -First 1
    if ($nestedZip) {
        throw "Source tree contains nested ZIP artifact: $($nestedZip.FullName)"
    }

    # Private keys, PFX/PKCS#12 secrets, local databases, and environment
    # files stay outside the repository.
    $secretExtensions = @('.pem', '.pfx', '.p12', '.p8', '.key', '.snk', '.dpapi', '.sqlite', '.sqlite3')
    $secretFile = $allFiles | Where-Object {
        $secretExtensions -contains $_.Extension.ToLowerInvariant() -or
        $_.Name -like '.dev.vars*' -or
        $_.Name -like '.env*' -or
        $_.FullName -match '[\\/]node_modules[\\/]'
    } | Select-Object -First 1
    if ($secretFile) {
        throw "Source tree contains key material, a signing secret, or local secrets: $($secretFile.FullName)"
    }

    $pemPrivateKeyPattern = '-----BEGIN (?:[A-Z0-9]+ )*PRIVATE KEY-----\s*[A-Za-z0-9+/=\r\n]{64,}'
    foreach ($file in $allFiles | Where-Object { $_.Length -le 4MB -and @('.zip', '.ico', '.png') -notcontains $_.Extension.ToLowerInvariant() }) {
        $content = [Text.Encoding]::ASCII.GetString([IO.File]::ReadAllBytes($file.FullName))
        if ($content -match $pemPrivateKeyPattern) {
            throw "Source tree contains PEM private key material: $($file.FullName)"
        }
    }

    $staleArtImport = Get-ChildItem -LiteralPath $root -Recurse -File -Force | Where-Object {
        $_.Name -match '(?:\.r3artimport\.(?:txn\.json|backup|lock)|\.r3stage)$'
    } | Select-Object -First 1
    if ($staleArtImport) {
        throw "Source tree contains an interrupted Artemis import transaction artifact: $($staleArtImport.FullName)"
    }

    $obsoleteCbak = $allFiles | Where-Object {
        $_.FullName.Substring($root.TrimEnd('\').Length + 1).Replace('\', '/') -ieq 'App/Data/cbak.json'
    } | Select-Object -First 1
    if ($obsoleteCbak) {
        throw "Source tree contains obsolete App/Data/cbak.json: $($obsoleteCbak.FullName)"
    }

    $legacyCheatPath = Join-Path $root 'App\Data\cheats.yml'
    if (Test-Path -LiteralPath $legacyCheatPath -PathType Leaf) {
        throw 'Source tree contains obsolete App/Data/cheats.yml; the documented Artemis catalog is App/Data/art.yml.'
    }

    $nclPath = Join-Path $root 'App\Data\ncl.zip'
    $nclNoticePath = Join-Path $root 'NCL.md'
    if (-not (Test-Path -LiteralPath $nclPath -PathType Leaf)) {
        throw 'Source tree is missing required pinned App/Data/ncl.zip.'
    }
    if (-not (Test-Path -LiteralPath $nclNoticePath -PathType Leaf)) {
        throw 'Source tree is missing required NCL.md bundled-corpus provenance notice.'
    }

    if (Test-Path -LiteralPath $nclPath -PathType Leaf) {

        $nclHash = Get-Sha256Hex -LiteralPath $nclPath
        $expectedNclHash = 'C4C70C6E73A225F1E15096A3AEA06DD05461F7EF0CBB4BE8B106577ABC22D8B8'
        if ($nclHash -ne $expectedNclHash) {
            throw "App/Data/ncl.zip SHA-256 drifted from the pinned bundled corpus: $nclHash"
        }

        $nclNotice = Get-Content -Raw -LiteralPath $nclNoticePath
        $requiredNclNoticeLines = @(
            'R3Config verified merged NCL corpus',
            'Source lineage: 5 records',
            'Source NCL files: 2910',
            "Bundled artifact SHA-256: $expectedNclHash",
            'Embedded provenance schema: 2',
            'Upstream ArtemisPS3 license notice: LICENSE-ArtemisPS3.txt'
        )
        foreach ($requiredLine in $requiredNclNoticeLines) {
            if ($nclNotice.IndexOf($requiredLine, [StringComparison]::Ordinal) -lt 0) {
                throw "NCL.md is missing required bundled-corpus provenance line: $requiredLine"
            }
        }

        Add-Type -AssemblyName System.IO.Compression
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        $nclArchive = [IO.Compression.ZipFile]::OpenRead($nclPath)
        try {
            $nclEntries = @($nclArchive.Entries | Where-Object {
                $_.Name.Length -gt 0 -and $_.FullName.EndsWith('.ncl', [StringComparison]::OrdinalIgnoreCase)
            })
            if ($nclEntries.Count -ne 2910) {
                throw "App/Data/ncl.zip expected 2910 NCL entries but found $($nclEntries.Count)."
            }

            if ($nclArchive.Entries.Count -ne 2912) {
                throw "App/Data/ncl.zip must contain exactly 2910 NCL entries plus LICENSE-ArtemisPS3.txt and PROVENANCE.json."
            }

            $unsafeNclEntry = $nclEntries | Where-Object {
                -not $_.FullName.StartsWith('codes/', [StringComparison]::Ordinal) -or
                $_.FullName.IndexOf('\', [StringComparison]::Ordinal) -ge 0 -or
                @($_.FullName.Split('/') | Where-Object { $_ -eq '..' }).Count -gt 0
            } | Select-Object -First 1
            if ($unsafeNclEntry) {
                throw "App/Data/ncl.zip contains an unsafe NCL entry path: $($unsafeNclEntry.FullName)"
            }

            $licenseEntry = $nclArchive.GetEntry('LICENSE-ArtemisPS3.txt')
            $provenanceEntry = $nclArchive.GetEntry('PROVENANCE.json')
            if ($null -eq $licenseEntry -or $null -eq $provenanceEntry) {
                throw 'App/Data/ncl.zip is missing its embedded license or provenance record.'
            }

            $licenseStream = $licenseEntry.Open()
            try {
                $licenseSha = [Security.Cryptography.SHA256]::Create()
                try {
                    $licenseHash = [BitConverter]::ToString($licenseSha.ComputeHash($licenseStream)).Replace('-', '')
                }
                finally {
                    $licenseSha.Dispose()
                }
            }
            finally {
                $licenseStream.Dispose()
            }
            if ($licenseHash -ne '6A3E4BA999067B1C412F1740282BC077F8EDF5F7178EA085ACB5F5CCA6FECB46') {
                throw 'App/Data/ncl.zip ArtemisPS3 license notice does not match the proven upstream license text.'
            }

            $reader = [IO.StreamReader]::new($provenanceEntry.Open(), [Text.Encoding]::UTF8, $true)
            try {
                $provenance = $reader.ReadToEnd() | ConvertFrom-Json
            }
            finally {
                $reader.Dispose()
            }

            if ($provenance.schema -ne 2 -or
                $provenance.artifactKind -ne 'R3Config NCL corpus' -or
                $provenance.nclFileCount -ne 2910 -or
                $provenance.dialect -ne 'ArtemisPs3' -or
                $provenance.generatedBy -ne 'R3Config deterministic NCL corpus builder' -or
                $provenance.licenseNotice -ne 'LICENSE-ArtemisPS3.txt') {
                throw 'App/Data/ncl.zip embedded schema-2 provenance header is invalid.'
            }

            $expectedSources = @{
                'ALDOSTOOLS_CODELIST' = @('upstreamArchive', 'A1ABC379DD6F8BB65399EACD97D147A33C5907BAFE2C1E0B468BA86A786E8CEA', 2501)
                'BUCANERO_CURRENT' = @('upstreamArchive', '8289EFDB5B5441A579C1ECAC8E608852FAB4D225A92728B5EB8D6227ED89EBA2', 2540)
                'DNAWRKSHP_HISTORICAL' = @('historicalUpstreamArchive', '8AC628D238D0501D699552EECF40C836D417DCADD66419699DC2C56BBE6D01A6', 1287)
                'R3CONFIG_CURRENT' = @('legacyCarryForward', '80AA918A4F98A7491A0D684456D7620486296C54B396FEA0C89D117CBF5DD17D', 2497)
                'AR_SITE_CODELIST_2026' = @('manifestDownloadArchive', '734896CB7DD1EEE2B9E6720E6E75D9D852626F213C8676B03914257DF8555DC3', 2495)
            }
            $sources = @($provenance.sources)
            if ($sources.Count -ne $expectedSources.Count) {
                throw "App/Data/ncl.zip provenance expected $($expectedSources.Count) source records but found $($sources.Count)."
            }

            $ids = @($sources | ForEach-Object { [string]$_.id })
            if (@($ids | Sort-Object -Unique).Count -ne $ids.Count) {
                throw 'App/Data/ncl.zip provenance contains duplicate source IDs.'
            }

            foreach ($source in $sources) {
                if ([string]::IsNullOrWhiteSpace([string]$source.id) -or
                    [string]::IsNullOrWhiteSpace([string]$source.kind) -or
                    [string]::IsNullOrWhiteSpace([string]$source.sha256) -or
                    [int]$source.nclFileCount -le 0) {
                    throw 'App/Data/ncl.zip provenance contains an incomplete source record.'
                }

                if (-not $expectedSources.ContainsKey([string]$source.id)) {
                    throw "App/Data/ncl.zip provenance contains an unexpected source ID: $($source.id)"
                }

                $expected = $expectedSources[[string]$source.id]
                if ([string]$source.kind -ne $expected[0] -or
                    [string]$source.sha256 -ne $expected[1] -or
                    [int]$source.nclFileCount -ne [int]$expected[2]) {
                    throw "App/Data/ncl.zip provenance mismatch for source ID: $($source.id)"
                }
            }
        }
        finally {
            $nclArchive.Dispose()
        }
    }

    $artemisPath = Join-Path $root 'App\Data\art.yml'
    if (Test-Path -LiteralPath $artemisPath -PathType Leaf) {
        $artemisNoticePath = Join-Path $root 'CHEATS.md'
        if (-not (Test-Path -LiteralPath $artemisNoticePath -PathType Leaf)) {
            throw 'Source tree contains App/Data/art.yml without CHEATS.md provenance notice.'
        }

        $artemisHash = Get-Sha256Hex -LiteralPath $artemisPath
        $artemisText = Get-Content -Raw -LiteralPath $artemisPath
        $artemisNotice = Get-Content -Raw -LiteralPath $artemisNoticePath
        $artemisDependencyPath = Join-Path $root 'App\Data\art_dep.json'
        if (-not (Test-Path -LiteralPath $artemisDependencyPath -PathType Leaf)) {
            throw 'Source tree contains App/Data/art.yml without required App/Data/art_dep.json dependency metadata.'
        }

        $artemisDependencyHash = Get-Sha256Hex -LiteralPath $artemisDependencyPath
        $artemisDependency = Get-Content -Raw -LiteralPath $artemisDependencyPath | ConvertFrom-Json
        $artemisDependencyRules = @($artemisDependency.rules)
        if ($artemisDependency.schemaVersion -ne '1' -or
            $artemisDependency.sourceIdentity -ne 'Artemis-Patch-Collection-RPCS3' -or
            $artemisDependency.sourceRevision -ne 'fd267a65759a2f1dcae31e362d318de7a6e16594' -or
            $artemisDependency.artemisSha256 -ne $artemisHash -or
            $artemisDependencyRules.Count -ne 23) {
            throw 'App/Data/art_dep.json provenance, corpus hash, schema, or reviewed rule count is invalid.'
        }
        foreach ($rule in $artemisDependencyRules) {
            if ([string]::IsNullOrWhiteSpace($rule.runKey) -or
                [string]::IsNullOrWhiteSpace($rule.serial) -or
                [string]::IsNullOrWhiteSpace($rule.version) -or
                [string]::IsNullOrWhiteSpace($rule.patchTitle) -or
                [string]::IsNullOrWhiteSpace($rule.evidence) -or
                @('All', 'Any') -notcontains $rule.mode -or
                @($rule.requires).Count -eq 0 -or
                @($rule.requires | Where-Object { [string]::IsNullOrWhiteSpace($_) }).Count -ne 0) {
                throw "App/Data/art_dep.json contains an invalid reviewed dependency rule for '$($rule.patchTitle)'."
            }
        }
        $acceptedEntries = [regex]::Matches(
            $artemisText,
            '(?m)^(?:(?:[A-Z0-9]{9})-)?(?:PPU|SPU|PRX|OVL)-[^:\r\n]+:\s*$').Count
        $requiredArtemisNoticeLines = @(
            'Canonical cheat source: Artemis Patch Collection for RPCS3 by ChiDreams',
            'Source URL: https://github.com/chidreams/Artemis-Patch-Collection-RPCS3',
            'Source artifact: Artemis-Patch-Collection-RPCS3-fd267a6-r3fix1.zip',
            'Source reference: pinned ChiDreams commit fd267a65759a2f1dcae31e362d318de7a6e16594 via Artemis-Patch-Collection-RPCS3-fd267a6-r3fix1.zip; 2 reviewed source corrections',
            'Source archive SHA-256: cefd5b7475a67d3c36f165935c60f611b8ac6e10a095f4524e53c86fe7b430de',
            'Source patch files: 237',
            'Candidate entries: 1956',
            'Accepted entries: 1922',
            'Rejected records: 34',
            "Generated art.yml SHA-256: $artemisHash",
            'Dependency metadata rules: 23',
            'Dependency source revision: fd267a65759a2f1dcae31e362d318de7a6e16594',
            "Generated art_dep.json SHA-256: $artemisDependencyHash",
            'Manager reference artifact: ARTEMIS-RPCS3-Cheat-Manager-2.00.zip',
            'Manager reference archive SHA-256: caaab46ad39c671470f1d2593958da8c2b9111348660cbd3f0f5a53e93c41604',
            'License: MIT'
        )
        foreach ($requiredLine in $requiredArtemisNoticeLines) {
            if ($artemisNotice.IndexOf($requiredLine, [StringComparison]::Ordinal) -lt 0) {
                throw "CHEATS.md is missing required Artemis collection provenance line: $requiredLine"
            }
        }

        if ($acceptedEntries -ne 1922) {
            throw "App/Data/art.yml expected 1922 accepted Artemis definitions but found $acceptedEntries."
        }
        if (-not $artemisText.StartsWith("Version: 1.2`n", [StringComparison]::Ordinal)) {
            throw 'App/Data/art.yml must begin with Version: 1.2.'
        }
        if ($artemisText -match '(?m)^Anchors(?:_\d+)?:') {
            throw 'App/Data/art.yml must contain expanded definitions without a top-level Anchors mapping.'
        }

        foreach ($obsoleteSidecar in @('artm.json', 'artb.json', 'artr.json.gz')) {
            $obsoletePath = Join-Path $root ("App\Data\" + $obsoleteSidecar)
            if (Test-Path -LiteralPath $obsoletePath -PathType Leaf) {
                throw "Source tree contains removed Artemis sidecar: App/Data/$obsoleteSidecar"
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

    Invoke-R3PowerShellFile -ScriptPath $gate
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
