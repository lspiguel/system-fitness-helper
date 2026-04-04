# Phase 1 — Windows Service & Tray Application: Implementation Plan

## Goal

Move the Phase 0 / 0.B logic into a background Windows Service and add two lightweight user-facing processes: a system-tray application and a full-screen dashboard UI. No new functional capabilities are introduced beyond what the CLI already provides. The primary goal is to make the engine run continuously in the background, expose it over a local JSON-RPC 2.0 named-pipe channel, and provide a graphical front-end that communicates through that channel.

The Phase 0.B service layer (`IConfigService`, `IListService`, `IActionsService`, `IExecuteService`) is the central contract: the Windows Service calls the same interfaces as the CLI does, and the pipe protocol is defined in terms of the same response types (`ConfigResult`, `ProcessListResult`, `ActionsResult`, `ExecuteResult`).

---

## Solution Structure

Five new projects are added. Existing projects are unchanged.

```
SystemFitnessHelper.sln
│
├── src/
│   ├── Core/        (unchanged)
│   ├── Cli/         (unchanged)
│   ├── Ipc/         ← new: JSON-RPC protocol types, pipe client/server primitives
│   ├── Service/     ← new: Windows Service host + request handlers
│   ├── TrayApp/     ← new: WinForms tray application
│   ├── Ui/          ← new: WinForms dashboard UI
│   └── Installer/   ← new: CLI tool to install/uninstall the service
│
└── tests/
    ├── Core.Tests/  (unchanged)
    ├── Cli.Tests/   (unchanged)
    ├── Ipc.Tests/   ← new: protocol serialization and pipe framing tests
    └── Service.Tests/ ← new: handler unit tests (mock service interfaces)
```

### Projects

| Project | Type | Purpose |
|---|---|---|
| `SystemFitnessHelper.Ipc` | Class library | Shared JSON-RPC types, pipe framing, command pipe client, event pipe client |
| `SystemFitnessHelper.Service` | Worker service (console/service) | Windows Service host; accepts JSON-RPC requests on the command pipe; broadcasts events on the event pipe |
| `SystemFitnessHelper.TrayApp` | WinForms app | System-tray icon; sends commands via command pipe; receives notifications via event pipe |
| `SystemFitnessHelper.Ui` | WinForms app | Dashboard UI; sends commands via command pipe; displays process list, action plans, and config editor |
| `SystemFitnessHelper.Installer` | Console app | CLI tool to install, start, stop, and uninstall the Windows Service |
| `SystemFitnessHelper.Ipc.Tests` | xUnit test project | Tests for protocol message serialization, pipe framing, and dispatcher routing |
| `SystemFitnessHelper.Service.Tests` | xUnit test project | Unit tests for each JSON-RPC handler (mock `IConfigService` etc.) |

### Project dependency graph

```
Core ←── Ipc ←── Service
                 TrayApp
                 Ui
         Core ←── Service
         Core ←── Cli   (unchanged)
         Installer       (no dependency on Core or Ipc — uses sc.exe and ServiceController)
```

`Ipc` references `Core` for the response types that are sent over the wire. `Service`, `TrayApp`, and `Ui` reference `Ipc`. `TrayApp` and `Ui` do not reference `Core` directly; they work only through the pipe client.

---

## Folder & File Layout

### `src/Ipc/`

```
src/Ipc/
├── SystemFitnessHelper.Ipc.csproj
│
├── Protocol/
│   ├── JsonRpcRequest.cs         # {"jsonrpc":"2.0","id":N,"method":"...","params":{...}}
│   ├── JsonRpcResponse.cs        # {"jsonrpc":"2.0","id":N,"result":{...}} or "error":{...}
│   ├── JsonRpcNotification.cs    # {"jsonrpc":"2.0","method":"...","params":{...}} (no id)
│   ├── JsonRpcError.cs           # {"code":N,"message":"..."}
│   └── JsonRpcErrorCode.cs       # Enum: ParseError=-32700, InvalidRequest=-32600, MethodNotFound=-32601, InternalError=-32603, ConfigNotFound=-32000, ExecutionFailed=-32001
│
├── Messages/
│   ├── Methods.cs                # string constants: Sfh.Config, Sfh.List, Sfh.ListTemplate, Sfh.Actions, Sfh.Execute, Sfh.ConfigSave
│   ├── ConfigParams.cs           # {string? ConfigPath}
│   ├── ListParams.cs             # {string? ConfigPath}
│   ├── ActionsParams.cs          # {string? ConfigPath}
│   ├── ExecuteParams.cs          # {string? ConfigPath}
│   ├── ConfigSaveParams.cs       # {RuleSet RuleSet, string? ConfigPath}
│   ├── ConfigSaveResult.cs       # {bool Success, string? ErrorMessage}
│   └── Events/
│       └── ActionExecutedEvent.cs  # mirrors ActionResultView; published after each execute action
│
└── Pipes/
    ├── PipeConstants.cs           # pipe names: SfhCommand = "sfh-command", SfhEvents = "sfh-events"
    ├── PipeFraming.cs             # static helpers: WriteMessageAsync / ReadMessageAsync (length-prefixed UTF-8 JSON)
    ├── CommandPipeClient.cs       # connects to sfh-command, sends a request, awaits a single response
    └── EventPipeClient.cs         # connects to sfh-events, reads notifications in a loop, raises .NET events
```

