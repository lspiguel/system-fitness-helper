# Phase 0.C — Multiple Rulesets: Implementation Plan

## Goal

Replace the single-ruleset configuration model with a named, dictionary-keyed multi-ruleset model. The config file now contains a map of named `RuleSet` entries, one of which is designated as the default. All commands (except `sfh list --format template`) accept an optional `--ruleset` global option to select which ruleset to act on; when omitted, the default ruleset is used automatically.

The changes are confined to the configuration model, the loader, the builder, the Phase 0.B service layer, and the CLI. No changes are made to fingerprinting, matching, actions, safety, or logging.

---

## Config File Schema Change

The JSON root object changes from a bare `RuleSet` to a `RuleSetsConfig` wrapper whose single property is a dictionary of named rulesets. Each entry's key is the unique ruleset name; each entry's value is the existing `RuleSet` structure extended with an `IsDefault` flag.

**Before (Phase 0 / 0.B):**

```json
{
  "rules": [
    { "id": "rule-0001", "enabled": true, "action": "Stop", "conditions": [...] }
  ],
  "protected": ["SomeService"]
}
```

**After (Phase 0.C):**

```json
{
  "ruleSets": {
    "work": {
      "isDefault": true,
      "rules": [
        { "id": "rule-0001", "enabled": true, "action": "Stop", "conditions": [...] }
      ],
      "protected": ["SomeService"]
    },
    "gaming": {
      "isDefault": false,
      "rules": [],
      "protected": []
    }
  }
}
```

Dictionary keys are the canonical ruleset names. They are case-insensitive for lookup purposes but preserved as-written for display. Exactly one entry must have `"isDefault": true`.

---

## Solution Structure

No new projects are added. All changes are inside existing projects.

```
SystemFitnessHelper.sln
│
├── src/
│   ├── Core/          ← configuration model, loader, builder, service layer updated
│   └── Cli/           ← global --ruleset option added; commands pass it through
│
└── tests/
    ├── Core.Tests/    ← configuration and service layer tests updated
    └── Cli.Tests/     ← command tests updated (--ruleset option scenarios)
```

---

## Folder & File Layout

### Changed and new files in `Core/`

```
src/Core/
│
├── Configuration/
│   ├── RuleSet.cs                  # CHANGED: gains bool IsDefault property
│   ├── RuleSetsConfig.cs           # NEW: root deserialization model; holds Dictionary<string, RuleSet>
│   ├── ConfigurationLoader.cs      # CHANGED: loads RuleSetsConfig; new multi-ruleset validation; ResolveRuleSet helper
│   └── ConfigurationBuilder.cs     # CHANGED: BuildConfig() added (returns RuleSetsConfig); Build() unchanged
│
└── Services/
    ├── ConfigResult.cs             # CHANGED: RuleSet? replaced by RuleSetsConfig?; adds AvailableRuleSetNames
    ├── ProcessListResult.cs        # CHANGED: adds ResolvedRuleSetName
    ├── ActionsResult.cs            # CHANGED: adds ResolvedRuleSetName
    ├── ExecuteResult.cs            # CHANGED: adds ResolvedRuleSetName
    ├── IConfigService.cs           # UNCHANGED (signature unchanged; returns full RuleSetsConfig)
    ├── ConfigService.cs            # CHANGED: returns RuleSetsConfig in ConfigResult
    ├── IListService.cs             # CHANGED: GetProcessList gains string? ruleSetName
    ├── ListService.cs              # CHANGED: resolves named ruleset before matching
    ├── IActionsService.cs          # CHANGED: GetActions gains string? ruleSetName
    ├── ActionsService.cs           # CHANGED: resolves named ruleset
    ├── IExecuteService.cs          # CHANGED: Execute gains string? ruleSetName
    └── ExecuteService.cs           # CHANGED: resolves named ruleset
```

### Changed files in `Cli/`

```
src/Cli/
├── Program.cs                      # CHANGED: global --ruleset option registered
└── Commands/
    ├── ConfigCommand.cs            # CHANGED: renders all named rulesets (no --ruleset needed here)
    ├── ListCommand.cs              # CHANGED: passes --ruleset to IListService; template path unchanged
    ├── ActionsCommand.cs           # CHANGED: passes --ruleset to IActionsService
    └── ExecuteCommand.cs           # CHANGED: passes --ruleset to IActionsService + IExecuteService
```

