# Phase 0.B — Service Layer & JSON Output: Implementation Plan

## Goal

Introduce a `Services` layer inside `Core` that encapsulates the orchestration logic currently embedded in each CLI command handler. Each service receives typed input parameters, delegates to the existing `Core` components (scanner, matcher, safety guard, executor), and returns a typed, JSON-serializable response — with no dependency on console I/O or Spectre.Console.

CLI commands are refactored into thin adapters: they call a service, then either render the result as a formatted Spectre.Console table (existing behaviour) or serialize it as indented JSON (new `--output json` global option). The service layer is the contract that Phase 1 will call over JSON-RPC; the CLI adapter is just one consumer of it.

---

## Solution Structure

No new projects are added. Changes are confined to existing projects.

```
SystemFitnessHelper.sln
│
├── src/
│   ├── Core/          ← new Services/ subfolder added
│   └── Cli/           ← commands refactored into thin adapters; --output global option added
│
└── tests/
    ├── Core.Tests/    ← new Services/ test subfolder added
    └── Cli.Tests/     ← command tests updated to mock service interfaces
```

### Project responsibilities after Phase 0.B

| Project | Before | After |
|---|---|---|
| `SystemFitnessHelper.Core` | Domain logic only | Domain logic + orchestration services + response types |
| `SystemFitnessHelper.Cli` | Command handlers embed all orchestration | Command handlers are thin adapters over service interfaces |
| `SystemFitnessHelper.Core.Tests` | Tests for domain components | Tests for domain components + service layer |
| `SystemFitnessHelper.Cli.Tests` | Tests mock low-level Core interfaces | Tests mock service interfaces |

---

## Folder & File Layout

### New files in `Core/`

```
src/Core/
│
└── Services/
    ├── IConfigService.cs       # Interface: load and validate the rule file
    ├── ConfigService.cs        # Implementation
    ├── ConfigResult.cs         # Response: RuleSet?, ValidationResult, ExitCode
    │
    ├── IListService.cs         # Interface: enumerate processes and match rules
    ├── ListService.cs          # Implementation
    ├── ProcessListResult.cs    # Response: fingerprints, matches, ExitCode
    │
    ├── IActionsService.cs      # Interface: build action plans (dry-run)
    ├── ActionsService.cs       # Implementation
    ├── ActionsResult.cs        # Response: list of ActionPlanView, ExitCode
    ├── ActionPlanView.cs       # Flat view model: process info + action + blocked state
    │
    ├── IExecuteService.cs      # Interface: execute action plans
    ├── ExecuteService.cs       # Implementation
    ├── ExecuteResult.cs        # Response: list of ActionResultView, AnyFailed, ExitCode
    └── ActionResultView.cs     # Flat view model: action info + success/failure + message
```

### Changed files in `Cli/`

```
src/Cli/
├── Program.cs                  # Register service interfaces; add global --output option
└── Commands/
    ├── ConfigCommand.cs        # Delegate to IConfigService; render ConfigResult
    ├── ListCommand.cs          # Delegate to IListService; render ProcessListResult or template
    ├── ActionsCommand.cs       # Delegate to IActionsService; render ActionsResult
    └── ExecuteCommand.cs       # Delegate to IExecuteService; render ExecuteResult
```

### New test files in `Core.Tests/`

```
tests/Core.Tests/
└── Services/
    ├── ConfigServiceTests.cs
    ├── ListServiceTests.cs
    ├── ActionsServiceTests.cs
    └── ExecuteServiceTests.cs
```

### Changed test files in `Cli.Tests/`

```
tests/Cli.Tests/
└── Commands/
    ├── ConfigCommandTests.cs   # Updated: mock IConfigService instead of ConfigurationLoader
    ├── ListCommandTests.cs     # Updated: mock IListService
    ├── ActionsCommandTests.cs  # Updated: mock IActionsService
    └── ExecuteCommandTests.cs  # Updated: mock IExecuteService
```