### `src/Service/`

```
src/Service/
├── SystemFitnessHelper.Service.csproj
├── Program.cs                    # Generic Host builder: adds WindowsService, Serilog, DI, starts hosted services
├── ServiceWorker.cs              # IHostedService: starts CommandPipeServer + EventPipeServer on StartAsync
│
├── Pipes/
│   ├── CommandPipeServer.cs      # accept→read→dispatch→write loop on sfh-command pipe
│   └── EventPipeServer.cs        # maintains connected clients on sfh-events; Broadcast(notification)
│
└── Handlers/
    ├── IRequestHandler.cs        # string Method { get; }; Task<object?> HandleAsync(JsonElement? params, CancellationToken ct)
    ├── HandlerDispatcher.cs      # registry of IRequestHandler keyed by method name; routes requests
    ├── ConfigHandler.cs          # method: sfh.config → IConfigService.GetConfig
    ├── ListHandler.cs            # method: sfh.list → IListService.GetProcessList; sfh.list.template → IListService.BuildTemplate
    ├── ActionsHandler.cs         # method: sfh.actions → IActionsService.GetActions
    ├── ExecuteHandler.cs         # method: sfh.execute → IExecuteService.Execute; broadcasts ActionExecutedEvent per result
    └── ConfigSaveHandler.cs      # method: sfh.config.save → deserializes RuleSet, writes to config path, returns ConfigSaveResult
```

### `src/TrayApp/`

```
src/TrayApp/
├── SystemFitnessHelper.TrayApp.csproj
├── Program.cs                    # Application.Run(new TrayApplicationContext())
├── TrayApplicationContext.cs     # ApplicationContext: owns NotifyIcon, context menu, service connection
├── ServiceConnection.cs          # wraps CommandPipeClient + EventPipeClient; exposes typed async methods + events
└── UiLauncher.cs                 # starts or activates the Ui process via Process.Start
```

### `src/Ui/`

```
src/Ui/
├── SystemFitnessHelper.Ui.csproj
├── Program.cs                    # Application.Run(new MainForm(...))
├── ServiceConnection.cs          # wraps CommandPipeClient; exposes typed async methods (same pattern as TrayApp)
├── MainForm.cs                   # hosts TabControl; wires toolbar buttons; shows status bar (connected/disconnected)
└── Forms/
    ├── ProcessListPanel.cs       # UserControl: process table with match highlighting; calls sfh.list
    ├── ActionsPanel.cs           # UserControl: action plan table; calls sfh.actions
    └── ConfigEditorPanel.cs      # UserControl: rule DataGridView; Edit/Add/Delete; "Add from Template" flow
```

### `src/Installer/`

```
src/Installer/
├── SystemFitnessHelper.Installer.csproj
└── Program.cs                    # Sub-commands: install | start | stop | uninstall | status
```

### `tests/Ipc.Tests/`

```
tests/Ipc.Tests/
├── SystemFitnessHelper.Ipc.Tests.csproj
├── Protocol/
│   └── JsonRpcSerializationTests.cs  # request/response/notification round-trip
└── Pipes/
    └── PipeFramingTests.cs           # WriteMessageAsync / ReadMessageAsync round-trip over in-process streams
```

### `tests/Service.Tests/`

