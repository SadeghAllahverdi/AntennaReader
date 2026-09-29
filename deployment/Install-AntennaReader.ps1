<#
.SYNOPSIS
    Installs AntennaReader for every user on this machine.

.DESCRIPTION
    Runs from the root of the Workspace ONE package, next to the app\ folder:

        powershell.exe -ExecutionPolicy Bypass -NoProfile -File .\Install-AntennaReader.ps1

    1. Closes AntennaReader if it is running from the install folder. Windows cannot replace an exe that
       is in use, so an update has to close it first. An unsaved diagram in that window is lost.
    2. Copies app\ to C:\Program Files\UPLINK\AntennaReader, replacing whatever version was there.
    3. Creates "AntennaReader" shortcuts in the Start Menu and on the desktop, for all users.

    Installing needs administrator rights (Workspace ONE runs this as SYSTEM). Running AntennaReader does
    not: everything it saves goes to each user's own %LOCALAPPDATA%\AntennaReader.

    It never changes the permissions of C:\Program Files\UPLINK. The Network Configuration Tool checks
    that folder's permissions and goes read-only if they change.

    Exit code 0 means installed, 1 means failed. Workspace ONE keeps only the exit code, so the details go
    to a log in C:\ProgramData\UPLINK\Logs\AntennaReader. That is a folder of its own because the Network
    Configuration Tool keeps only its newest 20 logs in C:\ProgramData\UPLINK\Logs and would delete ours.

.PARAMETER WhatIfOnly
    Shows what would be done and changes nothing. Works without administrator rights.

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
$AppSource      = Join-Path $PSScriptRoot 'app'
$InstalledExe   = Join-Path $InstallDir $ExecutableName

function Test-Elevated {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    (New-Object Security.Principal.WindowsPrincipal $identity).IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Start-SetupLog {
    param([string] $Kind)
    try {
        New-Item -ItemType Directory -Path $LogDir -Force | Out-Null
        # The newest 20 logs are plenty to diagnose a device; do not let them pile up forever.
        Get-ChildItem -LiteralPath $LogDir -Filter '*.log' -File -ErrorAction SilentlyContinue |
            Sort-Object LastWriteTime -Descending | Select-Object -Skip 20 |
            Remove-Item -Force -ErrorAction SilentlyContinue
        $path = Join-Path $LogDir ('{0}-{1:yyyyMMdd-HHmmss}.log' -f $Kind, (Get-Date))
        Start-Transcript -Path $path -Force | Out-Null
        return $path
    } catch {
        # A log that cannot be written must never stop the installation.
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

function New-AppShortcut {
    param([string] $Path)
    $shell = New-Object -ComObject WScript.Shell
    try {
        $shortcut = $shell.CreateShortcut($Path)
        $shortcut.TargetPath       = $InstalledExe
        $shortcut.WorkingDirectory = $InstallDir
        $shortcut.IconLocation     = "$InstalledExe,0"
        $shortcut.Description      = 'Digitize antenna diagram images'
        $shortcut.Save()
    } finally {
        [void][Runtime.InteropServices.Marshal]::ReleaseComObject($shell)
    }
}

# --- Checks that need no log: nothing has been touched yet ---------------------------------------------

$sourceExe = Join-Path $AppSource $ExecutableName
if (-not (Test-Path -LiteralPath $sourceExe)) {
    Write-Host "INSTALL FAILED: $sourceExe not found. Run this script from the package root, next to app\." -ForegroundColor Red
    exit 1
}
$version = (Get-Item -LiteralPath $sourceExe).VersionInfo.FileVersion

if ($WhatIfOnly) {
    Write-Host "Would install AntennaReader $version :"
    Write-Host "  application -> $InstallDir  (from $AppSource)"
    Write-Host "  shortcuts   -> $(Join-Path $StartMenuDir $ShortcutName)"
    Write-Host "                 $(Join-Path $DesktopDir $ShortcutName)"
    Write-Host "  log         -> $LogDir"
    exit 0
}

# Only the default location needs this check; a custom -InstallDir is a test run on a non-admin machine.
if (-not $PSBoundParameters.ContainsKey('InstallDir') -and -not (Test-Elevated)) {
    Write-Host 'INSTALL FAILED: run this elevated (as administrator or SYSTEM). Nothing was changed.' -ForegroundColor Red
    exit 1
}

# --- The installation ---------------------------------------------------------------------------------

$exitCode = 1
$logPath = Start-SetupLog 'Install'
try {
    Write-Host "Installing AntennaReader $version"
    Write-Host "  from $AppSource"
    Write-Host "  to   $InstallDir"

    if (Test-Path -LiteralPath $InstalledExe) {
        Write-Host "Existing installation found ($((Get-Item -LiteralPath $InstalledExe).VersionInfo.FileVersion)); closing it if it is running ..."
        Stop-RunningApp
    }

    Write-Host 'Copying files ...'
    New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
    # /MIR makes the folder identical to app\, so an update also removes files the new version no longer has.
    # /R:2 /W:2 because the default is a million retries 30 seconds apart: a locked file would hang the
    # deployment for days instead of failing in seconds.
    $robocopyOutput = & robocopy.exe $AppSource $InstallDir /MIR /XJ /R:2 /W:2 /NDL /NP /NJH /NJS
    $robocopyOutput | ForEach-Object { if ($_.Trim()) { Write-Host "  $($_.Trim())" } }
    # robocopy: below 8 means success (the number says what was copied), 8 and above means failure.
    if ($LASTEXITCODE -ge 8) { throw "robocopy failed with exit code $LASTEXITCODE." }
    $global:LASTEXITCODE = 0

    Write-Host 'Creating shortcuts ...'
    foreach ($dir in $StartMenuDir, $DesktopDir) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
        $shortcutPath = Join-Path $dir $ShortcutName
        New-AppShortcut -Path $shortcutPath
        Write-Host "  $shortcutPath"
    }

    Write-Host "Installed AntennaReader $version." -ForegroundColor Green
    $exitCode = 0
} catch {
    # Workspace ONE keeps only the exit code. Put the reason and the failing line in the log.
    Write-Host "INSTALL FAILED: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host $_.ScriptStackTrace
} finally {
    Write-Host "Log: $logPath"
    try { Stop-Transcript | Out-Null } catch { }
}
exit $exitCode
