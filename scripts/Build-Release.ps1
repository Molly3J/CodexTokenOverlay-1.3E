param(
    [switch]$SkipInstaller
)

$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$dist = [IO.Path]::GetFullPath((Join-Path $root 'dist'))
$publish = [IO.Path]::GetFullPath((Join-Path $dist 'publish'))
$rootPrefix = $root.TrimEnd('\') + '\'

if (-not $dist.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Resolved dist path escaped the repository root.'
}

if (Test-Path -LiteralPath $publish) {
    Remove-Item -LiteralPath $publish -Recurse -Force
}
New-Item -ItemType Directory -Path $publish -Force | Out-Null

dotnet publish (Join-Path $root 'src\CodexTokenOverlay.csproj') `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -o $publish `
    --nologo
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

$payload = Join-Path $publish 'CodexTokenOverlay.exe'
if (-not (Test-Path -LiteralPath $payload -PathType Leaf)) {
    throw 'Published executable was not created.'
}
if (Get-ChildItem -LiteralPath $publish -Filter '*.pdb' -File) {
    throw 'Release output contains a PDB file.'
}

if (-not $SkipInstaller) {
    $isccCandidates = @(
        (Get-Command ISCC.exe -ErrorAction SilentlyContinue).Source,
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles(x86)\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
    ) | Where-Object { $_ -and (Test-Path -LiteralPath $_ -PathType Leaf) } | Select-Object -Unique
    $iscc = $isccCandidates | Select-Object -First 1
    if (-not $iscc) {
        throw 'Inno Setup 6 compiler ISCC.exe was not found.'
    }

    & $iscc (Join-Path $root 'installer\CodexTokenOverlay.iss')
    if ($LASTEXITCODE -ne 0) {
        throw "Inno Setup failed with exit code $LASTEXITCODE."
    }
}

$hashFiles = @(Get-ChildItem -LiteralPath $dist -File | Where-Object { $_.Extension -eq '.exe' })
$hashFiles += Get-Item -LiteralPath $payload
$hashLines = $hashFiles | Sort-Object FullName -Unique | ForEach-Object {
    $relative = [IO.Path]::GetRelativePath($dist, $_.FullName).Replace('\', '/')
    '{0}  {1}' -f (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant(), $relative
}
$hashLines | Set-Content -LiteralPath (Join-Path $dist 'SHA256SUMS.txt') -Encoding utf8

[pscustomobject]@{
    Version = '1.3E'
    Payload = $payload
    PayloadSHA256 = (Get-FileHash -LiteralPath $payload -Algorithm SHA256).Hash
    Setup = if ($SkipInstaller) { $null } else { Join-Path $dist 'CodexTokenOverlay-1.3E-Setup.exe' }
    SetupSHA256 = if ($SkipInstaller) { $null } else { (Get-FileHash -LiteralPath (Join-Path $dist 'CodexTokenOverlay-1.3E-Setup.exe') -Algorithm SHA256).Hash }
} | ConvertTo-Json -Depth 3