```
tests/Service.Tests/
├── SystemFitnessHelper.Service.Tests.csproj
├── Handlers/
│   ├── ConfigHandlerTests.cs
│   ├── ListHandlerTests.cs
│   ├── ActionsHandlerTests.cs
│   ├── ExecuteHandlerTests.cs
│   └── ConfigSaveHandlerTests.cs
└── Pipes/
    └── HandlerDispatcherTests.cs     # unknown method → MethodNotFound error; known method → routed
```

---

## Component Design

### IPC Protocol

All inter-process communication uses **JSON-RPC 2.0** over **named pipes**. There are two pipes:

| Pipe | Name | Direction | Purpose |
|---|---|---|---|
| Command pipe | `sfh-command` | duplex (client ↔ server) | Client sends a request, server sends one response |
| Event pipe | `sfh-events` | server → clients (read-only for clients) | Server broadcasts notifications; no client response |

#### Framing

Because `NamedPipeServerStream` in message-transmission mode has a 64 KB message limit in some configurations, messages are framed explicitly:

```
[4 bytes little-endian int32: payload length][payload bytes: UTF-8 JSON]
```

`PipeFraming.WriteMessageAsync(PipeStream, string json, CancellationToken)` and `PipeFraming.ReadMessageAsync(PipeStream, CancellationToken) → string` implement this. Maximum message size is capped at 4 MB; messages exceeding this limit are rejected with an `InternalError` JSON-RPC error.

#### JSON-RPC message types

**`JsonRpcRequest`**

| Field | Type | Description |
|---|---|---|
| `Jsonrpc` | `string` | Always `"2.0"` |
| `Id` | `int` | Client-assigned request identifier; echoed in the response |
| `Method` | `string` | Method name (see `Methods.cs`) |
| `Params` | `JsonElement?` | Nullable; serialized as the params object or `null` |

**`JsonRpcResponse`**

| Field | Type | Description |
|---|---|---|
| `Jsonrpc` | `string` | Always `"2.0"` |
| `Id` | `int` | Echoed from the request |
| `Result` | `JsonElement?` | Present on success; `null` on error |
| `Error` | `JsonRpcError?` | Present on failure; `null` on success |

Exactly one of `Result` or `Error` is non-null.

**`JsonRpcNotification`** (event pipe)

| Field | Type | Description |
|---|---|---|
| `Jsonrpc` | `string` | Always `"2.0"` |
| `Method` | `string` | Event name (e.g. `sfh.action.executed`) |
| `Params` | `JsonElement?` | Event payload |

**`JsonRpcError`**

| Field | Type | Description |
|---|---|---|
| `Code` | `int` | `JsonRpcErrorCode` value |
| `Message` | `string` | Human-readable error description |

**`JsonRpcErrorCode`** enum

| Name | Value | Meaning |
|---|---|---|
| `ParseError` | -32700 | Request JSON was malformed |
| `InvalidRequest` | -32600 | Missing required JSON-RPC fields |
| `MethodNotFound` | -32601 | No handler registered for the method |
| `InternalError` | -32603 | Unhandled exception in the handler |
| `ConfigNotFound` | -32000 | Service-level: rules.json could not be located |
| `ExecutionFailed` | -32001 | Service-level: at least one action failed |

#### Methods and parameter / result types

| Method | Params type | Result type | Notes |
|---|---|---|---|
| `sfh.config` | `ConfigParams` | `ConfigResult` | Validates the service config file |
| `sfh.list` | `ListParams` | `ProcessListResult` | Scans + matches; returns fingerprints and match info |
| `sfh.list.template` | *(none)* | `RuleSet` | Generates a disabled RuleSet from the live process snapshot |
| `sfh.actions` | `ActionsParams` | `ActionsResult` | Dry-run: returns action plans with blocked/unblocked state |
| `sfh.execute` | `ExecuteParams` | `ExecuteResult` | Executes actions; broadcasts `sfh.action.executed` per result |
| `sfh.config.save` | `ConfigSaveParams` | `ConfigSaveResult` | Persists an updated RuleSet to the config file |

`ConfigPath` in all params types is always `null` when called from the service; the service resolves its own config path at `%ProgramData%\SystemFitnessHelper\rules.json`. The field exists so that `CommandPipeClient` can optionally be used by integration tests or future tooling that targets an alternate config.

#### Events

| Event method | Params type | Trigger |
|---|---|---|
| `sfh.action.executed` | `ActionExecutedEvent` | After each individual action in `sfh.execute` (success or failure) |

