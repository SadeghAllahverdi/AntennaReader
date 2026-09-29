<#
.SYNOPSIS
    Removes AntennaReader from this machine.

.DESCRIPTION
    Runs from the root of the Workspace ONE package:

        powershell.exe -ExecutionPolicy Bypass -NoProfile -File .\Uninstall-AntennaReader.ps1

    1. Closes AntennaReader if it is running from the install folder. An unsaved diagram in that window is lost.
    2. Removes the "AntennaReader" shortcuts from the Start Menu and the desktop.
    3. Deletes C:\Program Files\UPLINK\AntennaReader, and C:\Program Files\UPLINK only if nothing else is left
       in it (the Network Configuration Tool lives there too).

    What it deliberately keeps: every user's %LOCALAPPDATA%\AntennaReader, which holds their saved diagrams,
    preferences and exports. There is no other copy of that data. It also keeps the install logs.

    Exit code 0 means AntennaReader is gone (also when it was not installed), 1 means it could not be removed.
    The details go to a log in C:\ProgramData\UPLINK\Logs\AntennaReader.

.PARAMETER WhatIfOnly
    Shows what would be removed and changes nothing. Works without administrator rights.

.PARAMETER InstallDir
    Only for testing on a machine where you are not an administrator; Workspace ONE uses the default.
    The same goes for StartMenuDir, DesktopDir and LogDir.
#>
[CmdletBinding()]
param(
    [switch] $WhatIfOnly,
    [string] $InstallDir   = (Join-Path $env:ProgramFiles 'UPLINK\AntennaReader'),
    [string] $StartMenuDir = (Join-Path $env:ProgramData 'Microsoft\Windows\Start Menu\Programs'),
    [string] $DesktopDir   = (Join-Path $env:PUBLIC 'Desktop'),
    [string] $LogDir       = (Join-Path $env:ProgramData 'UPLINK\Logs\AntennaReader')
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$ExecutableName = 'AntennaReader.exe'
$ShortcutName   = 'AntennaReader.lnk'
$InstalledExe   = Join-Path $InstallDir $ExecutableName
$InstallParent  = Split-Path -Parent $InstallDir

function Test-Elevated {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    (New-Object Security.Principal.WindowsPrincipal $identity).IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Start-SetupLog {
    param([string] $Kind)
    try {
        New-Item -ItemType Directory -Path $LogDir -Force | Out-Null
        Get-ChildItem -LiteralPath $LogDir -Filter '*.log' -File -ErrorAction SilentlyContinue |
            Sort-Object LastWriteTime -Descending | Select-Object -Skip 20 |
            Remove-Item -Force -ErrorAction SilentlyContinue
        $path = Join-Path $LogDir ('{0}-{1:yyyyMMdd-HHmmss}.log' -f $Kind, (Get-Date))
        Start-Transcript -Path $path -Force | Out-Null
        return $path
    } catch {
        return "(no log written: $($_.Exception.Message))"
    }
}

function Stop-RunningApp {
    # Matched on the full path, so a copy running from somewhere else (a development build) is left alone.
    $running = @(Get-Process -Name ([IO.Path]::GetFileNameWithoutExtension($ExecutableName)) -ErrorAction SilentlyContinue |
        Where-Object { try { $_.Path -eq $InstalledExe } catch { $false } })
    if ($running.Count -eq 0) { return }
    Write-Host ("  closing {0} (PID {1})" -f $ExecutableName, (($running | ForEach-Object { $_.Id }) -join ', '))
    foreach ($process in $running) {
        try { $process | Stop-Process -Force -ErrorAction Stop }
        catch { Write-Warning "  PID $($process.Id): $($_.Exception.Message)" }
    }
    foreach ($process in $running) { try { $process.WaitForExit(15000) | Out-Null } catch { } }
}

function Test-OurShortcut {
    # Only delete a shortcut that really points at our exe, never someone else's file with the same name.
    param([string] $Path)
    if (-not (Test-Path -LiteralPath $Path)) { return $false }
    $shell = New-Object -ComObject WScript.Shell
    try { return $shell.CreateShortcut($Path).TargetPath -eq $InstalledExe }
    finally { [void][Runtime.InteropServices.Marshal]::ReleaseComObject($shell) }
}

$shortcuts = @(
    (Join-Path $StartMenuDir $ShortcutName)
    (Join-Path $DesktopDir $ShortcutName)
)

if ($WhatIfOnly) {
    Write-Host 'Would remove:'
    foreach ($s in $shortcuts) { if (Test-OurShortcut $s) { Write-Host "  shortcut    $s" } }
    if (Test-Path -LiteralPath $InstallDir) { Write-Host "  application $InstallDir" }
    else { Write-Host "  (AntennaReader is not installed in $InstallDir)" }
    Write-Host "Would keep every user's %LOCALAPPDATA%\AntennaReader (their saved diagrams)."
    exit 0
}

# Only the default location needs this check; a custom -InstallDir is a test run on a non-admin machine.
if (-not $PSBoundParameters.ContainsKey('InstallDir') -and -not (Test-Elevated)) {
    Write-Host 'UNINSTALL FAILED: run this elevated (as administrator or SYSTEM). Nothing was changed.' -ForegroundColor Red
    exit 1
}

$exitCode = 1
$logPath = Start-SetupLog 'Uninstall'
try {
    Write-Host "Uninstalling AntennaReader from $InstallDir"

    if (Test-Path -LiteralPath $InstalledExe) {
        Write-Host "Found version $((Get-Item -LiteralPath $InstalledExe).VersionInfo.FileVersion); closing it if it is running ..."
        Stop-RunningApp
    }

    Write-Host 'Removing shortcuts ...'
    foreach ($s in $shortcuts) {
        if (Test-OurShortcut $s) {
            Remove-Item -LiteralPath $s -Force
            Write-Host "  removed $s"
        }
    }

    if (Test-Path -LiteralPath $InstallDir) {
        Write-Host 'Removing application ...'
        Remove-Item -LiteralPath $InstallDir -Recurse -Force
        Write-Host "  removed $InstallDir"
    } else {
        Write-Host "  (nothing installed in $InstallDir)"
    }

    # The parent is shared with other UPLINK applications: remove it only when it is empty.
    if ((Test-Path -LiteralPath $InstallParent) -and @(Get-ChildItem -LiteralPath $InstallParent -Force).Count -eq 0) {
        try {
            Remove-Item -LiteralPath $InstallParent -Force
            Write-Host "  removed empty $InstallParent"
        } catch { Write-Warning "Kept $InstallParent ($($_.Exception.Message))" }
    }

    Write-Host "Kept every user's %LOCALAPPDATA%\AntennaReader (their saved diagrams)."
    Write-Host 'AntennaReader removed.' -ForegroundColor Green
    $exitCode = 0
} catch {
    Write-Host "UNINSTALL FAILED: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host $_.ScriptStackTrace
} finally {
    Write-Host "Log: $logPath"
    try { Stop-Transcript | Out-Null } catch { }
}
exit $exitCode