---

## Component Design

### Service Layer

All service types live in the `SystemFitnessHelper.Services` namespace, inside `src/Core/Services/`.

---

#### `IConfigService` / `ConfigService` / `ConfigResult`

**`ConfigResult`** — immutable record returned by the config service:

| Property | Type | Description |
|---|---|---|
| `RuleSet` | `RuleSet?` | Parsed rule set; `null` if config was not found or failed to parse |
| `Validation` | `ValidationResult` | Errors and warnings from the loader |
| `ErrorMessage` | `string?` | Human-readable error when config could not be located; `null` on success |
| `ExitCode` | `int` | `0` = valid, `2` = config not found or has errors |

**`IConfigService`** — single method:

```
ConfigResult GetConfig(string? configPath)
```

**`ConfigService`** — implementation:
1. Calls `ConfigurationLoader.DiscoverPath(configPath)`. If `null`, returns `ConfigResult` with `ExitCode = 2` and `ErrorMessage = "No rules.json found…"`.
2. Calls `ConfigurationLoader.Load(path)`, returns a `ConfigResult` wrapping the `RuleSet?` and `ValidationResult`. `ExitCode` is `0` if `validation.IsValid`, else `2`.

---

#### `IListService` / `ListService` / `ProcessListResult`

**`ProcessListResult`** — immutable record:

| Property | Type | Description |
|---|---|---|
| `Fingerprints` | `IReadOnlyList<ProcessFingerprint>` | All running processes/services |
| `Matches` | `IReadOnlyList<MatchResult>` | Rule-matched pairs (fingerprint + rule) |
| `ErrorMessage` | `string?` | Set when config could not be loaded; `null` on success |
| `ExitCode` | `int` | `0` = success, `2` = config error |

**`IListService`** — two methods:

```
ProcessListResult GetProcessList(string? configPath)
RuleSet BuildTemplate()
```

`BuildTemplate()` is a direct delegate to `ConfigurationBuilder.Build(scanner.Scan())`. It has no config dependency and always succeeds.

**`ListService`** — implementation:
1. **`GetProcessList`**: discovers and loads config (same as `ConfigService`); if loading fails, returns `ProcessListResult` with `ExitCode = 2`. Otherwise, calls `scanner.Scan()`, then `matcher.Match(fingerprints, ruleSet)`, returns `ProcessListResult` with `ExitCode = 0`.
2. **`BuildTemplate`**: calls `scanner.Scan()`, passes result to `ConfigurationBuilder.Build()`, returns the `RuleSet` template.

---

#### `IActionsService` / `ActionsService` / `ActionsResult` / `ActionPlanView`

**`ActionPlanView`** — flat, JSON-serializable record representing one evaluated action plan:

| Property | Type | Description |
|---|---|---|
| `ProcessName` | `string` | From `ProcessFingerprint.ProcessName` |
| `ProcessId` | `int` | From `ProcessFingerprint.ProcessId` |
| `ServiceName` | `string?` | From `ProcessFingerprint.ServiceName`; `null` for plain processes |
| `RuleId` | `string` | The rule that triggered this action |
| `Action` | `ActionType` | The planned action (`Stop`, `Kill`, `Suspend`, `None`) |
| `Blocked` | `bool` | `true` if `SafetyGuard.IsAllowed` returned `false` |
| `BlockReason` | `string?` | Reason string from the safety guard; `null` when not blocked |

**`ActionsResult`** — immutable record:

| Property | Type | Description |
|---|---|---|
| `Plans` | `IReadOnlyList<ActionPlanView>` | All evaluated plans (blocked and allowed) |
| `ErrorMessage` | `string?` | Set when config could not be loaded |
| `ExitCode` | `int` | `0` = success (even if all plans are blocked), `2` = config error |

**`IActionsService`** — single method:

```
ActionsResult GetActions(string? configPath)
```

