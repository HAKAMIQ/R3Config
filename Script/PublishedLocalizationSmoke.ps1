param(
    [Parameter(Mandatory = $true)]
    [string] $PublishDir
)
$ErrorActionPreference = 'Stop'

$resolved = (Resolve-Path -LiteralPath $PublishDir).Path
$exe = Join-Path $resolved 'R3Config.exe'
$keys = @(
    'App.Title', 'Games.Title', 'Games.SearchPlaceholder',
    'Games.FilterDetectedOnly', 'Games.EmptyInstalled',
    'Shell.NoGameTitle', 'Shell.NoGameHint', 'Btn.Analyze',
    'Action.More', 'Btn.Browse'
)

if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) { throw "Published executable missing: $exe" }

foreach ($lang in @('ar-SA', 'en-US')) {
    $jsonPath = Join-Path $resolved "Lang\$lang.json"
    if (Test-Path -LiteralPath $jsonPath -PathType Leaf) {
        throw "Published release must not expose editable JSON language source: $jsonPath"
    }
}

$temp = Join-Path ([IO.Path]::GetTempPath()) ("R3Config-LocSmoke-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $temp | Out-Null
try {
    foreach ($lang in @('ar-SA', 'en-US')) {
        $file = Join-Path $resolved "Lang\$lang.r3lang"
        if (-not (Test-Path -LiteralPath $file -PathType Leaf)) { throw "Published language pack missing: $file" }
        if ((Get-Item -LiteralPath $file).Length -le 32) { throw "Published language pack is too small: $file" }

        $stream = [IO.File]::OpenRead($file)
        try {
            $header = New-Object byte[] 8
            if ($stream.Read($header, 0, $header.Length) -ne $header.Length) {
                throw "Published language pack header is truncated: $file"
            }
            $magic = [Text.Encoding]::ASCII.GetString($header)
            if ($magic -ne 'R3LANG01') {
                throw "Published language pack magic is invalid: $file"
            }
        }
        finally {
            $stream.Dispose()
        }

        $result = Join-Path $temp "$lang.json"
        $process = Start-Process -FilePath $exe -WorkingDirectory $resolved -ArgumentList @('--localization-smoke', $lang, $result) -PassThru -Wait
        if ($process.ExitCode -ne 0) { throw "Published localization smoke failed for $lang with exit code $($process.ExitCode)." }
        if (-not (Test-Path -LiteralPath $result -PathType Leaf)) { throw "Published localization smoke produced no result for $lang." }
        $actual = Get-Content -Raw -LiteralPath $result | ConvertFrom-Json
        if ($actual.CurLang -ne $lang) { throw "UiText.CurLang was '$($actual.CurLang)' for requested '$lang'." }
        if ($actual.AppBase -ne ($resolved.TrimEnd('\') + '\')) { throw "UiText AppContext.BaseDirectory was '$($actual.AppBase)', expected '$resolved\'." }
        if ($actual.ResolvedLanguagePath -ne $file) { throw "Resolved language path was '$($actual.ResolvedLanguagePath)', expected '$file'." }
        foreach ($key in $keys) {
            $value = [string]$actual.Values.$key
            if ([string]::IsNullOrWhiteSpace($value) -or $value -eq $key) { throw "UiText returned localization key '$key' for $lang." }
        }
        Write-Host "Published compiled localization smoke PASS: $lang"
    }
}
finally {
    if (Test-Path -LiteralPath $temp) { Remove-Item -LiteralPath $temp -Recurse -Force }
}
