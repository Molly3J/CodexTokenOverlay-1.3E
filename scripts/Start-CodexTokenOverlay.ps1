param(
    [ValidateRange(1024, 65535)]
    [int]$PortStart = 9222,

    [ValidateRange(1024, 65535)]
    [int]$PortEnd = 9232,

    [string]$OverlayPath = "$env:LOCALAPPDATA\Programs\CodexTokenOverlay\CodexTokenOverlay.exe",

    [string]$SettingsPath = "$env:LOCALAPPDATA\CodexTokenOverlay\settings.json",

    [string]$ReportPath = "$env:LOCALAPPDATA\CodexTokenOverlay\launch-report.json",

    [switch]$SkipOverlayStart
)

$ErrorActionPreference = 'Stop'

if ($PortEnd -lt $PortStart) {
    throw 'PortEnd must be greater than or equal to PortStart.'
}

$result = [ordered]@{
    StartedAt = (Get-Date).ToString('o')
    Version = '1.4.0'
    CodexPackage = $null
    CodexVersion = $null
    CodexExecutable = $null
    CodexPid = $null
    CdpPort = $null
    ListenerReady = $false
    CompatiblePageFound = $false
    ReusedExistingCodex = $false
    RestartedCodex = $false
    SettingsCreated = $false
    SettingsUpdated = $false
    SettingsBackup = $null
    OverlayStarted = $false
    OverlayRestarted = $false
    Error = $null
}

$exitCode = 0
$settingsChanged = $false

function Set-ObjectProperty {
    param(
        [Parameter(Mandatory)] [object]$Object,
        [Parameter(Mandatory)] [string]$Name,
        [Parameter(Mandatory)] $Value
    )

    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property) {
        $Object | Add-Member -NotePropertyName $Name -NotePropertyValue $Value
        return $true
    }

    $currentJson = ConvertTo-Json -InputObject $property.Value -Compress -Depth 5
    $nextJson = ConvertTo-Json -InputObject $Value -Compress -Depth 5
    if ($currentJson -cne $nextJson) {
        $property.Value = $Value
        return $true
    }

    return $false
}

function Get-Listener {
    param([int]$Port)

    Get-NetTCPConnection -State Listen -LocalPort $Port -ErrorAction SilentlyContinue |
        Where-Object { $_.LocalAddress -in @('127.0.0.1', '::1', '0.0.0.0', '::') } |
        Select-Object -First 1
}

function Get-CodexProcesses {
    param([string]$PackageRoot)

    @(Get-CimInstance Win32_Process | Where-Object {
        $_.ExecutablePath -and
        [IO.Path]::GetFullPath($_.ExecutablePath).StartsWith(
            $PackageRoot,
            [StringComparison]::OrdinalIgnoreCase)
    })
}

function Test-ListenerOwnedByCodex {
    param(
        [object]$Listener,
        [object[]]$CodexProcesses
    )

    if ($null -eq $Listener) {
        return $false
    }

    return $Listener.OwningProcess -in @($CodexProcesses.ProcessId)
}

function Find-AvailablePort {
    param(
        [int]$PreferredPort,
        [int]$FirstPort,
        [int]$LastPort,
        [object[]]$CodexProcesses
    )

    $candidates = @($PreferredPort) + @($FirstPort..$LastPort) | Select-Object -Unique
    foreach ($candidate in $candidates) {
        if ($candidate -lt 1024 -or $candidate -gt 65535) {
            continue
        }

        $listener = Get-Listener -Port $candidate
        if ($null -eq $listener -or (Test-ListenerOwnedByCodex -Listener $listener -CodexProcesses $CodexProcesses)) {
            return $candidate
        }
    }

    throw "No free CDP port was found in range $FirstPort-$LastPort."
}