**`ActionsService`** — implementation:
1. Discovers and loads config; returns error result on failure.
2. Constructs a `SafetyGuard` from `ruleSet.Protected`.
3. Calls `scanner.Scan()`, then `matcher.Match()`, then projects each `MatchResult` into an `ActionPlan`.
4. For each plan, calls `guard.IsAllowed(plan)` to populate `Blocked` and `BlockReason`.
5. Returns `ActionsResult` with `ExitCode = 0`.

---

#### `IExecuteService` / `ExecuteService` / `ExecuteResult` / `ActionResultView`

**`ActionResultView`** — flat, JSON-serializable record representing the outcome of one executed plan:

| Property | Type | Description |
|---|---|---|
| `ProcessName` | `string` | Target process name |
| `ProcessId` | `int` | Target process ID |
| `ServiceName` | `string?` | Target service name; `null` for plain processes |
| `RuleId` | `string` | The rule that triggered the action |
| `Action` | `ActionType` | The action that was attempted |
| `Success` | `bool` | Whether the action succeeded |
| `Message` | `string` | Human-readable outcome from `ActionResult.Message` |

**`ExecuteResult`** — immutable record:

| Property | Type | Description |
|---|---|---|
| `Results` | `IReadOnlyList<ActionResultView>` | One entry per executed (or blocked) plan |
| `AnyFailed` | `bool` | `true` if any `ActionResultView.Success` is `false` |
| `ErrorMessage` | `string?` | Set when config could not be loaded |
| `ExitCode` | `int` | `0` = all succeeded, `1` = any failed, `2` = config error |

**`IExecuteService`** — single method:

```
ExecuteResult Execute(string? configPath)
```

**`ExecuteService`** — implementation:
1. Discovers and loads config; returns error result on failure.
2. Constructs `SafetyGuard`, builds plans (same as `ActionsService`).
3. For each plan: if `guard.IsAllowed` returns `false`, records `ActionResultView` with `Success = false`, `Message = "Blocked: {reason}"`. Otherwise calls `executor.Execute(plan)`, maps the `ActionResult` to an `ActionResultView`, and logs the outcome via Serilog.
4. Returns `ExecuteResult` with `AnyFailed` and `ExitCode` computed from the results.

> **Note:** Elevation checking (`WindowsIdentity`) and the UAC re-launch remain in `ExecuteCommand` — they are CLI concerns, not service concerns.

---

### CLI Command Changes

#### Global `--output` option

A new global option is added to `Program.cs`:

```
--output, -o   console (default) | json
```

It is registered as a global option on the root command so all sub-commands inherit it. Each command's `SetHandler` reads the value and, after obtaining the service result, either renders it with Spectre.Console or serializes it with `System.Text.Json`.

```csharp
var outputOption = new Option<string>(["--output", "-o"], "Output format: 'console' (default) or 'json'");
outputOption.SetDefaultValue("console");
root.AddGlobalOption(outputOption);
```

---

#### `sfh list` — `--format` option replaces the existing `--output` option

The current per-command `--output` option on `list` (which generates a RuleSet template) conflicts with the new global `--output` option. It is renamed to `--format`:

```
--format   table (default) | template
```

- `--format table` (default): calls `listService.GetProcessList(configPath)`, renders the result as a Spectre.Console table or JSON (`ProcessListResult`), depending on the global `--output` value.
- `--format template`: calls `listService.BuildTemplate()`, renders the `RuleSet` as indented JSON (the `--output` global option has no effect here; template output is always JSON).

---

#### Command adapter pattern

Each command handler follows this pattern after the refactor:

```
1. Read options (configPath, global outputType, command-specific options)
2. Resolve service from DI
3. Call service method → typed result
4. If outputType == "json": serialize result to indented JSON, write to stdout, return result.ExitCode
5. Else: render result with Spectre.Console, return result.ExitCode
```

