param(
    [switch]$SkipInstaller
)

$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'Build-Windows.ps1') -Architecture x64 -SkipInstaller:$SkipInstaller
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}
