# Phase 0 — Rule-Based CLI: Implementation Plan

## Goal

Build a self-contained .NET console application that a user runs on demand to inspect running processes and Windows services, match them against a user-defined rule file, and optionally take action (stop, kill, or suspend) on matched targets.

---

## Solution Structure

```
SystemFitnessHelper.sln
│
├── src/
│   ├── Cli/                          # Console entry point
│   └── Core/                         # All domain logic (reusable in Phase 1+)
│
└── tests/
    ├── Core.Tests/                   # Unit tests for Core
    └── Cli.Tests/                    # Unit tests for CLI command handlers
```

### Projects

| Project | Type | Purpose |
|---|---|---|
| `SystemFitnessHelper.Core` | Class library | Domain logic: fingerprinting, matching, action engine, config loading, safety guards |
| `SystemFitnessHelper.Cli` | Console app | Entry point, command definitions, output formatting |
| `SystemFitnessHelper.Core.Tests` | xUnit test project | Unit tests for Core |
| `SystemFitnessHelper.Cli.Tests` | xUnit test project | Unit tests for CLI command handlers |

Keeping all domain logic in `Core` is essential — Phase 1 will host the same library inside a Windows Service without touching `Cli`.

---

## Folder & File Layout

```
src/
│
├── Core/
│   ├── SystemFitnessHelper.Core.csproj
│   │
│   ├── Configuration/
│   │   ├── RuleSet.cs               # Root deserialization model for the JSON config file
│   │   ├── Rule.cs                  # Single rule: name, fingerprint conditions, action, enabled flag
│   │   ├── FingerprintCondition.cs  # One condition clause evaluated against a ProcessFingerprint
│   │   ├── ActionType.cs            # Enum: Stop | Kill | Suspend | None
│   │   ├── ConfigurationLoader.cs   # Reads, deserializes, and validates rules.json
│   │   └── ConfigurationBuilder.cs  # Builds a RuleSet template from a live process/service snapshot
│   │
│   ├── Fingerprinting/
│   │   ├── ProcessFingerprint.cs    # Immutable descriptor for a running process or service
│   │   ├── IProcessScanner.cs       # Interface: enumerate processes/services → IReadOnlyList<ProcessFingerprint>
│   │   └── WindowsProcessScanner.cs # Implementation using System.Diagnostics + System.ServiceProcess
│   │
│   ├── Matching/
│   │   ├── IRuleMatcher.cs          # Interface: given a fingerprint and rule set, return matched rules
│   │   ├── RuleMatcher.cs           # Evaluates FingerprintCondition expressions (regex, equality, range)
│   │   └── MatchResult.cs           # Pairs a ProcessFingerprint with the Rule(s) that matched it
│   │
│   ├── Actions/
│   │   ├── IActionExecutor.cs       # Interface: execute an action against a ProcessFingerprint
│   │   ├── WindowsActionExecutor.cs # Implementation: sc stop, Process.Kill, NtSuspendProcess
│   │   ├── ActionPlan.cs            # Describes a pending action (fingerprint + action type)
│   │   └── ActionResult.cs          # Outcome of executing one ActionPlan (success/failure + detail)
│   │
│   ├── Safety/
│   │   ├── SafetyGuard.cs           # Checks an ActionPlan against the protected-services list
│   │   └── ProtectedServices.cs     # Hard-coded + user-extensible list of services never to touch
│   │
│   └── Logging/
│       └── LoggingConfiguration.cs  # Serilog setup: console sink + rolling file sink
│
└── Cli/
    ├── SystemFitnessHelper.Cli.csproj
    ├── Program.cs                   # Builds DI container, wires System.CommandLine root command
    │
    └── Commands/
        ├── ConfigCommand.cs         # `sfh config` — load, validate, and pretty-print the rule file
        ├── ListCommand.cs           # `sfh list` — enumerate all processes, highlight matched ones
        ├── ActionsCommand.cs        # `sfh actions` — dry-run: show what would be done and why
        └── ExecuteCommand.cs        # `sfh execute` — run actions, print result for each target
```

```
tests/
│
├── Core.Tests/
│   ├── SystemFitnessHelper.Core.Tests.csproj
│   ├── Configuration/
│   │   └── ConfigurationLoaderTests.cs
│   ├── Fingerprinting/
│   │   └── WindowsProcessScannerTests.cs
│   ├── Matching/
│   │   └── RuleMatcherTests.cs
│   ├── Actions/
│   │   └── ActionExecutorTests.cs
│   └── Safety/
│       └── SafetyGuardTests.cs
│
└── Cli.Tests/
    ├── SystemFitnessHelper.Cli.Tests.csproj
    └── Commands/
        ├── ConfigCommandTests.cs
        ├── ListCommandTests.cs
        ├── ActionsCommandTests.cs
        └── ExecuteCommandTests.cs
```

---

## Component Design

### Configuration

**`rules.json`** — user-edited file, discovered from:
1. Path passed via `--config` option
2. `%APPDATA%\SystemFitnessHelper\rules.json`
3. Alongside the executable