### Changed test files

```
tests/Core.Tests/
├── Configuration/
│   ├── ConfigurationLoaderTests.cs   # UPDATED + NEW CASES: multi-ruleset load, validation, resolution
│   └── RuleSetsConfigTests.cs        # NEW: serialization round-trip for the new root type
└── Services/
    ├── ConfigServiceTests.cs         # UPDATED: result carries RuleSetsConfig
    ├── ListServiceTests.cs           # UPDATED: ruleSetName parameter scenarios
    ├── ActionsServiceTests.cs        # UPDATED: ruleSetName parameter scenarios
    └── ExecuteServiceTests.cs        # UPDATED: ruleSetName parameter scenarios

tests/Cli.Tests/
└── Commands/
    ├── ConfigCommandTests.cs         # UPDATED: multiple rulesets rendered
    ├── ListCommandTests.cs           # UPDATED: --ruleset option forwarded
    ├── ActionsCommandTests.cs        # UPDATED: --ruleset option forwarded
    └── ExecuteCommandTests.cs        # UPDATED: --ruleset option forwarded
```

---

## Component Design

### Configuration Layer

#### `RuleSet` — extended

`RuleSet` gains a single new property:

| Property | Type | Default | Description |
|---|---|---|---|
| `IsDefault` | `bool` | `false` | Marks this ruleset as the one selected when `--ruleset` is omitted |

Existing properties (`Rules`, `Protected`) are unchanged. Because `IsDefault` defaults to `false`, no existing test fixtures break — they simply become non-default rulesets in isolation.

---

#### `RuleSetsConfig` — new root type

```csharp
public sealed class RuleSetsConfig
{
    public Dictionary<string, RuleSet> RuleSets { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}
```

The dictionary key is the ruleset name. `StringComparer.OrdinalIgnoreCase` is applied so that lookup by `--ruleset gaming` matches a key stored as `"Gaming"`. The JSON property name is `"ruleSets"` (camelCase, matching the existing serializer options).

---

#### `ConfigurationLoader` — updated

The method signatures change as follows:

**`Load(string path)`** — now returns `(RuleSetsConfig? Config, ValidationResult Validation)`.

Deserialization target changes from `RuleSet` to `RuleSetsConfig`. The existing per-rule validations (duplicate IDs, missing fields, unknown operators, Kill-on-service warning) are now applied per named ruleset. Additional multi-ruleset validations:

| Validation | Severity | Condition |
|---|---|---|
| No rulesets defined | Error | `RuleSets` dictionary is empty |
| No default ruleset | Error | No entry has `IsDefault = true` |
| Multiple default rulesets | Error | More than one entry has `IsDefault = true` |
| Duplicate rule ID across the same ruleset | Error | Same ID appears twice within one ruleset's `Rules` list |
| Empty ruleset name (key) | Error | Dictionary key is null or whitespace |

Per-ruleset errors are prefixed with the ruleset name for clarity: `"Ruleset 'gaming': duplicate rule ID 'rule-0001'."`.

**`ResolveRuleSet(RuleSetsConfig config, string? ruleSetName)`** — new static helper:

```
(RuleSet? RuleSet, string? ResolvedName, string? ErrorMessage) ResolveRuleSet(RuleSetsConfig config, string? ruleSetName)
```

1. If `ruleSetName` is non-null: look up in `config.RuleSets`. If not found, return `ErrorMessage = "Ruleset '{name}' not found. Available: {list}."`.
2. If `ruleSetName` is null: find the entry with `IsDefault = true`. If none (should not happen post-validation), return `ErrorMessage = "No default ruleset is defined."`.
3. On success: return `(RuleSet, resolvedName, null)`.

**`DiscoverPath`** — unchanged.

---

#### `ConfigurationBuilder` — updated

**`Build(IReadOnlyList<ProcessFingerprint> fingerprints)`** — unchanged. Still returns a plain `RuleSet`. This is used by `sfh list --format template`: the output is a raw ruleset that the user can paste into a named entry in their config file. The `IsDefault` property on the returned ruleset is `false` by default.

