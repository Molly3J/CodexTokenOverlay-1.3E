$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$fixtureSessions = Join-Path $root 'tests\fixtures\sessions'
$probeOutput = Join-Path $root "dist\portable-fixture-$([guid]::NewGuid().ToString('N')).json"
New-Item -ItemType Directory -Path (Join-Path $root 'dist') -Force | Out-Null

dotnet run --project (Join-Path $root 'src\CodexTokenOverlay.Portable\CodexTokenOverlay.Portable.csproj') `
    -c Release `
    -- `
    --probe $probeOutput `
    --sessions $fixtureSessions
if ($LASTEXITCODE -ne 0) {
    throw "Portable fixture probe exited with $LASTEXITCODE."
}

if (-not (Test-Path -LiteralPath $probeOutput -PathType Leaf)) {
    throw 'Portable fixture probe did not create output.'
}
$fixture = Get-Content -LiteralPath $probeOutput -Raw | ConvertFrom-Json
Remove-Item -LiteralPath $probeOutput -Force

if ($fixture.ThreadId -ne '11111111-1111-1111-1111-111111111111' -or
    $fixture.TotalTokens -ne 1600 -or
    $fixture.ContextWindowTokens -ne 100000 -or
    [math]::Round($fixture.CacheHitPercent, 1) -ne 75.0) {
    throw 'Portable fixture probe returned unexpected token data.'
}

Write-Output 'TEST_PORTABLE_OK'