The rendering logic (tables, coloured text, prompts) stays in the CLI layer. The `HandleAsync` static method signature changes: instead of taking low-level `IProcessScanner`, `IRuleMatcher`, etc., it now takes the relevant service interface.

---

#### `sfh execute` — confirmation prompt

The `AnsiConsole.Confirm` prompt remains in `ExecuteCommand`. The command calls `actionsService.GetActions(configPath)` to display the plan table, prompts the user, and — if confirmed — calls `executeService.Execute(configPath)` for the actual execution. This keeps the interactive concern in the CLI layer and avoids duplicating the plan-building step in the service.

When `--output json` is specified on `execute`, the confirmation prompt is automatically skipped (non-interactive mode implied) and the `ExecuteResult` is serialized to JSON.

---

### DI Registration

`Program.cs` registers the four new service interfaces alongside the existing registrations:

```csharp
services.AddSingleton<IConfigService, ConfigService>();
services.AddSingleton<IListService, ListService>();
services.AddSingleton<IActionsService, ActionsService>();
services.AddSingleton<IExecuteService, ExecuteService>();
```

The existing `IProcessScanner`, `IRuleMatcher`, `IActionExecutor`, and `SafetyGuard` registrations remain; they are injected into the service implementations via constructor injection.

---

### Logging

No change to `LoggingConfiguration`. The Serilog call in `ExecuteService` (logging each action outcome) mirrors what `ExecuteCommand` did previously, preserving the existing logging contract.

---

## NuGet Dependencies

No new packages required. All four services depend only on types already in `Core`; the CLI layer already has `System.Text.Json` via the .NET runtime.

---

## Implementation Steps

1. **Response types** — create `ActionPlanView`, `ActionResultView`, `ConfigResult`, `ProcessListResult`, `ActionsResult`, `ExecuteResult` as immutable records in `Core/Services/`; annotate with `[JsonConverter]` for `ActionType` enum serialization
2. **`ConfigService`** — implement `IConfigService` / `ConfigService`; write unit tests in `Core.Tests/Services/ConfigServiceTests.cs` covering: config not found → ExitCode 2; valid config → ExitCode 0 with rules; config with errors → ExitCode 2 with error messages
3. **`ListService`** — implement `IListService` / `ListService`; write unit tests covering: config error path; no matches; matches present; `BuildTemplate` produces a rule per fingerprint
4. **`ActionsService`** — implement `IActionsService` / `ActionsService`; write unit tests covering: config error; no matches; allowed plan; blocked plan (hard-coded guard); blocked plan (user-configured guard)
5. **`ExecuteService`** — implement `IExecuteService` / `ExecuteService`; write unit tests covering: config error; blocked plan → `Success = false`; successful execution; failed execution → `AnyFailed = true`, `ExitCode = 1`
6. **DI registration** — add the four `AddSingleton` calls to `Program.cs`
7. **Global `--output` option** — add to `Program.cs` root command
8. **Refactor `ConfigCommand`** — replace `ConfigurationLoader` calls with `IConfigService`; add JSON output path; update `ConfigCommandTests` to mock `IConfigService`
9. **Refactor `ListCommand`** — replace scanner/matcher calls with `IListService`; rename per-command `--output` to `--format`; add JSON output path for `ProcessListResult`; update `ListCommandTests`
10. **Refactor `ActionsCommand`** — replace scanner/matcher/guard calls with `IActionsService`; add JSON output path; update `ActionsCommandTests`
11. **Refactor `ExecuteCommand`** — replace scanner/matcher/executor/guard calls with `IActionsService` (plan display) + `IExecuteService` (execution); retain elevation check and confirmation prompt; skip prompt when `--output json`; update `ExecuteCommandTests`
12. **End-to-end smoke test** — run `sfh list`, `sfh list --output json`, `sfh actions --output json`, and `sfh config --output json` against a real machine; verify JSON output is valid and console output is unchanged
