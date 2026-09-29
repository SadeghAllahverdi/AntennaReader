# Deploying AntennaReader with Workspace ONE

Everything needed to turn the source into a package that Workspace ONE can install on managed laptops.
None of it changes the application's code.

## Why a package at all

Company laptops set SmartScreen to **Block**. A downloaded, unsigned exe (like the GitHub release zip)
gets "Windows protected your PC" with no "Run anyway" button. Files installed by the Workspace ONE agent
are not marked as downloaded, so SmartScreen does not stop them.

## What is in this folder

| File | Purpose |
| --- | --- |
| `Install-AntennaReader.ps1` | Installs to `C:\Program Files\UPLINK\AntennaReader`, adds Start Menu and desktop shortcuts |
| `Uninstall-AntennaReader.ps1` | Removes the app and shortcuts; keeps every user's saved diagrams |
| `AntennaReader.ico` | The logo. The publish profile builds it into the exe |
| `AntennaReader.png` | The same logo at 256 px, for the app icon in the Workspace ONE console |
| `out\` | Created by each publish; not in git |

The publish settings live in `AntennaReader\Properties\PublishProfiles\WorkspaceONE.pubxml`, because that
is the only place Visual Studio looks for them.

## Publish

**Visual Studio:** right-click the **AntennaReader** project > **Publish** > pick **WorkspaceONE** > **Publish**.

**Terminal (VS Code):**

```powershell
dotnet publish AntennaReader/AntennaReader.csproj -c Release -p:PublishProfile=WorkspaceONE
```

Both produce:

```
deployment\out\
  package\                                what the zip contains
    app\AntennaReader.exe                 the application (plus its .pdb)
    Install-AntennaReader.ps1
    Uninstall-AntennaReader.ps1
  AntennaReader-1.1.3-WorkspaceONE.zip    upload this
```

The last lines of the build output print the zip's path and the SHA256 of `AntennaReader.exe`.

To try the build on your own laptop, run `deployment\out\package\app\AntennaReader.exe`. You built it
yourself, so SmartScreen does not block it.

## Releasing a new version

1. Change `<Version>` at the top of `WorkspaceONE.pubxml` (for example `1.1.4`), same as the git tag.
2. Publish as above.
3. In Workspace ONE, add the new zip as a **new version of the existing app**. The commands stay the same;
   update the version in the detection rule.

The GitHub workflow (`.github/workflows/release.yml`) is separate and unchanged. It still builds the
download zip when you push a `v*` tag.

## Workspace ONE settings

| Setting | Value |
| --- | --- |
| Install command | `powershell.exe -ExecutionPolicy Bypass -NoProfile -File .\Install-AntennaReader.ps1` |
| Uninstall command | `powershell.exe -ExecutionPolicy Bypass -NoProfile -File .\Uninstall-AntennaReader.ps1` |
| Install context | Device (runs as SYSTEM) |
| Detection | File exists: `C:\Program Files\UPLINK\AntennaReader\AntennaReader.exe`, version `1.1.3.0` |
| Privilege elevation rule | **None needed.** The app runs as the normal user |

The zip has no folder at its root, so the commands have no path prefix. If the zip is ever rebuilt with a
folder around its contents, both commands must include that folder, or they fail within a second with exit code 1.

Exit codes: `0` success, `1` failure. The reason is in the log.

## What install and uninstall do

**Install:**
1. If AntennaReader is running from the install folder, closes it. Windows cannot replace a running exe.
   An unsaved diagram in that window is lost.
2. Copies `app\` to `C:\Program Files\UPLINK\AntennaReader`, replacing the previous version exactly.
3. Creates `AntennaReader` shortcuts in the Start Menu and on the desktop for all users.

**Uninstall:**
1. Closes the app the same way.
2. Removes the two shortcuts (only if they point at the installed exe).
3. Deletes `C:\Program Files\UPLINK\AntennaReader`. Removes `C:\Program Files\UPLINK` only if it is empty.
4. **Keeps** every user's `%LOCALAPPDATA%\AntennaReader`: their database, images and exports.

Neither script ever changes the permissions on `C:\Program Files\UPLINK`. The Network Configuration Tool,
also installed there, checks that folder's permissions and would go read-only if they changed.

## Logs

```
C:\ProgramData\UPLINK\Logs\AntennaReader\Install-<date>-<time>.log
C:\ProgramData\UPLINK\Logs\AntennaReader\Uninstall-<date>-<time>.log
```

The newest 20 are kept. They have their own subfolder because the Network Configuration Tool trims
`C:\ProgramData\UPLINK\Logs` to its newest 20 files and would delete these.

## Trying the scripts without admin rights

A dry run shows what would happen and changes nothing:

```powershell
.\Install-AntennaReader.ps1 -WhatIfOnly
.\Uninstall-AntennaReader.ps1 -WhatIfOnly
```

For a real run without admin rights, point every location at a test folder. Use a normal full path:
`%TEMP%` can be the short form (`C:\Users\SADEGH~1\...`), and then the uninstall does not recognise the
shortcuts and the running app as its own. The real `C:\Program Files` paths never have this problem.

```powershell
$t = "$env:USERPROFILE\ar-test"
.\Install-AntennaReader.ps1   -InstallDir "$t\UPLINK\AntennaReader" -StartMenuDir "$t\StartMenu" -DesktopDir "$t\Desktop" -LogDir "$t\Logs"
.\Uninstall-AntennaReader.ps1 -InstallDir "$t\UPLINK\AntennaReader" -StartMenuDir "$t\StartMenu" -DesktopDir "$t\Desktop" -LogDir "$t\Logs"
```

Run these from `deployment\out\package\`, where `app\` sits next to the scripts.

## Good to know

- **Each user has their own data.** The database lives in `%LOCALAPPDATA%\AntennaReader`, so colleagues do
  not see each other's diagrams.
- **First start is a little slower.** The exe unpacks its OpenCV and SQLite libraries into the user's
  `%TEMP%\.net\AntennaReader\` the first time, the same as the GitHub release.
- **The build is not code-signed.** That is fine for Workspace ONE deployment, but a copy downloaded
  through a browser will still be blocked by SmartScreen.
- **A .NET security update means republishing.** The .NET runtime is bundled inside the exe.
