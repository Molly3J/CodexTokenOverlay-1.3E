$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$failures = [System.Collections.Generic.List[string]]::new()

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) {
        $failures.Add($Message)
    }
}

$assemblyInfo = Get-Content -LiteralPath (Join-Path $root 'src\Properties\AssemblyInfo.cs') -Raw
$settingsSource = Get-Content -LiteralPath (Join-Path $root 'src\CodexTokenOverlay\OverlaySettings.cs') -Raw
$installer = Get-Content -LiteralPath (Join-Path $root 'installer\CodexTokenOverlay.iss') -Raw
$launcher = Get-Content -LiteralPath (Join-Path $root 'scripts\Start-CodexTokenOverlay.ps1') -Raw
$machinePathPattern = 'C:' + '\\Users\\'
$desktopHostPattern = 'DESKTOP-' + '[A-Z0-9]{5,}'

Assert-True ($assemblyInfo -match 'AssemblyFileVersion\("1\.3\.0\.0"\)') 'Assembly file version is not 1.3.0.0.'
Assert-True ($assemblyInfo -match 'AssemblyInformationalVersion\("1\.3E"\)') 'Assembly informational version is not 1.3E.'
Assert-True ($assemblyInfo -notmatch $machinePathPattern) 'Assembly metadata contains a machine-specific user path.'
Assert-True ($settingsSource -match 'DisplayBackend = DisplayBackendKind\.ExperimentalCdp') 'Fresh-user default is not the in-page backend.'
Assert-True ($installer -match 'Name: "desktopicon"') 'Installer does not expose a desktop shortcut task.'
Assert-True ($installer -match 'CODEX\(tokenoverlay\)') 'Installer shortcut name is incorrect.'
Assert-True ($installer -match 'Tasks: desktopicon') 'Desktop shortcut is not gated by the installer task.'
Assert-True ($installer -match '\{autodesktop\}\\CODEX\(tokenoverlay\)\.lnk') 'Installer does not remove an old desktop shortcut when the task is unselected.'
Assert-True ($installer -match 'IconFilename: "\{app\}\\Codex\.ico"') 'Shortcut does not use the packaged Codex icon.'
Assert-True ($launcher -match 'Get-AppxPackage OpenAI\.Codex') 'Launcher does not resolve the Store Codex package.'
Assert-True ($launcher -match 'Test-ListenerOwnedByCodex') 'Launcher does not verify CDP listener ownership.'
Assert-True ($launcher -match 'ConvertTo-Json -InputObject \$property\.Value') 'Launcher does not compare persisted values semantically.'
Assert-True ($launcher -notmatch $machinePathPattern -and $launcher -notmatch $desktopHostPattern -and $launcher -notmatch 'api[_-]?key|Bearer\s') 'Launcher contains a machine path or credential-like value.'

$textFiles = Get-ChildItem -LiteralPath $root -Recurse -File | Where-Object {
    $_.FullName -notmatch '\\(dist|bin|obj|\.git)\\' -and
    $_.Extension -in @('.cs', '.csproj', '.ps1', '.iss', '.md', '.txt', '.yml', '.yaml', '.gitignore')
}
foreach ($file in $textFiles) {
    $content = Get-Content -LiteralPath $file.FullName -Raw
    if ($content -match $machinePathPattern -or $content -match $desktopHostPattern) {
        $failures.Add("Machine identity found in $([IO.Path]::GetRelativePath($root, $file.FullName)).")
    }
}

$payload = Join-Path $root 'dist\publish\CodexTokenOverlay.exe'
$setup = Join-Path $root 'dist\CodexTokenOverlay-1.3E-Setup.exe'
Assert-True (Test-Path -LiteralPath $payload -PathType Leaf) 'Published payload is missing.'
Assert-True (Test-Path -LiteralPath $setup -PathType Leaf) 'Setup executable is missing.'
Assert-True (-not (Get-ChildItem -LiteralPath (Split-Path -Parent $payload) -Filter '*.pdb' -File)) 'Published output contains a PDB.'

if (Test-Path -LiteralPath $payload -PathType Leaf) {
    $item = Get-Item -LiteralPath $payload
    Assert-True ($item.VersionInfo.FileVersion -eq '1.3.0.0') "Payload FileVersion is $($item.VersionInfo.FileVersion)."
    Assert-True ($item.VersionInfo.ProductVersion -eq '1.3E') "Payload ProductVersion is $($item.VersionInfo.ProductVersion)."
    $binaryText = [Text.Encoding]::Latin1.GetString([IO.File]::ReadAllBytes($payload))
    Assert-True ($binaryText -notmatch $machinePathPattern -and $binaryText -notmatch $desktopHostPattern -and $binaryText -notmatch 'CodexTokenOverlay\.pdb') 'Payload contains a machine identity or PDB path.'

    $fixtureSessions = Join-Path $root 'tests\fixtures\sessions'
    $fixtureOutput = Join-Path $root "dist\fixture-probe-$([guid]::NewGuid().ToString('N')).json"
    $probeProcess = Start-Process -FilePath $payload -ArgumentList @(
        '--probe',
        "`"$fixtureOutput`"",
        '--sessions',
        "`"$fixtureSessions`""
    ) -Wait -PassThru -WindowStyle Hidden
    Assert-True ($probeProcess.ExitCode -eq 0) "Fixture probe exited with $($probeProcess.ExitCode)."
    if (Test-Path -LiteralPath $fixtureOutput -PathType Leaf) {
        $fixture = Get-Content -LiteralPath $fixtureOutput -Raw | ConvertFrom-Json
        Assert-True ($fixture.ThreadId -eq '11111111-1111-1111-1111-111111111111') 'Fixture thread id was not parsed.'
        Assert-True ($fixture.TotalTokens -eq 1600) 'Fixture total tokens were not parsed.'
        Assert-True ($fixture.ContextWindowTokens -eq 100000) 'Fixture context window was not parsed.'
        Assert-True ([math]::Round($fixture.CacheHitPercent, 1) -eq 75.0) 'Fixture cache hit percent was not parsed.'
    }
    else {
        $failures.Add('Fixture probe output was not created.')
    }
}

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Error $_ }
    exit 1
}

Write-Output 'TEST_RELEASE_OK'