try {
    $package = Get-AppxPackage OpenAI.Codex |
        Sort-Object Version -Descending |
        Select-Object -First 1
    if ($null -eq $package) {
        throw 'The Microsoft Store package OpenAI.Codex was not found.'
    }

    $packageRoot = [IO.Path]::GetFullPath($package.InstallLocation).TrimEnd('\') + '\'
    $codexExecutable = [IO.Path]::GetFullPath((Join-Path $packageRoot 'app\ChatGPT.exe'))
    if (-not $codexExecutable.StartsWith($packageRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Resolved Codex executable escaped the package root.'
    }
    if (-not (Test-Path -LiteralPath $codexExecutable -PathType Leaf)) {
        throw "Codex executable was not found: $codexExecutable"
    }

    $result.CodexPackage = $package.PackageFullName
    $result.CodexVersion = $package.Version.ToString()
    $result.CodexExecutable = $codexExecutable

    $settings = $null
    $settingsExisted = Test-Path -LiteralPath $SettingsPath -PathType Leaf
    if ($settingsExisted) {
        try {
            $settings = Get-Content -LiteralPath $SettingsPath -Raw -Encoding utf8 | ConvertFrom-Json
        }
        catch {
            $invalidBackup = "$SettingsPath.invalid-$((Get-Date).ToString('yyyyMMdd-HHmmss')).bak"
            Copy-Item -LiteralPath $SettingsPath -Destination $invalidBackup -Force
            $result.SettingsBackup = $invalidBackup
            $settings = $null
        }
    }

    if ($null -eq $settings) {
        $settings = [pscustomobject][ordered]@{
            SettingsVersion = 1
            AnchorMode = 4
            VisibleFields = 1663
            CollapsedPrimaryField = 1024
            CollapsedSecondaryField = 512
            ManualPlacementEnabled = $false
            OverlayScalePercent = 100
            DisplayBackend = 1
            CdpPort = $PortStart
            CdpExpectedCodexVersion = $package.Version.ToString()
        }
        $result.SettingsCreated = $true
        $settingsChanged = $true
    }

    $codexProcesses = Get-CodexProcesses -PackageRoot $packageRoot
    $preferredPort = if ($settings.CdpPort -is [int] -and $settings.CdpPort -ge 1024 -and $settings.CdpPort -le 65535) {
        [int]$settings.CdpPort
    }
    else {
        $PortStart
    }
    $port = Find-AvailablePort -PreferredPort $preferredPort -FirstPort $PortStart -LastPort $PortEnd -CodexProcesses $codexProcesses
    $result.CdpPort = $port

    $settingsChanged = (Set-ObjectProperty -Object $settings -Name 'SettingsVersion' -Value 1) -or $settingsChanged
    $settingsChanged = (Set-ObjectProperty -Object $settings -Name 'DisplayBackend' -Value 1) -or $settingsChanged
    $settingsChanged = (Set-ObjectProperty -Object $settings -Name 'CdpPort' -Value $port) -or $settingsChanged
    $settingsChanged = (Set-ObjectProperty -Object $settings -Name 'CdpExpectedCodexVersion' -Value $package.Version.ToString()) -or $settingsChanged

    if ($settingsChanged) {
        if ($settingsExisted -and -not $result.SettingsBackup) {
            $settingsBackup = "$SettingsPath.pre-1.4.0-$((Get-Date).ToString('yyyyMMdd-HHmmss')).bak"
            Copy-Item -LiteralPath $SettingsPath -Destination $settingsBackup -Force
            $result.SettingsBackup = $settingsBackup
        }
        $settingsDirectory = Split-Path -Parent $SettingsPath
        New-Item -ItemType Directory -Path $settingsDirectory -Force | Out-Null
        $settings | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $SettingsPath -Encoding utf8
        $result.SettingsUpdated = $true
    }

    $codexProcesses = Get-CodexProcesses -PackageRoot $packageRoot
    $packagePids = @($codexProcesses.ProcessId)
    $existingMain = $codexProcesses |
        Where-Object { $_.ParentProcessId -notin $packagePids -and $_.CommandLine -notlike '*--type=*' } |
        Select-Object -First 1
    $listener = Get-Listener -Port $port
    $canReuse = (
        $existingMain -and
        $existingMain.CommandLine -like "*--remote-debugging-port=$port*" -and
        (Test-ListenerOwnedByCodex -Listener $listener -CodexProcesses $codexProcesses)
    )

    if ($canReuse) {
        $result.ReusedExistingCodex = $true
        $result.CodexPid = $existingMain.ProcessId
    }
    else {
        foreach ($process in $codexProcesses | Sort-Object ProcessId -Descending) {
            Stop-Process -Id $process.ProcessId -Force -ErrorAction SilentlyContinue
        }

        $stopDeadline = (Get-Date).AddSeconds(15)
        do {
            Start-Sleep -Milliseconds 250
            $remaining = Get-CodexProcesses -PackageRoot $packageRoot
        } while ($remaining.Count -gt 0 -and (Get-Date) -lt $stopDeadline)
        if ($remaining.Count -gt 0) {
            throw 'Codex processes did not exit within 15 seconds.'
        }

        $started = Start-Process -FilePath $codexExecutable -ArgumentList @(
            "--remote-debugging-port=$port",
            '--remote-debugging-address=127.0.0.1'
        ) -PassThru
        $result.CodexPid = $started.Id
        $result.RestartedCodex = $true
    }

    $readyDeadline = (Get-Date).AddSeconds(30)
    do {
        Start-Sleep -Milliseconds 500
        $codexProcesses = Get-CodexProcesses -PackageRoot $packageRoot
        $listener = Get-Listener -Port $port
        $listenerIsCodex = Test-ListenerOwnedByCodex -Listener $listener -CodexProcesses $codexProcesses
    } while (-not $listenerIsCodex -and (Get-Date) -lt $readyDeadline)

    if (-not $listenerIsCodex) {
        throw "Codex did not open a verified loopback CDP listener on port $port."
    }
    $result.ListenerReady = $true

    $targets = @(Invoke-RestMethod -Uri "http://127.0.0.1:$port/json/list" -TimeoutSec 5)
    $compatiblePage = $targets | Where-Object {
        $_.type -eq 'page' -and [string]$_.url -like 'app://*'
    } | Select-Object -First 1
    if ($null -eq $compatiblePage) {
        throw 'The CDP endpoint did not expose a compatible Codex app page.'
    }
    $result.CompatiblePageFound = $true
}
catch {
    $result.Error = $_.Exception.Message
    $exitCode = 1
}
finally {
    if (-not $SkipOverlayStart -and (Test-Path -LiteralPath $OverlayPath -PathType Leaf)) {
        $resolvedOverlayPath = [IO.Path]::GetFullPath($OverlayPath)
        $overlayProcesses = @(Get-CimInstance Win32_Process | Where-Object {
            $_.Name -eq 'CodexTokenOverlay.exe' -and
            $_.ExecutablePath -and
            [IO.Path]::GetFullPath($_.ExecutablePath).Equals($resolvedOverlayPath, [StringComparison]::OrdinalIgnoreCase)
        })

        if ($settingsChanged -and $overlayProcesses.Count -gt 0) {
            foreach ($process in $overlayProcesses) {
                Stop-Process -Id $process.ProcessId -Force -ErrorAction SilentlyContinue
            }
            $result.OverlayRestarted = $true
            $overlayProcesses = @()
        }

        if ($overlayProcesses.Count -eq 0) {
            Start-Process -FilePath $resolvedOverlayPath -WindowStyle Hidden | Out-Null
            $result.OverlayStarted = $true
        }
    }

    $result.CompletedAt = (Get-Date).ToString('o')
    $reportDirectory = Split-Path -Parent $ReportPath
    if ($reportDirectory) {
        New-Item -ItemType Directory -Path $reportDirectory -Force | Out-Null
    }
    $result | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $ReportPath -Encoding utf8
}

exit $exitCode