**`ActionExecutedEvent`** — mirrors `ActionResultView` with one additional field:

| Property | Type | Description |
|---|---|---|
| `ProcessName` | `string` | Target process name |
| `ProcessId` | `int` | Target process ID |
| `ServiceName` | `string?` | Target service name; `null` for plain processes |
| `RuleId` | `string` | Rule that triggered the action |
| `Action` | `ActionType` | Action that was performed |
| `Success` | `bool` | Whether the action succeeded |
| `Message` | `string` | Human-readable outcome |
| `Timestamp` | `DateTimeOffset` | When the action was executed |

---

### `src/Ipc/Pipes/`

#### `CommandPipeClient`

Stateless; each call opens a new connection, writes the request, reads the response, and closes the connection.

```
Task<TResult> SendAsync<TResult>(string method, object? @params, CancellationToken ct)
```

Internally:
1. Create `NamedPipeClientStream(".", PipeConstants.SfhCommand, PipeDirection.InOut, PipeOptions.Asynchronous)`.
2. `ConnectAsync(timeout: 5 s, ct)` — throws `TimeoutException` if the service is not running.
3. Serialize the `JsonRpcRequest` (auto-incrementing client-side `Id`) and write via `PipeFraming.WriteMessageAsync`.
4. Read the response via `PipeFraming.ReadMessageAsync`, deserialize `JsonRpcResponse`.
5. If `response.Error != null`, throw `JsonRpcException(error.Code, error.Message)`.
6. Deserialize `response.Result` as `TResult` and return.

#### `EventPipeClient`

Long-lived; starts a background loop that reads notifications until cancellation.

```
event EventHandler<ActionExecutedEventArgs> ActionExecuted
Task StartListeningAsync(CancellationToken ct)
```

Internally, `StartListeningAsync` runs a loop:
1. Create `NamedPipeClientStream(".", PipeConstants.SfhEvents, PipeDirection.In, PipeOptions.Asynchronous)`.
2. `ConnectAsync(ct)`. On `OperationCanceledException`, exit the loop.
3. In an inner loop: `ReadMessageAsync` → deserialize `JsonRpcNotification` → dispatch to the matching event handler.
4. On `IOException` (server disconnected): reconnect after a 2-second delay, then back to step 1.

---

### `src/Service/`

#### `Program.cs`

Uses `Microsoft.Extensions.Hosting` generic host:

```csharp
Host.CreateDefaultBuilder(args)
    .UseWindowsService(options => options.ServiceName = "SystemFitnessHelper")
    .UseSerilog(...)
    .ConfigureServices(services =>
    {
        // Core services (same registrations as Cli/Program.cs)
        services.AddSingleton<IProcessScanner, WindowsProcessScanner>();
        services.AddSingleton<IRuleMatcher, RuleMatcher>();
        services.AddSingleton<IActionExecutor, WindowsActionExecutor>();
        services.AddSingleton<IConfigService, ConfigService>();
        services.AddSingleton<IListService, ListService>();
        services.AddSingleton<IActionsService, ActionsService>();
        services.AddSingleton<IExecuteService, ExecuteService>();
        // IPC
        services.AddSingleton<EventPipeServer>();
        services.AddSingleton<CommandPipeServer>();
        services.AddSingleton<HandlerDispatcher>();
        services.AddSingleton<IRequestHandler, ConfigHandler>();
        services.AddSingleton<IRequestHandler, ListHandler>();
        services.AddSingleton<IRequestHandler, ActionsHandler>();
        services.AddSingleton<IRequestHandler, ExecuteHandler>();
        services.AddSingleton<IRequestHandler, ConfigSaveHandler>();
        services.AddHostedService<ServiceWorker>();
    })
    .Build()
    .Run();
```

When run interactively (not as a Windows Service), `UseWindowsService` is a no-op; the host runs as a console app, enabling local debugging.

**Config path for the service:** The service always uses `%ProgramData%\SystemFitnessHelper\rules.json` as its config path. This path is injected as an `IOptions<ServiceConfig>` (a simple settings class with a `ConfigPath` string) sourced from `appsettings.json` in the service install directory, with an environment-variable override (`SFH_CONFIG_PATH`).

#### `ServiceWorker`

`IHostedService` that holds a reference to `CommandPipeServer` and `EventPipeServer`.

