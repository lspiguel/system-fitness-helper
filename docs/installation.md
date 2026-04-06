# Installation Guide — System Fitness Helper Service

## Prerequisites

- Windows 10 / Windows Server 2016 or later
- .NET 8.0 Runtime (Desktop + Windows)
- Administrator privileges (required for service registration)

---

## Build

From the repository root, publish a self-contained release of both the service and the installer:

```powershell
dotnet publish src/Service/SystemFitnessHelper.Service.csproj `
    -c Release -r win-x64 --self-contained false `
    -o publish/service

dotnet publish src/Installer/SystemFitnessHelper.Installer.csproj `
    -c Release -r win-x64 --self-contained false `
    -o publish/installer
```

Both output directories must be on the same path when you run the installer, because `sfhi install` copies all files from its own directory into the service install location (except the `sfhi` binary itself).

> **Tip:** publish the installer into `publish/service` so that a single folder contains both `sfhi.exe` and `SystemFitnessHelper.Service.exe` — then run `sfhi` from there.

---

## Install

Open an **elevated** (Administrator) command prompt or PowerShell window.

```powershell
cd publish\service
.\sfhi.exe install
```

What this does:

1. Copies all binaries from the current directory to  
   `C:\Program Files\SystemFitnessHelper\Service\`
2. Creates the config directory  
   `C:\ProgramData\SystemFitnessHelper\`
3. Writes a default `rules.json` (if one does not already exist)
4. Registers the Windows Service with the SCM  
   (`sc create SystemFitnessHelper binPath= "..." start= auto`)

Expected output:

```
Installing service to: C:\Program Files\SystemFitnessHelper\Service
Created default config at: C:\ProgramData\SystemFitnessHelper\rules.json
Service 'SystemFitnessHelper' installed successfully.
Install path: C:\Program Files\SystemFitnessHelper\Service
Config path:  C:\ProgramData\SystemFitnessHelper\rules.json
Run 'sfhi start' to start the service.
```

---

## Start the service

```powershell
.\sfhi.exe start
```

Expected output:

```
Service started.
```

---

## Uninstall

To remove the service registration only (binaries are kept):

```powershell
.\sfhi.exe uninstall
```

To also delete the installed binaries:

```powershell
.\sfhi.exe uninstall --remove-files
```

---

## Configuration

The service reads rules from  
`C:\ProgramData\SystemFitnessHelper\rules.json`

This path can be overridden with the `SFH_CONFIG_PATH` environment variable before starting the service.

The `sfhcli` tool uses the per-user path (`%APPDATA%`) by default. To point it at the service config, pass `--config "C:\ProgramData\SystemFitnessHelper\rules.json"` (or set `SFH_CONFIG_PATH`).

---

## Testing the installation

### Step 1 — Verify the service is installed

```powershell
.\sfhi.exe status
```

Expected output:

```
Service status: Stopped
Config path:    C:\ProgramData\SystemFitnessHelper\rules.json
Config exists:  True
```

You can also verify via the SCM:

```powershell
sc.exe query SystemFitnessHelper
```

### Step 2 — Start the service and confirm it is running

```powershell
.\sfhi.exe start
.\sfhi.exe status
```

Expected `status` output:

```
Service status: Running
Config path:    C:\ProgramData\SystemFitnessHelper\rules.json
Config exists:  True
```

### Step 3 — Verify via Services console

Open `services.msc`. Locate **System Fitness Helper** in the list.  
The **Status** column should show **Running** and **Startup Type** should be **Automatic**.

### Step 4 — Verify named pipes are open

The service exposes two named pipes. Confirm they exist while the service is running:

```powershell
[System.IO.Directory]::GetFiles('\\.\pipe\') | Select-String 'sfh'
```

Expected output includes:

```
\\.\pipe\sfh-command
\\.\pipe\sfh-events
```

### Step 5 — Send a command via the CLI

Use `sfhcli` to confirm the service responds to IPC requests. The `list` command sends a `sfh.list` JSON-RPC request over the `sfh-command` pipe:

```powershell
sfhcli list --config "C:\ProgramData\SystemFitnessHelper\rules.json"
```

If the service is running and the config is valid, this returns the current process list.

### Step 6 — Stop the service

```powershell
.\sfhi.exe stop
.\sfhi.exe status
```

Expected `status` output:

```
Service status: Stopped
Config path:    C:\ProgramData\SystemFitnessHelper\rules.json
Config exists:  True
```

### Step 7 — Start idempotency check

Run `start` twice in a row; the second call should be a no-op:

```powershell
.\sfhi.exe start
.\sfhi.exe start
```

Expected second output:

```
Service is already running.
```

### Step 8 — Uninstall and verify removal

```powershell
.\sfhi.exe uninstall --remove-files
sc.exe query SystemFitnessHelper
```

`sc.exe query` should return:

```
[SC] EnumQueryServicesStatus:OpenService FAILED 1060:

The specified service does not exist as an installed service.
```
