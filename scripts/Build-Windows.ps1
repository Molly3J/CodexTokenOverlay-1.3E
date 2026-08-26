param(
    [ValidateSet('all', 'x86', 'x64')]
    [string]$Architecture = 'all',
    [switch]$SkipInstaller
)

$ErrorActionPreference = 'Stop'
$version = '0.1.1'
$numericVersion = '0.1.1.0'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$dist = [IO.Path]::GetFullPath((Join-Path $root 'dist'))
$release = [IO.Path]::GetFullPath((Join-Path $dist 'release'))
$rootPrefix = $root.TrimEnd('\') + '\'

if (-not $dist.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Resolved dist path escaped the repository root.'
}

New-Item -ItemType Directory -Path $release -Force | Out-Null
$targets = if ($Architecture -eq 'all') { @('x86', 'x64') } else { @($Architecture) }

$iscc = $null
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
}

$results = foreach ($target in $targets) {
    $rid = "win-$target"
    $publish = [IO.Path]::GetFullPath((Join-Path $dist "$rid\publish"))
    if (Test-Path -LiteralPath $publish) {
        Remove-Item -LiteralPath $publish -Recurse -Force
    }
    New-Item -ItemType Directory -Path $publish -Force | Out-Null

    dotnet publish (Join-Path $root 'src\CodexTokenOverlay.csproj') `
        -c Release `
        -r $rid `
        --self-contained true `
        -p:PlatformTarget=$target `
        -o $publish `
        --nologo | Write-Host
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed for $rid with exit code $LASTEXITCODE."
    }

    $payload = Join-Path $publish 'CodexTokenOverlay.exe'
    if (-not (Test-Path -LiteralPath $payload -PathType Leaf)) {
        throw "Published executable was not created for $rid."
    }
    if (Get-ChildItem -LiteralPath $publish -Filter '*.pdb' -File) {
        throw "Release output for $rid contains a PDB file."
    }

    $setup = $null
    if (-not $SkipInstaller) {
        & $iscc "/DMyAppArch=$target" "/DMyAppDisplayVersion=$version" "/DMyAppNumericVersion=$numericVersion" (Join-Path $root 'installer\CodexTokenOverlay.iss') | Write-Host
        if ($LASTEXITCODE -ne 0) {
            throw "Inno Setup failed for $rid with exit code $LASTEXITCODE."
        }
        $setup = Join-Path $release "CodexTokenOverlay-$version-windows-$target-Setup.exe"
        if (-not (Test-Path -LiteralPath $setup -PathType Leaf)) {
            throw "Installer was not created for $rid."
        }
    }

    [pscustomobject]@{
        Version = $version
        Runtime = $rid
        Payload = $payload
        PayloadSHA256 = (Get-FileHash -LiteralPath $payload -Algorithm SHA256).Hash.ToLowerInvariant()
        Setup = $setup
        SetupSHA256 = if ($setup) { (Get-FileHash -LiteralPath $setup -Algorithm SHA256).Hash.ToLowerInvariant() } else { $null }
    }
}

$results | ConvertTo-Json -Depth 3