**`BuildConfig(IReadOnlyList<ProcessFingerprint> fingerprints, string ruleSetName = "default")`** — new overload. Returns a `RuleSetsConfig` with a single entry keyed by `ruleSetName`, `IsDefault = true`. Intended for the initial installer drop and for tooling that needs a ready-to-use multi-ruleset file from scratch.

---

### Service Layer

All four service interfaces gain `string? ruleSetName` on the methods that need to resolve a specific ruleset. `IConfigService` is unchanged because `sfh config` always displays all rulesets.

---

#### `IConfigService` / `ConfigService` / `ConfigResult` — updated

**`ConfigResult`** — changes:

| Property | Type | Before | After |
|---|---|---|---|
| `RuleSet` | `RuleSet?` | Parsed single ruleset | **Removed** |
| `Config` | `RuleSetsConfig?` | — | **Added**: full multi-ruleset config; `null` on load failure |
| `AvailableRuleSetNames` | `IReadOnlyList<string>` | — | **Added**: sorted list of all ruleset names from `Config`; empty on failure |
| `Validation` | `ValidationResult` | Unchanged | Unchanged |
| `ErrorMessage` | `string?` | Unchanged | Unchanged |
| `ExitCode` | `int` | Unchanged | Unchanged |

`ConfigService.GetConfig(string? configPath)` calls `ConfigurationLoader.Load(path)` and maps the result to `ConfigResult`, populating `Config` and `AvailableRuleSetNames`.

---

#### `IListService` / `ListService` / `ProcessListResult` — updated

**`IListService`** — `GetProcessList` signature change:

```
ProcessListResult GetProcessList(string? configPath, string? ruleSetName)
```

**`ProcessListResult`** — gains:

| Property | Type | Description |
|---|---|---|
| `ResolvedRuleSetName` | `string?` | The name of the ruleset that was actually used; `null` on error |

**`ListService.GetProcessList`** — after loading the `RuleSetsConfig`, calls `ConfigurationLoader.ResolveRuleSet(config, ruleSetName)`. If resolution fails (name not found, no default), returns `ProcessListResult` with `ExitCode = 2` and the resolver's `ErrorMessage`. Otherwise, proceeds with the resolved `RuleSet` as before.

**`BuildTemplate()`** — unchanged; returns a `RuleSet` (no ruleset name involved).

---

#### `IActionsService` / `ActionsService` / `ActionsResult` — updated

**`IActionsService`** — signature change:

```
ActionsResult GetActions(string? configPath, string? ruleSetName)
```

**`ActionsResult`** — gains `string? ResolvedRuleSetName`.

**`ActionsService.GetActions`** — resolves the named ruleset before building plans; populates `ResolvedRuleSetName` in the result.

---

#### `IExecuteService` / `ExecuteService` / `ExecuteResult` — updated

**`IExecuteService`** — signature change:

```
ExecuteResult Execute(string? configPath, string? ruleSetName)
```

**`ExecuteResult`** — gains `string? ResolvedRuleSetName`.

**`ExecuteService.Execute`** — resolves the named ruleset before executing; populates `ResolvedRuleSetName` in the result.

---

### CLI Changes

#### Global `--ruleset` option

Added to `Program.cs` alongside the existing `--config` and `--output` global options:

```csharp
var ruleSetOption = new Option<string?>(
    aliases: ["--ruleset", "-r"],
    description: "Name of the ruleset to use. If omitted, the default ruleset is used.");
root.AddGlobalOption(ruleSetOption);
```

The option is nullable (`string?`). When `null`, services use the default. When set, services attempt to resolve by name and return an error if not found.

---

#### `sfh config [--config <path>]`

Does **not** accept `--ruleset`. It always displays the entire config: all named rulesets, each with its rules table, protected list, and `IsDefault` flag. Validation errors are shown below, prefixed by ruleset name.

**Console output layout:**
1. For each named ruleset (sorted alphabetically): print a heading `Ruleset: {name} [DEFAULT]` (or without `[DEFAULT]`), followed by the existing rules table and protected list.
2. Validation errors and warnings below all rulesets.

