# Installer Test Plan

Manual verification for both installers. Every step below has been executed against a real
installation; the notes record what each one is actually guarding against, because most of these
steps exist because something failed there.

- **[`sfhi` plan](#before-you-start)** — the development installer, steps 1–16.
- **[MSI plan](#msi-test-plan)** — the shipping package, steps M1–M13.

Each sequence takes about ten minutes and leaves the machine as it found it. They are independent;
run whichever installer you changed.

## Before you start

Record the starting state so you can confirm the restore at the end:

```powershell
sc.exe query SystemFitnessHelper                      # expect 1060, not installed
Test-Path 'C:\Program Files\SystemFitnessHelper'      # expect False
Get-ChildItem 'C:\ProgramData\SystemFitnessHelper' -Recurse -ErrorAction SilentlyContinue
```

If `C:\ProgramData\SystemFitnessHelper\rules.json` already exists, **back it up** — step 9 writes
to it:

```powershell
$bk = "$env:TEMP\sfh-backup-$(Get-Date -Format yyyyMMddHHmmss)"
New-Item -ItemType Directory $bk -Force | Out-Null
Copy-Item 'C:\ProgramData\SystemFitnessHelper\*' $bk -Recurse -Force
```

---

## 1. Build

```powershell
.\build.ps1
```

**Pass:** build and all tests succeed; `publish\` contains `sfhi.exe`, `rules.sample.json` and
the `Service\`, `TrayApp\`, `Ui\` directories.

---

## 2. Install

From an **elevated** prompt:

```powershell
cd publish
.\sfhi.exe install
```

**Pass:** exit code 0; output names the install path, config path and dashboard path.

> Guards against: a payload layout the installer cannot read. The layout is validated before
> anything is copied, so a wrong working directory fails cleanly rather than half-installing.

---

## 3. Registration

```powershell
.\sfhi.exe status
(Get-ItemProperty 'HKLM:\SYSTEM\CurrentControlSet\Services\SystemFitnessHelper').ImagePath
Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\SystemFitnessHelper' |
    Select-Object DisplayName, DisplayVersion, UninstallString
(Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run').SystemFitnessHelperTray
Get-ChildItem 'C:\ProgramData\Microsoft\Windows\Start Menu\Programs\System Fitness Helper'
(Get-CimInstance Win32_Service -Filter "Name='SystemFitnessHelper'").StartName
```

**Pass:**
- status reports `Stopped`
- **`ImagePath` is quoted** — `"C:\Program Files\...\SystemFitnessHelper.Service.exe"`
- the Apps & Features entry exists
- the Run value points at `TrayApp\SystemFitnessHelper.TrayApp.exe`
- two Start Menu shortcuts exist
- the service account is `LocalSystem`

> Guards against: an unquoted `ImagePath`. `sc.exe` stores the value verbatim, so a bare path
> containing spaces leaves the classic unquoted-service-path weakness.

---

## 4. Start

```powershell
.\sfhi.exe start
.\sfhi.exe status
```

**Pass:** status reports `Running`, and the health probe responds with a version and a rules file
matching the config path above. No mismatch warning.

---

## 5. Installed installer runs

```powershell
& 'C:\Program Files\SystemFitnessHelper\sfhi.exe' status
```

**Pass:** exit code 0.

> Guards against: copying `sfhi.exe` without its dependencies. This command *is* the Apps &
> Features `UninstallString`; if it cannot start, uninstalling from Windows Settings is broken.

---

## 6. Named pipes

```powershell
[System.IO.Directory]::GetFiles('\\.\pipe\') | Where-Object { $_ -match 'sfh' }
```

**Pass:** both `\\.\pipe\sfh-command` and `\\.\pipe\sfh-events` are listed.

---

## 7. Non-elevated pipe access — the critical check

From an **ordinary, non-elevated** PowerShell:

```powershell
$p = New-Object System.IO.Pipes.NamedPipeClientStream('.', 'sfh-command', 'InOut')
try { $p.Connect(5000); 'connected' } catch { "FAILED: $($_.Exception.GetType().Name)" }
$p.Dispose()
```

**Pass:** `connected`.

> Guards against: pipes created with the default DACL. The service runs as LocalSystem; without
> an explicit ACL granting Authenticated Users read/write, every non-elevated client gets
> `UnauthorizedAccessException` and the whole product is unusable after a successful install.
> This never reproduces in development, where both ends run as the same interactive user.

---

## 8. Tray app and dashboard

From the **non-elevated** session, launch both:

```powershell
Start-Process 'C:\Program Files\SystemFitnessHelper\TrayApp\SystemFitnessHelper.TrayApp.exe'
Start-Process 'C:\Program Files\SystemFitnessHelper\Ui\SystemFitnessHelper.Ui.exe'
```

**Pass:**
- the tray icon appears and its menu items are **enabled** (they are disabled when the service is
  unreachable)
- the dashboard opens and the Processes, Actions and Configuration tabs all populate
- tray → **Open Dashboard** brings the existing window forward rather than starting a second copy

> Guards against: the tray resolving the dashboard at `..\Ui\SystemFitnessHelper.Ui.exe`. If the
> component layout is wrong, this is where it shows.

---

## 9. Configuration round-trip

In the dashboard's Configuration tab, change a rule's description and click **Save**. Then:

```powershell
Get-Item 'C:\ProgramData\SystemFitnessHelper\rules.json' | Select-Object LastWriteTime, Length
Test-Path -LiteralPath 'C:\Windows\System32\%ProgramData%'
```

**Pass:** `rules.json` was rewritten and is still valid JSON; the second command returns
**False**.

> Guards against: an unexpanded `%ProgramData%` in the configured path. A Windows Service starts
> in `C:\Windows\System32`, so a literal path creates a directory named `%ProgramData%` there and
> the user's edits vanish into it.

---

## 10. Concurrency

With the dashboard open and refreshing, run **Execute Now** from the tray menu. Then:

```powershell
Select-String -Path "C:\ProgramData\SystemFitnessHelper\logs\sfh-$(Get-Date -Format 'yyyyMMdd').log" `
    -Pattern '\[ERR\]|\[FTL\]|Pipe is broken'
```

**Pass:** both stay responsive; no matches in the log.

> Guards against: a single-instance command pipe handling requests inline. A long `sfh.execute`
> would block every other client past its connect timeout, filling the log with
> `IOException: Pipe is broken` as the server answered clients that had already given up.

---

## 11. Action events

**Pass:** a balloon notification appears for each action the execute performed.

> Guards against: the event pipe dropping subscribers. A disconnect monitor that reads from a
> write-only (`PipeDirection.Out`) stream throws `NotSupportedException` immediately, and if that
> is swallowed the client is torn down microseconds after connecting — so no notification is ever
> delivered.

To test this without acting on real services, point `sfh.execute` at a throwaway config via its
`configPath` parameter, with a single rule matching a process you started yourself.

---

## 12. Autostart

Sign out and back in.

**Pass:** the tray app is running without being launched manually.

---

## 13. Upgrade over a running install

```powershell
.\build.ps1
cd publish
.\sfhi.exe install
```

**Pass:** the installer reports stopping the running service, then *"already exists; updating its
configuration"* rather than failing; `rules.json` is unchanged.

> Guards against: `sc create` failing with 1073 *after* the files were copied, and against
> copying over a running service's locked binaries.

---

## 14. Non-elevated invocation

Close the dashboard and tray app first, then from an **ordinary** prompt:

```powershell
.\sfhi.exe uninstall --remove-files
```

**Pass:** a UAC prompt appears; the elevated window stays open until a key is pressed; the
original shell reports exit code 0.

> Guards against: an elevation relaunch that mangles quoted arguments, never waits for the child,
> returns a blind failure, and closes the child's console before any output can be read.

---

## 15. Clean removal

```powershell
sc.exe query SystemFitnessHelper                                   # expect 1060
Test-Path 'C:\Program Files\SystemFitnessHelper'                   # expect False
Test-Path 'C:\ProgramData\Microsoft\Windows\Start Menu\Programs\System Fitness Helper'  # False
(Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run').SystemFitnessHelperTray   # $null
Test-Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\SystemFitnessHelper'          # False
Get-ChildItem 'C:\ProgramData\SystemFitnessHelper'                 # still present
[System.IO.Directory]::GetFiles('\\.\pipe\') | Where-Object { $_ -match 'sfh' }   # nothing
```

**Pass:** everything removed **except** `C:\ProgramData\SystemFitnessHelper`, which is kept
because `--purge` was not passed.

---

## 16. Restore

If you backed up in step 0:

```powershell
$bk = Get-ChildItem "$env:TEMP\sfh-backup-*" -Directory | Sort-Object Name -Desc | Select-Object -First 1
Copy-Item (Join-Path $bk.FullName 'rules.json') 'C:\ProgramData\SystemFitnessHelper\rules.json' -Force
```

To verify `--purge` separately, reinstall and uninstall again with `--purge`, and confirm
`C:\ProgramData\SystemFitnessHelper` is gone.

---

## Isolated testing

To exercise the installer without touching a real installation:

```powershell
.\sfhi.exe install   --prefix C:\Temp\sfh-test --service-name SfhTest
.\sfhi.exe status    --service-name SfhTest
.\sfhi.exe uninstall --prefix C:\Temp\sfh-test --service-name SfhTest --remove-files
```

Note that the configuration directory is always `%ProgramData%\SystemFitnessHelper` regardless of
prefix, so an isolated install still shares the rules file with a real one.

---

# MSI Test Plan

Verification for `SystemFitnessHelper.msi`. Like the `sfhi` plan above, every step exists because
something failed there. Run it from an **elevated** prompt except where noted.

Back up `C:\ProgramData\SystemFitnessHelper\rules.json` first — step M7 replaces the config
directory to simulate a clean machine.

```powershell
.\build.ps1 -Msi
cd publish
```

## M1. Install

```powershell
msiexec /i SystemFitnessHelper.msi /qn /l*v install.log
```

**Pass:** exit code 0 (or 3010 for reboot-required).

> Because Windows Installer transacts the operation, a failure rolls back. If it does fail, the
> verbose log is the record — search it for `Return value 3`, which marks the failing action.

## M2. Installed to the 64-bit locations

```powershell
Test-Path 'C:\Program Files\SystemFitnessHelper'        # True
Test-Path 'C:\Program Files (x86)\SystemFitnessHelper'  # False
Test-Path 'HKLM:\SOFTWARE\WOW6432Node\SystemFitnessHelper'  # False
Get-ChildItem 'C:\Program Files\SystemFitnessHelper' -Recurse -Filter *.pdb   # nothing
```

**Pass:** as annotated.

> Guards against the package defaulting to x86, which installs under `Program Files (x86)` and has
> its HKLM writes redirected into `WOW6432Node` — putting the MSI and `sfhi` installations in
> different places.

## M3. `runtimes\` layout preserved

```powershell
Test-Path 'C:\Program Files\SystemFitnessHelper\runtimes\win\lib\net8.0'  # True
Test-Path 'C:\Program Files\SystemFitnessHelper\win'                      # False
& 'C:\Program Files\SystemFitnessHelper\sfhi.exe' status
```

**Pass:** the `runtimes` directory exists, there is no stray `win` directory, and `sfhi status`
runs and reports a healthy health probe.

> Guards against a `Files` harvest anchoring at `runtimes\` and flattening the prefix away.
> `sfhi.exe` resolves RID-specific assemblies through `runtimes\` and fails to start without it.

## M4. Service

```powershell
Get-CimInstance Win32_Service -Filter "Name='SystemFitnessHelper'" |
    Select-Object State, StartMode, StartName, PathName
```

**Pass:** `Running`, `Auto`, `LocalSystem`, and a **quoted** `PathName`.

## M5. All Users Start Menu

```powershell
Get-ChildItem 'C:\ProgramData\Microsoft\Windows\Start Menu\Programs\System Fitness Helper'
Test-Path "$env:APPDATA\Microsoft\Windows\Start Menu\Programs\System Fitness Helper"
```

**Pass:** two shortcuts in the common Start Menu; the per-user path does **not** exist.

> This is the empirical justification for suppressing ICE43 and ICE57. Both assume shortcuts in
> `ProgramMenuFolder` are per-user data; with `ALLUSERS=1` they are not. If this check ever shows
> shortcuts landing in a user profile, the suppression is wrong and must be revisited.

## M6. Registry and Apps & Features

```powershell
(Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run').SystemFitnessHelperTray
Get-ItemProperty 'HKLM:\SOFTWARE\SystemFitnessHelper' | Select-Object InstalledBy, InstallRoot
Get-ChildItem 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall' |
    ForEach-Object { Get-ItemProperty $_.PSPath } |
    Where-Object { $_.DisplayName -like '*Fitness*' } |
    Select-Object DisplayName, DisplayVersion, WindowsInstaller
```

**Pass:** the Run value points into `TrayApp\`; `InstalledBy` is `msi`; exactly one Apps &
Features entry with `WindowsInstaller = 1`.

## M7. Fresh-machine seeding

Move the config directory aside, install, and inspect what was seeded:

```powershell
Move-Item 'C:\ProgramData\SystemFitnessHelper' "$env:TEMP\sfh-stash"
msiexec /i SystemFitnessHelper.msi /qn
$cfg = Get-Content 'C:\ProgramData\SystemFitnessHelper\rules.json' -Raw | ConvertFrom-Json
$cfg.ruleSets.default.rules.Count                                  # 37
@($cfg.ruleSets.default.rules | Where-Object { $_.enabled }).Count # 0
& 'C:\Program Files\SystemFitnessHelper\sfhi.exe' status
```

**Pass:** the seed uses the **multi-ruleset** schema, carries its rules, has **none enabled**, and
the service parses it — confirm via `sfh.config` that the service reports the same rule count with
no validation errors.

> Guards against schema drift in `docs/sample-config1.json`. It was left in the pre-0.C
> single-ruleset shape, so every fresh install seeded a file that deserialised to zero rulesets.
> `SampleConfigSchemaTests` now fails the build if that recurs, but this step proves it end to end.

## M8. Non-elevated clients

From an **ordinary** shell, launch the tray app and dashboard from the install directory.

**Pass:** both connect; the tray menu items are enabled; all three dashboard tabs populate.

> Same pipe-ACL check as step 7 of the `sfhi` plan, repeated because the MSI installs to a
> different path and could in principle differ.

## M9. Configuration survives an upgrade

```powershell
$before = (Get-FileHash 'C:\ProgramData\SystemFitnessHelper\rules.json').Hash
.\build.ps1 -Msi -ProductVersion 1.1.0
msiexec /i publish\SystemFitnessHelper.msi /qn
(Get-FileHash 'C:\ProgramData\SystemFitnessHelper\rules.json').Hash -eq $before
```

**Pass:** `True`, one Apps & Features entry showing `1.1.0`, the service still running, and
`sfhi status` reporting a matching assembly version.

> The version check is not incidental: WiX judges itself up to date from source timestamps and
> ignores preprocessor constants, so a package can silently carry a stale `ProductVersion`.
> `build.ps1` forces `-t:Rebuild` to prevent it; this step confirms it worked.

## M10. `TRAYAUTOSTART=0`

```powershell
msiexec /x SystemFitnessHelper.msi /qn
msiexec /i SystemFitnessHelper.msi TRAYAUTOSTART=0 /qn
(Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run' -EA SilentlyContinue).SystemFitnessHelperTray
```

**Pass:** the Run value is absent, while the service and the Start Menu shortcuts are still
installed.

## M11. `sfhi` refuses to manage the MSI installation

```powershell
& 'C:\Program Files\SystemFitnessHelper\sfhi.exe' install      # exit 1, explains why
& 'C:\Program Files\SystemFitnessHelper\sfhi.exe' uninstall    # exit 1, explains why
& 'C:\Program Files\SystemFitnessHelper\sfhi.exe' status       # exit 0, works normally
& 'C:\Program Files\SystemFitnessHelper\sfhi.exe' status --service-name SfhTest   # exit 0
```

**Pass:** the two mutating verbs refuse and point at the MSI; read-only verbs work; an explicit
`--service-name` is allowed through as an isolated installation.

## M12. Uninstall

```powershell
msiexec /x SystemFitnessHelper.msi /qn
```

**Pass:** service gone, install root gone, Start Menu gone, `HKLM\SOFTWARE\SystemFitnessHelper`
gone, Run value gone, no Apps & Features entry, no `sfh-*` pipes — and
`C:\ProgramData\SystemFitnessHelper` **still present** with `rules.json` intact.

> The config is a permanent component on purpose. An installer should not delete configuration a
> user has invested time in.

## M13. Restore

```powershell
Remove-Item 'C:\ProgramData\SystemFitnessHelper' -Recurse -Force
Move-Item "$env:TEMP\sfh-stash" 'C:\ProgramData\SystemFitnessHelper'
```
