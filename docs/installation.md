# Installation Guide — System Fitness Helper

## Prerequisites

- Windows 10 / Windows Server 2016 or later
- [.NET 8.0 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) (the Desktop
  runtime, not just the console one — the tray app and dashboard are Windows Forms)
- Administrator privileges for `install` and `uninstall`

---

## Build

From the repository root:

```powershell
.\build.ps1
```

This builds the solution, runs the tests, and publishes every component into `publish\` in the
layout the installer expects:

```
publish\
├── sfhi.exe            the installer, with its dependencies
├── rules.sample.json   seed configuration
├── Service\            SystemFitnessHelper.Service.exe
├── TrayApp\            SystemFitnessHelper.TrayApp.exe
└── Ui\                 SystemFitnessHelper.Ui.exe
```

Options:

| Switch | Effect |
|---|---|
| `-Configuration Debug` | Build Debug instead of Release |
| `-SkipTests` | Skip the test run |
| `-OutputDir <path>` | Publish somewhere other than `publish\` |

Each component is published into its own directory. `sfhi install` copies the whole payload, so
do not rearrange it — the tray app locates the dashboard at `..\Ui\` relative to itself.

---

## Install

From an **elevated** PowerShell:

```powershell
cd publish
.\sfhi.exe install
```

If you run it without elevation, the installer relaunches itself through UAC, waits for that
copy to finish, and returns its exit code — so `.\sfhi.exe install; if ($?) { ... }` chains
correctly. The elevated window waits for a keypress so you can read what it did.

`install` performs these steps, in order:

1. Validates the payload layout before modifying anything.
2. Stops the service if it is already running (so an upgrade does not hit a sharing violation).
3. Copies the payload to `C:\Program Files\SystemFitnessHelper\`.
4. Creates `C:\ProgramData\SystemFitnessHelper\` and its `logs\` subdirectory.
5. Writes `rules.json` **only if it does not already exist**, seeded from `rules.sample.json`
   with every rule disabled.
6. Registers the service with a quoted `binPath`, `start= auto`, `obj= LocalSystem`, a
   description, and restart-on-failure actions. If the service already exists it is
   reconfigured rather than failing.
7. Creates Start Menu shortcuts for the dashboard and the tray app.
8. Registers the tray app to start at sign-in for all users.
9. Registers the product in Apps & Features.

Expected output:

```
Installing System Fitness Helper to: C:\Program Files\SystemFitnessHelper
  Copied Service, TrayApp, Ui and the installer
Created default config at: C:\ProgramData\SystemFitnessHelper\rules.json
Created Start Menu shortcuts in: C:\ProgramData\Microsoft\Windows\Start Menu\Programs\System Fitness Helper
Tray application registered to start for all users at sign-in.
Registered in Apps & Features.

Service 'SystemFitnessHelper' installed successfully.
  Install path: C:\Program Files\SystemFitnessHelper
  Config path:  C:\ProgramData\SystemFitnessHelper\rules.json
  Dashboard:    C:\Program Files\SystemFitnessHelper\Ui\SystemFitnessHelper.Ui.exe

Next: sfhi start
```

### Options

| Option | Applies to | Effect |
|---|---|---|
| `--prefix <path>` | install, uninstall | Install root instead of `%ProgramFiles%\SystemFitnessHelper` |
| `--service-name <name>` | all | SCM service name instead of `SystemFitnessHelper` |
| `--no-tray-autostart` | install | Do not start the tray app at sign-in |
| `--remove-files` | uninstall | Delete the installed binaries |
| `--purge` | uninstall | Also delete config and logs (implies `--remove-files`) |

`--prefix` and `--service-name` together let you install a second, isolated copy for testing
without disturbing a real installation. The configuration directory is always
`%ProgramData%\SystemFitnessHelper` regardless of prefix — it belongs to the machine, not to a
particular copy of the binaries.

---

## Start and verify

```powershell
.\sfhi.exe start
.\sfhi.exe status
```

`status` reports the SCM state, the registered image path, and — when the service is running —
asks the service itself over the command pipe:

```
Service name:   SystemFitnessHelper
Service status: Running
Image path:     "C:\Program Files\SystemFitnessHelper\Service\SystemFitnessHelper.Service.exe"
Install root:   C:\Program Files\SystemFitnessHelper (exists: True)
Config path:    C:\ProgramData\SystemFitnessHelper\rules.json (exists: True)
Log directory:  C:\ProgramData\SystemFitnessHelper\logs

Health probe (sfh.ping):
  Responding:  True
  Version:     1.0.0.0
  Rules file:  C:\ProgramData\SystemFitnessHelper\rules.json (exists: True)