**JSON output** (`--output json`): serializes the full `ConfigResult.Config` as indented JSON.

---

#### `sfh list [--config <path>] [--format <table|template>] [--ruleset <name>]`

`--ruleset` is accepted when `--format table` (default). Ignored when `--format template` (template generation scans processes — it has no ruleset context).

Console and JSON output gains a header line `Using ruleset: {ResolvedRuleSetName}` when the resolved name is non-null.

---

#### `sfh actions [--config <path>] [--ruleset <name>]`

Passes `--ruleset` to `IActionsService.GetActions`. Console output gains a header line `Using ruleset: {ResolvedRuleSetName}`.

---

#### `sfh execute [--config <path>] [--yes] [--ruleset <name>]`

Passes `--ruleset` to both `IActionsService.GetActions` (for the plan preview table) and `IExecuteService.Execute` (for the actual execution). Both calls use the same ruleset name so the preview and execution are always consistent.

---

### Sample Config Update

The existing `docs/sample-config0.json` (referenced in `phase-0.md`) is superseded. A new `docs/sample-config0c.json` demonstrates the multi-ruleset format with two named rulesets (`work` as default, `gaming` as non-default), illustrating the full schema.

---

## NuGet Dependencies

No new packages required.

---

## Implementation Steps

1. **`RuleSet.IsDefault`** — add `bool IsDefault { get; init; } = false;` to `RuleSet.cs`; verify all existing tests still compile and pass
2. **`RuleSetsConfig`** — create `RuleSetsConfig.cs` in `Core/Configuration/`; write `RuleSetsConfigTests.cs` covering: serialize a two-entry dictionary round-trip; case-insensitive key lookup works; empty dictionary deserializes without throwing
3. **`ConfigurationLoader` — deserialize** — update `Load()` to deserialize `RuleSetsConfig`; write tests covering: valid two-ruleset file loads without errors; missing file returns error; malformed JSON returns parse error
4. **`ConfigurationLoader` — multi-ruleset validation** — implement and test all new validation rules: no rulesets → error; no default → error; multiple defaults → error; per-ruleset duplicate ID → error prefixed with ruleset name; valid config with two rulesets → no errors
5. **`ConfigurationLoader.ResolveRuleSet`** — implement and test: named lookup hit; named lookup miss (unknown name → error with available list); null name → returns default; null name + no default → error
6. **`ConfigurationBuilder.BuildConfig`** — implement; write test: returns `RuleSetsConfig` with one entry named `"default"`, `IsDefault = true`, containing the same rules as `Build()`
7. **`ConfigResult` update** — replace `RuleSet?` with `RuleSetsConfig?` + `AvailableRuleSetNames`; update `ConfigService` and its tests
8. **`ProcessListResult`, `ActionsResult`, `ExecuteResult` update** — add `ResolvedRuleSetName`; update `IListService`, `IActionsService`, `IExecuteService` signatures; update all four service implementations to call `ResolveRuleSet` before proceeding; update service tests covering: unknown ruleset name → ExitCode 2; null name uses default; valid named ruleset is used
9. **Global `--ruleset` option** — add to `Program.cs`; pass through to each command's `HandleAsync` signature
10. **`ConfigCommand` update** — render all named rulesets in the console output; JSON output serializes `ConfigResult.Config`; update `ConfigCommandTests`
11. **`ListCommand` update** — pass `ruleSetName` to `IListService`; show `Using ruleset:` header; skip for `--format template`; update `ListCommandTests`
12. **`ActionsCommand` update** — pass `ruleSetName` to `IActionsService`; show `Using ruleset:` header; update `ActionsCommandTests`
13. **`ExecuteCommand` update** — pass `ruleSetName` to both service calls; update `ExecuteCommandTests`
14. **Sample config** — write `docs/sample-config0c.json` with `work` (default) and `gaming` named rulesets and use it in place of rules.json on the CLI project; adapt sample-config0.json and sample-config1.json to the new format (without making other changes)
15. **End-to-end smoke test** — run `sfh config`, `sfh list`, `sfh list --ruleset gaming`, `sfh list --ruleset nonexistent` (expect exit 2 with available names), `sfh actions --ruleset work --output json`; verify output and exit codes
