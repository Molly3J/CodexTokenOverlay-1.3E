param(
    [ValidateSet('x86', 'x64')]
    [string]$Architecture = 'x64',
    [switch]$SkipInstaller
)

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
$portableProject = Get-Content -LiteralPath (Join-Path $root 'src\CodexTokenOverlay.Portable\CodexTokenOverlay.Portable.csproj') -Raw
$machinePathPattern = 'C:' + '\\Users\\'
$desktopHostPattern = 'DESKTOP-' + '[A-Z0-9]{5,}'

Assert-True ($assemblyInfo -match 'AssemblyFileVersion\("1\.4\.0\.0"\)') 'Assembly file version is not 1.4.0.0.'
Assert-True ($assemblyInfo -match 'AssemblyInformationalVersion\("1\.4\.0"\)') 'Assembly informational version is not 1.4.0.'
Assert-True ($assemblyInfo -notmatch $machinePathPattern) 'Assembly metadata contains a machine-specific user path.'
Assert-True ($settingsSource -match 'DisplayBackend = DisplayBackendKind\.ExperimentalCdp') 'Fresh-user default is not the in-page backend.'
Assert-True ($settingsSource -match 'CdpPort = 19222') 'Fresh-user CDP port is not the dedicated port 19222.'
Assert-True ($settingsSource -match 'value is >= 9222 and <= 9232') 'Legacy shared CDP ports are not migrated to 19222.'
Assert-True ($settingsSource -match '\? value\.Value : 19222') 'Invalid CDP ports do not fall back to 19222.'
Assert-True ($installer -match 'MyAppArch') 'Installer is not architecture-aware.'
Assert-True ($installer -match 'Name: "desktopicon"') 'Installer does not expose a desktop shortcut task.'
Assert-True ($installer -match 'CODEX\(tokenoverlay\)') 'Installer shortcut name is incorrect.'
Assert-True ($installer -match 'Tasks: desktopicon') 'Desktop shortcut is not gated by the installer task.'
Assert-True ($installer -match '\{autodesktop\}\\CODEX\(tokenoverlay\)\.lnk') 'Installer does not remove an old desktop shortcut when the task is unselected.'
Assert-True ($installer -match 'IconFilename: "\{app\}\\Codex\.ico"') 'Shortcut does not use the packaged Codex icon.'
Assert-True ($launcher -match 'Get-AppxPackage OpenAI\.Codex') 'Launcher does not resolve the Store Codex package.'
Assert-True ($launcher -match '\[int\]\$PortStart = 19222') 'Launcher does not start its dedicated CDP range at 19222.'
Assert-True ($launcher -match '\[int\]\$PortEnd = 19222') 'Launcher does not end its dedicated CDP range at 19222.'
Assert-True ($launcher -match '(?s)\$settings\.CdpPort -ge \$PortStart.*\$settings\.CdpPort -le \$PortEnd') 'Launcher can reuse a persisted CDP port outside the dedicated range.'
Assert-True ($launcher -match 'Test-ListenerOwnedByCodex') 'Launcher does not verify CDP listener ownership.'
Assert-True ($launcher -match 'ConvertTo-Json -InputObject \$property\.Value') 'Launcher does not compare persisted values semantically.'
Assert-True ($launcher -notmatch $machinePathPattern -and $launcher -notmatch $desktopHostPattern -and $launcher -notmatch 'api[_-]?key|Bearer\s') 'Launcher contains a machine path or credential-like value.'
Assert-True ($portableProject -match '<TargetFramework>net10\.0</TargetFramework>') 'Portable project does not target cross-platform .NET.'
Assert-True ($portableProject -notmatch 'net10\.0-windows|UseWindowsForms|UseWPF') 'Portable project contains Windows-only framework settings.'

$textFiles = Get-ChildItem -LiteralPath $root -Recurse -File | Where-Object {
    $_.FullName -notmatch '\\(dist|bin|obj|\.git)\\' -and
    $_.Extension -in @('.cs', '.csproj', '.ps1', '.sh', '.iss', '.spec', '.plist', '.desktop', '.md', '.txt', '.yml', '.yaml', '.gitignore')
}
foreach ($file in $textFiles) {
    $content = Get-Content -LiteralPath $file.FullName -Raw
    if ($content -match $machinePathPattern -or $content -match $desktopHostPattern) {
        $failures.Add("Machine identity found in $([IO.Path]::GetRelativePath($root, $file.FullName)).")
    }
}

$payload = Join-Path $root "dist\win-$Architecture\publish\CodexTokenOverlay.exe"
$setup = Join-Path $root "dist\release\CodexTokenOverlay-1.4.0-windows-$Architecture-Setup.exe"
Assert-True (Test-Path -LiteralPath $payload -PathType Leaf) "Published $Architecture payload is missing."
if (-not $SkipInstaller) {
    Assert-True (Test-Path -LiteralPath $setup -PathType Leaf) "The $Architecture setup executable is missing."
}
Assert-True (-not (Get-ChildItem -LiteralPath (Split-Path -Parent $payload) -Filter '*.pdb' -File -ErrorAction SilentlyContinue)) "Published $Architecture output contains a PDB."

if (Test-Path -LiteralPath $payload -PathType Leaf) {
    $item = Get-Item -LiteralPath $payload
    Assert-True ($item.VersionInfo.FileVersion -eq '1.4.0.0') "Payload FileVersion is $($item.VersionInfo.FileVersion)."
    Assert-True ($item.VersionInfo.ProductVersion -eq '1.4.0') "Payload ProductVersion is $($item.VersionInfo.ProductVersion)."
    $binaryText = [Text.Encoding]::Latin1.GetString([IO.File]::ReadAllBytes($payload))
    Assert-True ($binaryText -notmatch $machinePathPattern -and $binaryText -notmatch $desktopHostPattern -and $binaryText -notmatch 'CodexTokenOverlay\.pdb') 'Payload contains a machine identity or PDB path.'

    $fixtureSessions = Join-Path $root 'tests\fixtures\sessions'
    $fixtureOutput = Join-Path $root "dist\fixture-probe-$Architecture-$([guid]::NewGuid().ToString('N')).json"
    $probeProcess = Start-Process -FilePath $payload -ArgumentList @(
        '--probe',
        "`"$fixtureOutput`"",
        '--sessions',
        "`"$fixtureSessions`""
    ) -Wait -PassThru -WindowStyle Hidden
    Assert-True ($probeProcess.ExitCode -eq 0) "Fixture probe exited with $($probeProcess.ExitCode) for $Architecture."
    if (Test-Path -LiteralPath $fixtureOutput -PathType Leaf) {
        $fixture = Get-Content -LiteralPath $fixtureOutput -Raw | ConvertFrom-Json
        Assert-True ($fixture.ThreadId -eq '11111111-1111-1111-1111-111111111111') 'Fixture thread id was not parsed.'
        Assert-True ($fixture.TotalTokens -eq 1600) 'Fixture total tokens were not parsed.'
        Assert-True ($fixture.ContextWindowTokens -eq 100000) 'Fixture context window was not parsed.'
        Assert-True ([math]::Round($fixture.CacheHitPercent, 1) -eq 75.0) 'Fixture cache hit percent was not parsed.'
        Remove-Item -LiteralPath $fixtureOutput -Force
    }
    else {
        $failures.Add('Fixture probe output was not created.')
    }
}

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Error $_ }
    exit 1
}

Write-Output "TEST_RELEASE_$($Architecture.ToUpperInvariant())_OK"