- `StartAsync`: calls `commandPipeServer.StartAsync(ct)` and `eventPipeServer.StartAsync(ct)` (both start background tasks and return immediately).
- `StopAsync`: calls `commandPipeServer.StopAsync(ct)` and `eventPipeServer.StopAsync(ct)`, waits for both.

#### `CommandPipeServer`

Owns a `CancellationTokenSource` (stopped via `StopAsync`). `StartAsync` launches a `Task` running the accept loop:

1. Create `NamedPipeServerStream("sfh-command", PipeDirection.InOut, maxNumberOfServerInstances: 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous)`.
2. `await serverStream.WaitForConnectionAsync(ct)`.
3. `await PipeFraming.ReadMessageAsync(serverStream, ct)` → `string json`.
4. `JsonRpcRequest? request = Deserialize(json)`. On deserialization failure, write a `ParseError` response and jump to step 7.
5. `JsonRpcResponse response = await dispatcher.DispatchAsync(request, ct)`.
6. `await PipeFraming.WriteMessageAsync(serverStream, Serialize(response), ct)`.
7. `serverStream.Disconnect()`. Go to step 1 (creates a new `NamedPipeServerStream` instance for the next client).

Each accepted connection is handled inline (one at a time). This is sufficient for Phase 1 where the TrayApp and UI make infrequent one-shot calls. If `ct` is cancelled, the loop exits cleanly.

#### `EventPipeServer`

Maintains a `ConcurrentDictionary<int, NamedPipeServerStream>` of connected client streams (keyed by connection index).

`StartAsync` launches a background task that continuously creates new server instances (`maxNumberOfServerInstances: NamedPipeServerStream.MaxAllowedServerInstances`) and accepts clients:

```
loop:
  create new NamedPipeServerStream("sfh-events", PipeDirection.Out, ..., PipeOptions.Asynchronous)
  WaitForConnectionAsync(ct)
  add to connected clients dictionary
  start a "monitor" task: reads nothing but detects disconnect (ReadAsync returns 0 bytes → remove from dictionary)
  loop
```

`Broadcast(JsonRpcNotification notification)` serializes the notification and writes it to every connected client via `PipeFraming.WriteMessageAsync`. Clients that fail the write (disconnected) are removed from the dictionary.

#### `HandlerDispatcher`

Receives all `IRequestHandler` implementations via constructor injection (registered as a collection). On startup, builds a `Dictionary<string, IRequestHandler>` keyed by `handler.Method`.

```
Task<JsonRpcResponse> DispatchAsync(JsonRpcRequest request, CancellationToken ct)
```

1. Looks up the handler by `request.Method`. If not found, returns a `MethodNotFound` error response.
2. Calls `handler.HandleAsync(request.Params, ct)`.
3. On success, returns a response with the handler's return value serialized into `Result`.
4. On `JsonRpcException`, returns a response with the exception's code and message as `Error`.
5. On any other exception, logs it and returns an `InternalError` response (no exception details in the response body — security).

#### `IRequestHandler` / handlers

**`IRequestHandler`**:

```csharp
string Method { get; }
Task<object?> HandleAsync(JsonElement? @params, CancellationToken ct);
```

**`ConfigHandler`** — `Method = Methods.Config`:
Deserializes `ConfigParams` from `params`, calls `configService.GetConfig(configParams.ConfigPath ?? serviceConfigPath)`, returns `ConfigResult`.

**`ListHandler`** — handles two methods:
- `Methods.List`: deserializes `ListParams`, calls `listService.GetProcessList(...)`, returns `ProcessListResult`.
- `Methods.ListTemplate`: calls `listService.BuildTemplate()`, returns `RuleSet`.

Because `IRequestHandler` maps one-to-one with a method name, `ListHandler` is split into `ListProcessHandler` and `ListTemplateHandler`, each implementing `IRequestHandler` for their respective method.

**`ActionsHandler`** — `Method = Methods.Actions`:
Calls `actionsService.GetActions(...)`, returns `ActionsResult`.

**`ExecuteHandler`** — `Method = Methods.Execute`:
Calls `executeService.Execute(...)`, returns `ExecuteResult`. After the call, iterates `result.Results` and calls `eventPipeServer.Broadcast(new JsonRpcNotification { Method = "sfh.action.executed", Params = Serialize(ActionExecutedEvent.From(resultView)) })` for each result.