Rules can identify a target by any combination of three names:
- **`ServiceDisplayName`** — the human-readable name shown in Control Panel / Services MMC (e.g. `Steam Client Service`)
- **`ServiceName`** — the internal SCM identifier (e.g. `SteamClientService`)
- **`ProcessName`** / **`ExecutablePath`** — the executable backing the process (e.g. `steamservice.exe`)

Using `ServiceDisplayName` is the most user-friendly option; `ServiceName` is more stable across OS versions; `ProcessName` is the fallback for plain processes that are not registered as services.

Example schema: see [sample-config0.json](sample-config0.json).

**`ConfigurationLoader`** — deserializes the file, validates required fields, checks for duplicate rule IDs, and surfaces a typed `ValidationResult` (list of errors/warnings) without throwing.

**`ConfigurationBuilder`** — given the result of `WindowsProcessScanner.Scan()`, produces a `RuleSet` template with one disabled `Rule` per fingerprint. Each rule has a unique sequential ID (`rule-0001`, `rule-0002`, …), a human-readable description (service display name for services, process name for plain processes), a single `FingerprintCondition` matching on `ServiceName` (for services) or `ProcessName` (for plain processes), and `Action` set to `Stop` for services or `Kill` for plain processes. All rules have `Enabled = false` so users can review and selectively enable them before applying. This class is the foundation for the `--output json` option on `sfh list` and for future UI-based configuration builders.

---

### Fingerprinting

**`ProcessFingerprint`** — immutable record capturing:

| Field | Source |
|---|---|
| `ProcessId` | `Process.Id` |
| `ProcessName` | `Process.ProcessName` |
| `ExecutablePath` | `Process.MainModule.FileName` |
| `CommandLine` | WMI `Win32_Process.CommandLine` |
| `Publisher` | `FileVersionInfo.CompanyName` |
| `WorkingSetBytes` | `Process.WorkingSet64` |
| `ParentProcessName` | WMI `Win32_Process.ParentProcessId` lookup |
| `IsService` | `true` when the process hosts one or more Windows services |
| `ServiceName` | Internal SCM name (e.g. `SteamClientService`) — `null` for plain processes |
| `ServiceDisplayName` | Human-readable SCM name as shown in Control Panel (e.g. `Steam Client Service`) — `null` for plain processes |
| `ServiceStatus` | `ServiceController.Status` — `null` for plain processes |

`IsService` is the authoritative flag for deciding how to act on a target. A process can host multiple services (e.g. `svchost`); in that case multiple fingerprints are produced — one per service — all sharing the same `ProcessId`.

**`IProcessScanner`** — single method: `IReadOnlyList<ProcessFingerprint> Scan()`. The interface lets tests inject a stub without touching real OS APIs.

**`WindowsProcessScanner`** — builds the fingerprint list in two passes:

1. **Process pass** — call `Process.GetProcesses()` to get every running process; build a base fingerprint for each.
2. **Service pass** — call `ServiceController.GetServices()` to get all registered Windows services; for each running service, query WMI `Win32_Service` to resolve its hosting `ProcessId`, then annotate the matching fingerprint with `ServiceName`, `ServiceDisplayName`, `ServiceStatus`, and set `IsService = true`. Services hosted inside a shared `svchost` instance each produce their own fingerprint entry even though they share a PID.

The two passes are independent — no matching by executable path is required. Falls back gracefully when elevated access is unavailable (marks restricted fields as `null` rather than throwing).

---

### Matching

**`FingerprintCondition`** — evaluates one predicate against a `ProcessFingerprint`:

| `op` | Behaviour |
|---|---|
| `eq` | Case-insensitive string equality |
| `neq` | Negated equality |
| `regex` | `Regex.IsMatch` against the field value |
| `gt` / `lt` | Numeric comparison (useful for memory thresholds) |

**`RuleMatcher`** — for each fingerprint, iterates enabled rules and evaluates their conditions according to `conditionLogic` (`And` / `Or`, defaulting to `And`). Returns a list of `MatchResult` (fingerprint + matched rule pairs).

---

### Action Engine

**`ActionPlan`** — value type combining a `ProcessFingerprint` and the `ActionType` from the matched rule.

**`IActionExecutor.Execute(ActionPlan)`** → `ActionResult` containing:
- `Success` (bool)
- `Message` (human-readable outcome)
- `Exception` (if failed)

**`WindowsActionExecutor`** — dispatches based on both `ActionType` and `ProcessFingerprint.IsService`:

| Target type | Allowed actions | Mechanism |
|---|---|---|
| Service (`IsService = true`) | `Stop` only | `ServiceController.Stop()`, waits for `Stopped` status (configurable timeout) |
| Plain process (`IsService = false`) | `Kill`, `Suspend` | `Kill` → `Process.Kill(entireProcessTree: true)`; `Suspend` → P/Invoke `NtSuspendProcess` |

**Services must never be killed directly.** Killing a service host process (`svchost`, a dedicated service exe) bypasses SCM, leaves the service in an inconsistent state, and can corrupt dependent services. If a rule specifies `Kill` on a fingerprint where `IsService = true`, the executor rejects the action and returns an `ActionResult` with `Success = false` and a clear message directing the user to use `Stop` instead. `ConfigurationLoader` also emits a validation warning for such rules so the user is notified at config-check time.