```

If the **Rules file** reported by the probe differs from the **Config path**, `status` prints a
warning: the service is reading a different file than the installer wrote.

Then start the dashboard from the Start Menu, or:

```powershell
& "C:\Program Files\SystemFitnessHelper\Ui\SystemFitnessHelper.Ui.exe"
```

The tray app starts automatically at your next sign-in; to start it now, use the Start Menu
shortcut **System Fitness Helper Tray**.

> The tray app and dashboard run as your ordinary user account while the service runs as
> LocalSystem. That works because the service publishes its pipes with a DACL granting
> authenticated users read/write access. If they report "service not running" while `sfhi status`
> says Running, see [Access denied on the pipe](#access-denied-on-the-pipe).

---

## Upgrade

Re-run `install` over an existing installation:

```powershell
.\build.ps1
cd publish
.\sfhi.exe install
.\sfhi.exe start
```

The installer stops the running service, replaces the binaries, reconfigures the existing SCM
entry, and **leaves `rules.json` untouched**.

---

## Uninstall

```powershell
.\sfhi.exe uninstall                  # remove service, shortcuts and registry entries
.\sfhi.exe uninstall --remove-files   # also delete the installed binaries
.\sfhi.exe uninstall --purge          # also delete config and logs
```

Without `--purge`, `C:\ProgramData\SystemFitnessHelper` is kept and the installer says so.
Uninstalling also works from Apps & Features, which invokes the installed copy of `sfhi.exe`.

Close the dashboard and tray app first — a running executable under the install root blocks
deletion of its own directory.

---

## Configuration

The service reads and writes `C:\ProgramData\SystemFitnessHelper\rules.json`.

The path is resolved in this order:

1. The `SFH_CONFIG_PATH` environment variable, if set.
2. `ServiceConfig:ConfigPath` in `appsettings.json` in the service install directory, if set.
3. `%ProgramData%\SystemFitnessHelper\rules.json`.

Whatever the source, the value has environment variables expanded and is made absolute before
use. To override the path machine-wide:

```powershell
[System.Environment]::SetEnvironmentVariable("SFH_CONFIG_PATH", "D:\custom\rules.json", "Machine")
Restart-Service SystemFitnessHelper
```

Confirm it took effect with `sfhi status`, which prints the path the service actually resolved.

The service does not reload the file automatically. Either restart the service after editing
`rules.json` by hand, or edit through the dashboard's Configuration tab, which pushes changes
over the `sfh.config.save` IPC method and takes effect immediately.

The `sfhcli` command-line tool discovers its config separately: an explicit `--config`, then
`%ProgramData%`, then `%APPDATA%`, then `rules.json` next to the executable. It therefore picks
up the service's configuration by default.

---

## Log files

```
C:\ProgramData\SystemFitnessHelper\logs\sfh-<yyyyMMdd>.log
```

Daily rolling, 7 days retained. Console output is added when the service is run interactively
rather than under the SCM.

```powershell
# Tail today's log
Get-Content "C:\ProgramData\SystemFitnessHelper\logs\sfh-$(Get-Date -Format 'yyyyMMdd').log" -Wait

# Any errors at all?
Select-String -Path "C:\ProgramData\SystemFitnessHelper\logs\*.log" -Pattern '\[ERR\]|\[FTL\]'
```

A healthy service logs its resolved configuration at startup:

```
[INF] Using rules file: C:\ProgramData\SystemFitnessHelper\rules.json
[INF] Application started. Hosting environment: Production; Content root path: C:\Program Files\SystemFitnessHelper\Service\
```

A **Content root path** pointing into a source tree means you are looking at a development run,
not the installed service.

---

## Troubleshooting

### Service fails to start

```powershell
# What did the SCM say?
Get-WinEvent -FilterHashtable @{LogName='System'; ProviderName='Service Control Manager'} -MaxEvents 20 |
    Where-Object { $_.Message -like '*SystemFitnessHelper*' } | Format-List TimeCreated, Message

# What did the service say?
Get-Content "C:\ProgramData\SystemFitnessHelper\logs\sfh-$(Get-Date -Format 'yyyyMMdd').log" -Tail 40
```

To see startup errors directly, run the service executable as a console application — console
logging is enabled automatically when it is not running under the SCM:

```powershell
& "C:\Program Files\SystemFitnessHelper\Service\SystemFitnessHelper.Service.exe"
```

Stop the Windows service first; the two cannot hold the same pipe names at once.

### Access denied on the pipe

Symptom: `sfhi status` reports Running, but the tray app or dashboard says the service is not
running.

The service publishes `sfh-command` and `sfh-events` with a DACL granting **Authenticated Users**
read/write, **Administrators** and **LocalSystem** full control, and the creating account full
control. Check what is actually on the pipe:

```powershell
$p = New-Object System.IO.Pipes.NamedPipeClientStream('.', 'sfh-command', 'InOut')
try { $p.Connect(3000); "connected" } catch { $_.Exception.GetType().Name }
```

An `UnauthorizedAccessException` here means the pipe was created with a different DACL — most
likely another build of the service, or a stale process, is holding the name. Confirm only one
instance is running and that it came from the install directory.

### Named pipes missing

```powershell
[System.IO.Directory]::GetFiles('\\.\pipe\') | Where-Object { $_ -match 'sfh' }
```

Both `sfh-command` and `sfh-events` should be listed while the service runs. If they are not,
check the log for a pipe server startup error, and confirm no second copy of the service (for
example one started interactively for debugging) already owns the names.

### Dashboard cannot be opened from the tray

The tray app resolves the dashboard as `..\Ui\SystemFitnessHelper.Ui.exe` relative to its own
directory. If you moved the binaries, restore the component layout under a single root:

```
<install root>\TrayApp\SystemFitnessHelper.TrayApp.exe
<install root>\Ui\SystemFitnessHelper.Ui.exe
```

### `sc` reports 1072, "marked for deletion"

Something still holds a handle to the service — usually an open `services.msc`. The installer
retries automatically; if it still fails, close Services and re-run `uninstall`.

### Empty process list after a fresh install

Expected. `install` seeds every rule **disabled** so a new installation never stops anything
before you have reviewed it. Enable the rules you want in the dashboard's Configuration tab.