**`ConfigSaveHandler`** — `Method = Methods.ConfigSave`:
1. Deserializes `ConfigSaveParams`.
2. Validates `params.RuleSet` is not null.
3. Serializes `params.RuleSet` to indented JSON.
4. Writes to `params.ConfigPath ?? serviceConfigPath` atomically (write to a `.tmp` file, then `File.Replace`).
5. Returns `ConfigSaveResult { Success = true }` on success, `{ Success = false, ErrorMessage = ... }` on I/O failure.

---

### `src/TrayApp/`

WinForms app targeting `net8.0-windows`, `<UseWindowsForms>true</UseWindowsForms>`. The application has no main window; it runs entirely through a `NotifyIcon`.

#### `ServiceConnection`

Thin wrapper around `CommandPipeClient` and `EventPipeClient` that exposes typed async methods:

```csharp
Task<ActionsResult> GetActionsAsync(CancellationToken ct = default)
Task<ExecuteResult> ExecuteAsync(CancellationToken ct = default)
event EventHandler<ActionExecutedEventArgs> ActionExecuted
Task StartEventListeningAsync(CancellationToken ct)
bool IsServiceRunning { get; }   // updated periodically by a health-check timer
```

`IsServiceRunning` is maintained by a `System.Windows.Forms.Timer` that fires every 10 seconds and attempts a quick `SendAsync` with a short timeout. If the call succeeds, the property is `true`; if it throws `TimeoutException`, it is `false`.

#### `TrayApplicationContext`

Extends `ApplicationContext`. On construction:
1. Creates `NotifyIcon` with the application icon.
2. Creates context menu:
   - **"Execute Now"** — calls `serviceConnection.ExecuteAsync()` asynchronously; on completion, shows a balloon tip summarising success/failure counts.
   - **"Open Dashboard"** — calls `uiLauncher.LaunchOrActivate()`.
   - separator
   - **"Exit"** — disposes the `NotifyIcon` and calls `Application.Exit()`.
3. Starts `serviceConnection.StartEventListeningAsync(cts.Token)`.
4. Subscribes to `serviceConnection.ActionExecuted` → shows a balloon tip: `"{ProcessName} → {Action}: {Success/Failed}"`.

When `serviceConnection.IsServiceRunning` is `false`, the "Execute Now" and "Open Dashboard" menu items are disabled and the tray icon shows an overlay (a small warning badge added to the icon bitmap at runtime).

#### `UiLauncher`

```csharp
void LaunchOrActivate()
```

Checks if a process named `SystemFitnessHelper.Ui` is already running. If so, brings its main window to the foreground via `SetForegroundWindow` P/Invoke. If not, calls `Process.Start(pathToUiExe)`.

---

### `src/Ui/`

WinForms app targeting `net8.0-windows`, `<UseWindowsForms>true</UseWindowsForms>`.

#### `ServiceConnection`

Same pattern as the TrayApp `ServiceConnection` but without the event pipe client (the UI does not subscribe to background events — it refreshes on demand). Exposes:

```csharp
Task<ConfigResult> GetConfigAsync(CancellationToken ct = default)
Task<ProcessListResult> GetProcessListAsync(CancellationToken ct = default)
Task<RuleSet> GetTemplateAsync(CancellationToken ct = default)
Task<ActionsResult> GetActionsAsync(CancellationToken ct = default)
Task<ExecuteResult> ExecuteAsync(CancellationToken ct = default)
Task<ConfigSaveResult> SaveConfigAsync(RuleSet ruleSet, CancellationToken ct = default)
```

#### `MainForm`

A `Form` with:
- A `ToolStrip` across the top with: **Refresh**, **Execute**, and a status label ("Connected" / "Service not running").
- A `TabControl` with three tabs: **Processes**, **Actions**, **Configuration**.
- A `StatusStrip` at the bottom showing the last-refresh timestamp.

On load, calls `Refresh()` which populates all three tabs in sequence. Long-running service calls are awaited on a background task via `async void` event handlers; a `ProgressBar` overlay (`UseWaitCursor = true`) is shown while calls are in flight. If the service is unavailable, all tab contents are replaced with an error label.

#### `ProcessListPanel`

`UserControl` hosting a `DataGridView` bound to a `BindingList<ProcessRowViewModel>`. Columns: PID, Process Name, Service Name, Status, Memory (MB), Matched Rule.