All actions are guarded by `SafetyGuard` before execution. Any action blocked by the guard returns an `ActionResult` with `Success = false` and a clear reason.

---

### Safety Guards

**`SafetyGuard.IsAllowed(ActionPlan)`** — returns `false` (with a reason string) if:
- The target service name is in the protected list (hard-coded or from `rules.json`)
- The process is a known Windows system process (`System`, `Registry`, `smss`, `csrss`, `wininit`, `services`, `lsass`, `svchost` hosting critical services)

The hard-coded list is the last line of defence; the user-configurable list in `rules.json` extends it.

---

### CLI Commands

Built with **System.CommandLine**. Each command:
1. Resolves dependencies from the DI container
2. Calls into `Core` services
3. Formats output to stdout using **Spectre.Console** (tables, coloured text)
4. Returns an exit code (0 = success, 1 = error, 2 = validation failure)

#### `sfh config [--config <path>]`

1. Locate and load `rules.json` via `ConfigurationLoader`
2. Print a formatted table of all rules (ID, description, enabled, conditions, action)
3. Print any validation errors/warnings below the table
4. Exit 2 if validation errors exist, 0 otherwise

#### `sfh list [--config <path>] [--output <type>]`

1. Scan processes via `IProcessScanner`
2. With `--output console` (default): load config, run `RuleMatcher`, print all processes in a table; rows with a match are highlighted (yellow = matched, red = matched + destructive action)
3. With `--output json`: skip config and rule matching; instead pass the fingerprint list to `ConfigurationBuilder.Build()` and print the resulting `RuleSet` as indented JSON. This is intended for copy-pasting as a starting configuration — all generated rules are disabled and must be reviewed before use.

#### `sfh actions [--config <path>]`

1. Load config, scan, match
2. Build `ActionPlan` for each match
3. Check each plan against `SafetyGuard`
4. Print a table: process name, service name, matched rule, planned action, blocked (yes/no + reason)
5. No changes made; exit 0

#### `sfh execute [--config <path>] [--yes]`

1. Same as `actions` to build the plan list
2. Without `--yes`: print the plan table and prompt "Proceed? [y/N]"
3. With `--yes`: skip the prompt (suitable for scripts)
4. Execute each allowed `ActionPlan` via `IActionExecutor`
5. Print a result row per action: target, action, outcome (success/failure + detail)
6. Exit 1 if any action failed, 0 if all succeeded

---

### Logging

**`LoggingConfiguration.Configure()`** — called once at startup, sets up Serilog with:
- **Console sink** — `Information` and above, plain-text
- **Rolling file sink** — `%APPDATA%\SystemFitnessHelper\logs\sfh-.log`, daily rotation, 7-day retention
- **Minimum level** — `Information` by default; overridable via `--verbose` flag (`Debug`)

Every `ActionResult` (success or failure) is written to the file sink at `Information` level, regardless of console verbosity.

---

## NuGet Dependencies

| Package | Used in | Purpose |
|---|---|---|
| `System.CommandLine` | Cli | Command/option/argument parsing |
| `Spectre.Console` | Cli | Coloured tables and prompts |
| `Serilog` | Core | Structured logging |
| `Serilog.Sinks.Console` | Core | Console log output |
| `Serilog.Sinks.File` | Core | Rolling file log output |
| `Microsoft.Extensions.DependencyInjection` | Cli | DI container wiring |
| `xUnit` | Tests | Test framework |
| `Moq` | Tests | Mocking `IProcessScanner`, `IActionExecutor` |
| `FluentAssertions` | Tests | Readable test assertions |

---

## Implementation Steps

1. **Scaffold solution** — create `SystemFitnessHelper.sln`, add the four projects, set `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` and `<Nullable>enable</Nullable>` in a `Directory.Build.props`
2. **Configuration layer** — implement `Rule`, `RuleSet`, `FingerprintCondition`, `ConfigurationLoader`; write unit tests
3. **Fingerprinting layer** — implement `ProcessFingerprint`, `IProcessScanner`, `WindowsProcessScanner`; write unit tests with a stub scanner
4. **Matching layer** — implement `RuleMatcher`, all condition operators; write unit tests covering `And`/`Or` logic and each operator
5. **Safety layer** — implement `SafetyGuard`, `ProtectedServices`; write unit tests for blocked and allowed cases
6. **Action layer** — implement `ActionPlan`, `ActionResult`, `IActionExecutor`, `WindowsActionExecutor`; write unit tests using a mock executor
7. **Logging** — wire Serilog in `LoggingConfiguration`
8. **CLI entry point** — wire DI in `Program.cs`, register System.CommandLine root command
9. **CLI commands** — implement `ConfigCommand`, `ListCommand`, `ActionsCommand`, `ExecuteCommand`; write unit tests for each
10. **End-to-end smoke test** — run `sfh list` and `sfh actions` against a real machine; verify output and that no actions are taken unintentionally