Rows with a destructive matched action are coloured red; rows with a non-destructive match are coloured yellow; unmatched rows are the default colour. This mirrors the `sfh list` CLI command output.

`Refresh()` calls `serviceConnection.GetProcessListAsync()` and rebinds the grid.

#### `ActionsPanel`

`UserControl` hosting a `DataGridView` bound to `IReadOnlyList<ActionPlanView>`. Columns: Process, Service, Rule, Action, Blocked, Reason.

Blocked rows are coloured orange. `Refresh()` calls `serviceConnection.GetActionsAsync()`.

#### `ConfigEditorPanel`

`UserControl` hosting a `DataGridView` of rules with inline editing enabled. Columns: ID (read-only), Enabled (checkbox), Action (ComboBox cell), Conditions (read-only summary), Description.

Toolbar buttons above the grid:
- **Save** — serializes the current rule list back into a `RuleSet` and calls `serviceConnection.SaveConfigAsync(ruleSet)`. Shows a success or error message box.
- **Add Rule** — opens a `RuleEditDialog` (modal form) pre-populated with blank fields.
- **Edit Rule** — opens `RuleEditDialog` populated with the selected rule.
- **Delete Rule** — removes the selected row after confirmation.
- **Add from Template** — calls `serviceConnection.GetTemplateAsync()` to get the live process snapshot as a disabled `RuleSet`; opens a `TemplateImportDialog` listing those rules; the user checks the ones to import; checked rules are appended to the current rule list (still disabled, ready to be reviewed and enabled before saving).

`Refresh()` calls `serviceConnection.GetConfigAsync()` and rebinds the grid.

---

### `src/Installer/`

Console application (`net8.0-windows`). No DI; straightforward procedural code.

```
sfhi install   — copies binaries, creates config directory, registers service with SCM
sfhi start     — starts the service via ServiceController
sfhi stop      — stops the service via ServiceController
sfhi uninstall — stops + deletes the service, optionally removes binaries
sfhi status    — prints current service status and config file path
```

**`install` sub-command:**
1. Requires elevation; if not elevated, re-launches with `runas` verb (same pattern as `ExecuteCommand`).
2. Copies the service binary directory to `%ProgramFiles%\SystemFitnessHelper\Service\`.
3. Creates `%ProgramData%\SystemFitnessHelper\` if it does not exist.
4. Writes a minimal `rules.json` to `%ProgramData%\SystemFitnessHelper\rules.json` if one does not already exist: `{"rules":[],"protected":[]}`.
5. Runs `sc create SystemFitnessHelper binPath= "<installDir>\SystemFitnessHelper.Service.exe" start= auto DisplayName= "System Fitness Helper"` via `Process.Start`.
6. Sets the service description via `sc description`.
7. Prints a confirmation message with the install path and config path.

**`start` / `stop`:**
Use `new ServiceController("SystemFitnessHelper")` with `Start()` / `Stop()` and `WaitForStatus(ServiceControllerStatus.Running/Stopped, timeout: 30s)`.

**`uninstall` sub-command:**
1. Stops the service if running.
2. Runs `sc delete SystemFitnessHelper`.
3. Optionally (with `--remove-files` flag) deletes `%ProgramFiles%\SystemFitnessHelper\`.

**`status` sub-command:**
Prints service status (`Running`, `Stopped`, `NotInstalled`), the config file path, and whether the config file exists.

---

### Logging

The service's `LoggingConfiguration` (from `Core`) is unchanged. `Program.cs` integrates Serilog with `UseSerilog(...)` and writes to:
- **Console sink** — only when running interactively (not as a Windows Service); detected via `WindowsServiceHelpers.IsWindowsService()`.
- **Rolling file sink** — `%ProgramData%\SystemFitnessHelper\logs\sfh-.log`, daily rotation, 7-day retention (same path as Phase 0, changed from `%APPDATA%` to `%ProgramData%` so service log and CLI log go to the same place).

---

### Config file location change

| Phase | Config path | Log path |
|---|---|---|
| Phase 0 / 0.B (CLI) | `%APPDATA%\SystemFitnessHelper\rules.json` | `%APPDATA%\SystemFitnessHelper\logs\` |
| Phase 1 (Service) | `%ProgramData%\SystemFitnessHelper\rules.json` | `%ProgramData%\SystemFitnessHelper\logs\` |

The CLI (`sfh`) is unchanged and still discovers config via the three-step search (explicit `--config`, then `%APPDATA%`, then executable directory). The service always uses the `%ProgramData%` path. The installer creates the file there. Users who want the CLI to use the same config as the service should pass `--config %ProgramData%\SystemFitnessHelper\rules.json`.

---

## NuGet Dependencies

| Package | Used in | Purpose |
|---|---|---|
| `Microsoft.Extensions.Hosting` | Service | Generic host builder |
| `Microsoft.Extensions.Hosting.WindowsServices` | Service | `UseWindowsService()`, Windows Service lifetime |
| `Microsoft.Extensions.DependencyInjection` | Service | DI container (already used by Core) |
| `Serilog.Extensions.Hosting` | Service | `UseSerilog()` integration with generic host |
| `System.ServiceProcess.ServiceController` | Installer | `ServiceController` for start/stop/status |
| `xUnit` | Ipc.Tests, Service.Tests | Test framework |
| `Moq` | Service.Tests | Mock `IConfigService`, `IListService`, etc. |
| `FluentAssertions` | Ipc.Tests, Service.Tests | Readable assertions |

`System.IO.Pipes` is part of the .NET BCL — no package required.

---

## Implementation Steps

1. **`Ipc` protocol types** — create `JsonRpcRequest`, `JsonRpcResponse`, `JsonRpcNotification`, `JsonRpcError`, `JsonRpcErrorCode`; write `JsonRpcSerializationTests` covering round-trip for each type and enum values
2. **`PipeFraming`** — implement `WriteMessageAsync` / `ReadMessageAsync` with length-prefix framing; write `PipeFramingTests` using `MemoryStream` as the pipe substitute
3. **Message types** — create all `*Params`, `*Result`, `Methods`, `ActionExecutedEvent`; verify they serialize cleanly with `System.Text.Json` (including `ActionType` enum as string)
4. **`CommandPipeClient`** — implement typed `SendAsync<T>`; write tests using a loopback `AnonymousPipeServerStream` pair
5. **`EventPipeClient`** — implement listen loop with reconnection; write tests using an in-process server
6. **`HandlerDispatcher`** — implement method routing and error wrapping; write `HandlerDispatcherTests` covering: unknown method → `MethodNotFound`; handler throws `JsonRpcException` → error response; handler throws generic exception → `InternalError`
7. **Handlers** — implement `ConfigHandler`, `ListProcessHandler`, `ListTemplateHandler`, `ActionsHandler`, `ExecuteHandler`, `ConfigSaveHandler`; write handler tests for each using mocked service interfaces; test `ExecuteHandler` broadcasts one event per result
8. **`EventPipeServer`** — implement broadcast and client lifecycle; write integration test: two connected clients both receive a broadcast
9. **`CommandPipeServer`** — implement accept-read-dispatch-write loop; write integration test: send a valid request over a real named pipe, receive the expected response
10. **`ServiceWorker` + `Program.cs`** — wire the generic host; verify the service starts, accepts a connection, and responds when run interactively
11. **`Installer`** — implement all five sub-commands; manually test `install` + `start` + `status` + `stop` + `uninstall` on a clean machine
12. **`TrayApp` — `ServiceConnection`** — implement typed wrappers around `CommandPipeClient` + `EventPipeClient`; implement health-check timer
13. **`TrayApp` — `TrayApplicationContext`** — implement tray icon, context menu, balloon-tip logic, service-down state
14. **`TrayApp` — `UiLauncher`** — implement process detection and `SetForegroundWindow` P/Invoke
15. **`Ui` — `ServiceConnection`** — implement typed wrappers around `CommandPipeClient`
16. **`Ui` — `ProcessListPanel`** — implement data binding and row colouring; test with mock `ServiceConnection`
17. **`Ui` — `ActionsPanel`** — implement data binding and blocked-row colouring
18. **`Ui` — `ConfigEditorPanel`** — implement grid editing, Save, Add/Edit/Delete, Add from Template flow
19. **`Ui` — `MainForm`** — wire tabs, toolbar, async refresh, progress overlay, error state
20. **End-to-end smoke test** — install the service; run `sfhi install && sfhi start`; open TrayApp; open the UI; run Execute from the tray; verify a balloon tip appears; open the Dashboard; verify the Processes tab, Actions tab, and Config Editor load correctly; verify `sfhi stop && sfhi uninstall` cleans up cleanly
