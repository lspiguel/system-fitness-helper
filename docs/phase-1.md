# Phase 1 — Windows Service & Tray Application: Implementation Plan

## Goal

Move the Phase 0 / 0.B / 0.C logic into a background Windows Service and add two lightweight user-facing processes: a system-tray application and a full-screen dashboard UI. No new functional capabilities are introduced beyond what the CLI already provides. The primary goal is to make the engine run continuously in the background, expose it over a local JSON-RPC 2.0 named-pipe channel, and provide a graphical front-end that communicates through that channel.

The Phase 0.B service layer (`IConfigService`, `IListService`, `IActionsService`, `IExecuteService`) is the central contract: the Windows Service calls the same interfaces as the CLI does, and the pipe protocol is defined in terms of the same response types (`ConfigResult`, `ProcessListResult`, `ActionsResult`, `ExecuteResult`). The Phase 0.C multi-ruleset model is fully reflected in the protocol: all relevant params types carry an optional `RuleSetName`, and config-save operates on the whole `RuleSetsConfig`.

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
│   ├── ListParams.cs             # {string? ConfigPath, string? RuleSetName}
│   ├── ActionsParams.cs          # {string? ConfigPath, string? RuleSetName}
│   ├── ExecuteParams.cs          # {string? ConfigPath, string? RuleSetName}
│   ├── ConfigSaveParams.cs       # {RuleSetsConfig RuleSetsConfig, string? ConfigPath}
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
    └── ConfigSaveHandler.cs      # method: sfh.config.save → deserializes RuleSetsConfig, writes atomically to config path, returns ConfigSaveResult
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
├── Program.cs                    # Argument dispatch + the five sub-commands
├── InstallerOptions.cs           # Parsed command line (pure, unit-tested)
├── Paths.cs                      # Every path derived from the options (pure, unit-tested)
├── Layout.cs                     # Payload validation and recursive copy
├── ServiceControl.cs             # SCM operations via sc.exe + ServiceController
├── Shortcuts.cs                  # Start Menu, tray autostart, Apps & Features
└── Elevation.cs                  # Elevation check and UAC relaunch (pure quoting helpers)
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

#### Pipe security

Both pipes are created with an explicit DACL (`PipeSecurityFactory.CreateDefault`) via
`NamedPipeServerStreamAcl.Create`:

| Principal | Rights | Why |
|---|---|---|
| Authenticated Users | `ReadWrite \| Synchronize` | The tray app and dashboard run as an ordinary, non-elevated user |
| The creating account | `FullControl` | The accept loop must create a new pipe instance per connection |
| Administrators, LocalSystem | `FullControl` | Administration and the service's own identity |

This is **not optional**. Under the SCM the service runs as LocalSystem, and a pipe created with
the default DACL is unreachable from a normal interactive user — a non-elevated client gets
`UnauthorizedAccessException` on connect. The omission is invisible during development, where
both ends run as the same interactive user.

Authenticated Users deliberately do **not** get `CreateNewInstance`, so an ordinary user cannot
add instances of the pipe and impersonate the service to other clients. The creating account
does need it: without it the accept loop can create the first pipe instance but not the second,
and the server silently degrades to serving one client at a time.

**Known limitation.** Clients connect by name and do not verify the server's identity, so a
process that wins the race before the service starts could squat the pipe name. Closing this
means a `Global\` prefix plus a client-side check of the server's SID.

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
| `RuleSetNotFound` | -32001 | Service-level: named ruleset does not exist in the config |
| `ExecutionFailed` | -32002 | Service-level: at least one action failed |

#### Methods and parameter / result types

| Method | Params type | Result type | Notes |
|---|---|---|---|
| `sfh.config` | `ConfigParams` | `ConfigResult` | Validates the service config; returns all named rulesets and `AvailableRuleSetNames` |
| `sfh.list` | `ListParams` | `ProcessListResult` | Scans + matches using `RuleSetName` (or default); returns fingerprints, matches, and `ResolvedRuleSetName` |
| `sfh.list.template` | *(none)* | `RuleSet` | Generates a disabled RuleSet from the live process snapshot; no ruleset context |
| `sfh.actions` | `ActionsParams` | `ActionsResult` | Dry-run using `RuleSetName` (or default); returns plans and `ResolvedRuleSetName` |
| `sfh.execute` | `ExecuteParams` | `ExecuteResult` | Executes actions using `RuleSetName` (or default); broadcasts `sfh.action.executed` per result |
| `sfh.config.save` | `ConfigSaveParams` | `ConfigSaveResult` | Persists the entire updated `RuleSetsConfig` atomically to the config file |
| `sfh.ping` | *(none)* | `PingResult` | Liveness probe: `Ok`, `Version`, resolved `ConfigPath`, `ConfigExists` |

`sfh.ping` exists so that clients have a **cheap** health check. The tray app previously probed
with `sfh.actions` every ten seconds — a full process enumeration plus rule match, per client,
purely to set a boolean. Returning the resolved config path also makes a misconfigured rules file
visible in the tray tooltip and in `sfhi status`.

`ConfigPath` and `RuleSetName` in all params types are always `null` when called from the TrayApp or Ui: the service resolves its own config path (`%ProgramData%\SystemFitnessHelper\rules.json`) and uses the default ruleset. Both fields exist so that `CommandPipeClient` can optionally be used by integration tests or CLI tooling that targets an alternate config or a named ruleset.

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

The timeout covers the **whole call** — connect, write and read — not just the connect. Timing
out only the connect leaves a caller hung indefinitely if the service accepts the connection and
then stalls, which on the UI thread freezes the dashboard. The default is 30 s; `ExecuteAsync`
passes 5 minutes, and the tray's health check passes 3 s. An `UnauthorizedAccessException` is
rethrown with a message naming the pipe ACL as the likely cause.
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

**Config path for the service:** injected as `IOptions<ServiceConfig>` and resolved in this order:

1. `SFH_CONFIG_PATH` environment variable, if set.
2. `ServiceConfig:ConfigPath` from `appsettings.json` in the install directory, if set.
3. The default, `%ProgramData%\SystemFitnessHelper\rules.json`, computed in C# from
   `Environment.SpecialFolder.CommonApplicationData`.

A `PostConfigure` step then calls `ServiceConfig.Resolve()`, which expands environment variables
and makes the path absolute. **This step is essential.** The options binder stores configured
strings verbatim, so a value such as `%ProgramData%\SystemFitnessHelper\rules.json` written
literally into `appsettings.json` stays unexpanded — and because a Windows Service starts in
`C:\Windows\System32`, the service would create and write a directory literally named
`%ProgramData%` underneath it. `appsettings.json` therefore ships with no `ConfigPath` value at
all; the C# default is already correct and absolute.

**Every handler uses this path.** `ConfigHandler`, `ListProcessHandler`, `ActionsHandler`,
`ExecuteHandler` and `ConfigSaveHandler` all resolve their target as
`params.ConfigPath ?? serviceConfig.ConfigPath`. The read handlers previously passed
`params.ConfigPath` straight through, which is always `null` from the tray app and dashboard, so
the service only found the right file because `ConfigurationLoader.DiscoverPath` happens to probe
`%ProgramData%` first. The service does not rely on that fallback.

The resolved path is logged once at startup and reported by `sfh.ping`, so a misconfiguration is
visible from `sfhi status` and the tray tooltip without reading the log.

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

**Connections are handled concurrently.** The accept loop hands each accepted stream to its own
task and immediately creates the next server instance, bounded by a `SemaphoreSlim` of 16
in-flight connections. Handling connections inline instead is not sufficient: a single
`sfh.execute` can take 30 seconds or more when a Windows service refuses to stop, and every
other client would exceed its connect timeout and report the service as down. A client that
disconnects before its response is written is logged at `Debug`, not `Error` — it is routine,
not a fault.

`ExecuteHandler` holds a process-wide `SemaphoreSlim(1,1)` around the action plan, so that
concurrent callers cannot race to stop the same services and kill the same PIDs.

If `ct` is cancelled, the loop exits cleanly.

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

`BroadcastAsync(JsonRpcNotification notification)` serializes the notification and writes it to
every connected client via `PipeFraming.WriteMessageAsync`. Each client carries its own write
lock, so a slow reader delays only itself and never the handler that raised the event. Clients
that fail the write, or whose `IsConnected` is already false, are removed and disposed.

**There is deliberately no disconnect-monitor task.** An earlier version started one that read
from the `PipeDirection.Out` client stream to detect disconnects. Reading a write-only stream
throws `NotSupportedException` immediately; the empty `catch` swallowed it and the `finally`
dropped the client, so every subscriber was torn down microseconds after connecting and **no
event was ever delivered**. Disconnects are detected through `IsConnected` and failed writes
instead.

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
Deserializes `ConfigParams` from `params`, calls `configService.GetConfig(configParams.ConfigPath ?? serviceConfigPath)`, returns `ConfigResult` (which now contains `RuleSetsConfig?` and `AvailableRuleSetNames`).

**`ListHandler`** — handles two methods:
- `Methods.List`: deserializes `ListParams`, calls `listService.GetProcessList(params.ConfigPath ?? serviceConfigPath, params.RuleSetName)`, returns `ProcessListResult` (including `ResolvedRuleSetName`). If the resolver returns an error, the handler returns a `RuleSetNotFound` JSON-RPC error response.
- `Methods.ListTemplate`: calls `listService.BuildTemplate()`, returns `RuleSet`. No ruleset context involved.

Because `IRequestHandler` maps one-to-one with a method name, `ListHandler` is split into `ListProcessHandler` and `ListTemplateHandler`, each implementing `IRequestHandler` for their respective method.

**`ActionsHandler`** — `Method = Methods.Actions`:
Deserializes `ActionsParams`, calls `actionsService.GetActions(params.ConfigPath ?? serviceConfigPath, params.RuleSetName)`, returns `ActionsResult`. Returns `RuleSetNotFound` error if the named ruleset does not exist.

**`ExecuteHandler`** — `Method = Methods.Execute`:
Deserializes `ExecuteParams`, calls `executeService.Execute(params.ConfigPath ?? serviceConfigPath, params.RuleSetName)`, returns `ExecuteResult`. Returns `RuleSetNotFound` error if the named ruleset does not exist. After the call, iterates `result.Results` and calls `eventPipeServer.Broadcast(new JsonRpcNotification { Method = "sfh.action.executed", Params = Serialize(ActionExecutedEvent.From(resultView)) })` for each result.

**`ConfigSaveHandler`** — `Method = Methods.ConfigSave`:
1. Deserializes `ConfigSaveParams`.
2. Validates `params.RuleSetsConfig` is not null and has at least one ruleset with `IsDefault = true`.
3. Serializes `params.RuleSetsConfig` to indented JSON.
4. Writes to `params.ConfigPath ?? serviceConfigPath` atomically (write to a `.tmp` file, then `File.Replace`).
5. Returns `ConfigSaveResult { Success = true }` on success, `{ Success = false, ErrorMessage = ... }` on I/O or validation failure.

---

### `src/TrayApp/`

WinForms app targeting `net8.0-windows`, `<UseWindowsForms>true</UseWindowsForms>`. The application has no main window; it runs entirely through a `NotifyIcon`.

#### `ServiceConnection`

Thin wrapper around `CommandPipeClient` and `EventPipeClient` that exposes typed async methods:

```csharp
Task<ActionsResult> GetActionsAsync(string? ruleSetName = null, CancellationToken ct = default)
Task<ExecuteResult> ExecuteAsync(string? ruleSetName = null, CancellationToken ct = default)
event EventHandler<ActionExecutedEventArgs> ActionExecuted
Task StartEventListeningAsync(CancellationToken ct)
bool IsServiceRunning { get; }   // updated periodically by a health-check timer
```

Both methods pass `ruleSetName = null` by default, so the service uses its configured default ruleset. The TrayApp does not expose a ruleset picker; it always acts on the default. If a non-default ruleset is needed from the tray in the future, the caller can supply the name.

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
Task<ProcessListResult> GetProcessListAsync(string? ruleSetName = null, CancellationToken ct = default)
Task<RuleSet> GetTemplateAsync(CancellationToken ct = default)
Task<ActionsResult> GetActionsAsync(string? ruleSetName = null, CancellationToken ct = default)
Task<ExecuteResult> ExecuteAsync(string? ruleSetName = null, CancellationToken ct = default)
Task<ConfigSaveResult> SaveConfigAsync(RuleSetsConfig ruleSetsConfig, CancellationToken ct = default)
```

`ruleSetName` is supplied from the UI's active ruleset selector (see `ConfigEditorPanel`). `SaveConfigAsync` now accepts the entire `RuleSetsConfig` so that adding, editing, or deleting a ruleset always persists the full document atomically.

#### `MainForm`

A `Form` with:
- A `ToolStrip` across the top with: **Refresh**, **Execute**, a **Ruleset** `ComboBox` selector, and a status label ("Connected" / "Service not running").
- A `TabControl` with three tabs: **Processes**, **Actions**, **Configuration**.
- A `StatusStrip` at the bottom showing the last-refresh timestamp and the active ruleset name.

The **Ruleset** ComboBox is populated from `ConfigResult.AvailableRuleSetNames` after the initial `GetConfigAsync()` call. The selected name is passed as `ruleSetName` to `GetProcessListAsync`, `GetActionsAsync`, and `ExecuteAsync`. Changing the selection triggers a refresh of the Processes and Actions tabs automatically. The Configuration tab always loads the full `RuleSetsConfig` (all rulesets).

On load, calls `Refresh()` which populates all three tabs in sequence. Long-running service calls are awaited on a background task via `async void` event handlers; a `ProgressBar` overlay (`UseWaitCursor = true`) is shown while calls are in flight. If the service is unavailable, all tab contents are replaced with an error label.

#### `ProcessListPanel`

`UserControl` hosting a `DataGridView` bound to a `BindingList<ProcessRowViewModel>`. Columns: PID, Process Name, Service Name, Status, Memory (MB), Matched Rule.

Rows with a destructive matched action are coloured red; rows with a non-destructive match are coloured yellow; unmatched rows are the default colour. This mirrors the `sfh list` CLI command output.

`Refresh(string? ruleSetName)` calls `serviceConnection.GetProcessListAsync(ruleSetName)` and rebinds the grid. Called by `MainForm` whenever the active ruleset changes.

#### `ActionsPanel`

`UserControl` hosting a `DataGridView` bound to `IReadOnlyList<ActionPlanView>`. Columns: Process, Service, Rule, Action, Blocked, Reason.

Blocked rows are coloured orange. `Refresh(string? ruleSetName)` calls `serviceConnection.GetActionsAsync(ruleSetName)`. Called by `MainForm` whenever the active ruleset changes.

#### `ConfigEditorPanel`

`UserControl` with two areas:
- **Top bar**: a `ComboBox` showing all ruleset names (labelled "Editing ruleset:") and a toolbar with ruleset-level buttons.
- **Main area**: a `DataGridView` of rules for the selected ruleset, with inline editing enabled.

Rule grid columns: ID (read-only), Enabled (checkbox), Action (ComboBox cell), Conditions (read-only summary), Description.

The `ComboBox` selection determines which ruleset's rules are shown in the grid. The `IsDefault` flag is indicated by a `[DEFAULT]` suffix in the ComboBox item text. Selecting a different ruleset in the ComboBox rebinds the grid immediately (no service call needed — the full `RuleSetsConfig` is already loaded).

**Ruleset-level toolbar buttons:**
- **New Ruleset** — prompts for a unique name, adds a new empty `RuleSet` entry to the in-memory `RuleSetsConfig`, selects it in the ComboBox.
- **Delete Ruleset** — removes the selected ruleset from the in-memory config after confirmation; not allowed if it is the default and no other ruleset exists.
- **Set as Default** — sets the selected ruleset's `IsDefault = true`, clears `IsDefault` on all others.

**Rule-level toolbar buttons:**
- **Save** — serializes the entire in-memory `RuleSetsConfig` and calls `serviceConnection.SaveConfigAsync(ruleSetsConfig)`. Shows a success or error message box.
- **Add Rule** — opens a `RuleEditDialog` (modal form) pre-populated with blank fields; adds the result to the currently selected ruleset.
- **Edit Rule** — opens `RuleEditDialog` populated with the selected rule.
- **Delete Rule** — removes the selected row after confirmation.
- **Add from Template** — calls `serviceConnection.GetTemplateAsync()` to get the live process snapshot as a disabled `RuleSet`; opens a `TemplateImportDialog` listing those rules; the user checks the ones to import and selects the target ruleset (defaults to the currently selected one); checked rules are appended to the target ruleset's rule list (still disabled).

`Refresh()` calls `serviceConnection.GetConfigAsync()`, loads the full `RuleSetsConfig` into memory, and rebinds both the ComboBox and the rule grid.

---

### `src/Installer/`

Console application (`net8.0-windows`). No DI; procedural code split so that the path and
argument logic is pure and unit-testable.

```
sfhi install   — deploys all three components, seeds config, registers the service and shortcuts
sfhi start     — starts the service via ServiceController
sfhi stop      — stops the service via ServiceController
sfhi uninstall — stops + deletes the service, shortcuts and registry entries
sfhi status    — prints service status, paths, and a live health probe
```

Options: `--prefix`, `--service-name`, `--no-tray-autostart`, `--remove-files`, `--purge`.

#### Deployment layout

The installer deploys **all three** user-facing components, not just the service. The component
layout is a contract: `UiLauncher` resolves the dashboard as `..\Ui\SystemFitnessHelper.Ui.exe`
relative to the tray app, so the three directories must be siblings under one root.

```
%ProgramFiles%\SystemFitnessHelper\
├── sfhi.exe + dependencies    (so Apps & Features can invoke it)
├── rules.sample.json
├── Service\
├── TrayApp\
└── Ui\

%ProgramData%\SystemFitnessHelper\
├── rules.json                 (seeded once, every rule disabled)
└── logs\
```

`build.ps1` produces exactly this layout under `publish\`; the installer copies it wholesale.
The whole payload is copied rather than a hand-picked file list, because `sfhi.exe` sits at the
payload root with its own dependencies beside it.

#### `install`

1. Elevate if required, then **validate the payload layout before modifying anything**, so a
   wrong working directory fails cleanly instead of half-installing.
2. Stop the service if it is running — its binaries are otherwise locked against the copy.
3. Copy the payload to the install root.
4. Create `%ProgramData%\SystemFitnessHelper\` and `logs\`.
5. Seed `rules.json` **only when absent**, from `rules.sample.json` with every rule forced to
   `enabled: false`, so a fresh install never stops anything unreviewed.
6. `sc create` with a **quoted** `binPath`, `start= auto`, `obj= LocalSystem`; if the service
   already exists, `sc config` it instead of failing with 1073. Then `sc description` and
   `sc failure` (restart/restart/none, 60 s reset).
7. Common Start Menu shortcuts for the dashboard and the tray app.
8. Tray autostart under `HKLM\...\CurrentVersion\Run`.
9. Apps & Features registration under `HKLM\...\Uninstall\SystemFitnessHelper`.

Shortcuts and autostart are **machine-wide** (common Start Menu, HKLM) rather than per-user:
the installer runs elevated, so HKCU and the per-user Start Menu would land in the
administrator's profile instead of the profile of whoever is installing.

The `binPath` is double-quoted inside the `sc.exe` argument. `sc.exe` stores the value verbatim,
so passing it bare leaves an unquoted `ImagePath` containing spaces — the classic unquoted
service path weakness.

#### `uninstall`

Stops the service, deletes it (retrying past error 1072, "marked for deletion", which happens
whenever something such as an open `services.msc` still holds a handle), then removes shortcuts,
the Run value and the Apps & Features entry. `--remove-files` deletes the install root;
`--purge` additionally deletes `%ProgramData%\SystemFitnessHelper`. Without `--purge`,
configuration and logs are kept and the installer says so.

#### `status`

Prints the SCM state, the registered `ImagePath`, install root, config path and log directory —
and when the service is running, sends `sfh.ping` over the command pipe and reports the version
and the rules file the service actually resolved. A mismatch between that and the installer's
expected config path is called out explicitly.

#### Elevation

`install` and `uninstall` require Administrator rights. When not elevated, the installer
relaunches itself with the `runas` verb, **quoting each argument individually** so paths
containing spaces survive, **waits for the child** and **returns the child's exit code**, so
`sfhi install; if ($?) { ... }` chains correctly. The child is passed `--elevated-child` and
holds its console open at the end, since a relaunched window otherwise closes before any output
can be read.

#### sc.exe diagnostics

`sc.exe` reports its failures on **stdout**, not stderr. Both streams are drained on background
threads (draining one to the end after `WaitForExit` can deadlock when the other fills its
buffer) and echoed on a non-zero exit, with the common codes explained in plain English: 5
access denied, 1060 not installed, 1072 marked for deletion, 1073 already exists.

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
3. **Message types** — create all `*Params` (including `string? RuleSetName` on `ListParams`, `ActionsParams`, `ExecuteParams`), `*Result`, `Methods`, `ActionExecutedEvent`; update `ConfigSaveParams` to carry `RuleSetsConfig` instead of `RuleSet`; verify they serialize cleanly with `System.Text.Json` (including `ActionType` enum as string and `RuleSetsConfig` dictionary round-trip)
4. **`CommandPipeClient`** — implement typed `SendAsync<T>`; write tests using a loopback `AnonymousPipeServerStream` pair
5. **`EventPipeClient`** — implement listen loop with reconnection; write tests using an in-process server
6. **`HandlerDispatcher`** — implement method routing and error wrapping; write `HandlerDispatcherTests` covering: unknown method → `MethodNotFound`; handler throws `JsonRpcException` → error response; handler throws generic exception → `InternalError`
7. **Handlers** — implement `ConfigHandler`, `ListProcessHandler`, `ListTemplateHandler`, `ActionsHandler`, `ExecuteHandler`, `ConfigSaveHandler`; write handler tests for each using mocked service interfaces; test `ListProcessHandler` / `ActionsHandler` / `ExecuteHandler` with `RuleSetName = null` (uses default) and with a valid named ruleset; test that an unknown `RuleSetName` produces a `RuleSetNotFound` error response; test `ExecuteHandler` broadcasts one event per result; test `ConfigSaveHandler` validates that exactly one ruleset has `IsDefault = true`
8. **`EventPipeServer`** — implement broadcast and client lifecycle; write integration test: two connected clients both receive a broadcast
9. **`CommandPipeServer`** — implement accept-read-dispatch-write loop; write integration test: send a valid request over a real named pipe, receive the expected response
10. **`ServiceWorker` + `Program.cs`** — wire the generic host; verify the service starts, accepts a connection, and responds when run interactively
11. **`Installer`** — implement all five sub-commands; deploy all three components; verify against [installer-test-plan.md](installer-test-plan.md) on a real machine
12. **`TrayApp` — `ServiceConnection`** — implement typed wrappers around `CommandPipeClient` + `EventPipeClient`; implement health-check timer
13. **`TrayApp` — `TrayApplicationContext`** — implement tray icon, context menu, balloon-tip logic, service-down state
14. **`TrayApp` — `UiLauncher`** — implement process detection and `SetForegroundWindow` P/Invoke
15. **`Ui` — `ServiceConnection`** — implement typed wrappers around `CommandPipeClient`
16. **`Ui` — `ProcessListPanel`** — implement data binding and row colouring; `Refresh(ruleSetName)` passes the active ruleset name; test with mock `ServiceConnection`
17. **`Ui` — `ActionsPanel`** — implement data binding and blocked-row colouring; `Refresh(ruleSetName)` passes the active ruleset name
18. **`Ui` — `ConfigEditorPanel`** — implement ruleset ComboBox, in-memory `RuleSetsConfig` editing, New/Delete/Set-as-Default ruleset buttons, rule-level Add/Edit/Delete, Save (full config), Add from Template with target ruleset picker
19. **`Ui` — `MainForm`** — wire tabs, toolbar, async refresh, progress overlay, error state
20. **End-to-end verification** — run the full sequence in
    [installer-test-plan.md](installer-test-plan.md): build the payload, install, start, connect
    from a **non-elevated** tray app and dashboard, round-trip a config save, confirm events
    arrive on the event pipe, upgrade over the running install, and uninstall cleanly

---

## Known limitations

Carried into a later phase rather than addressed in Phase 1:

- **Pipe name squatting** — clients do not verify the server's identity; see
  [Pipe security](#pipe-security).
- **No icon assets** — the tray uses `SystemIcons.Application` and the Start Menu shortcuts use
  each executable's default icon.
- **No code signing** — neither the binaries nor the installer are signed, so SmartScreen warns
  on first run.
- **No automatic config reload** — the service re-reads `rules.json` per request, but a change
  made outside `sfh.config.save` is not announced to connected clients.
- **`sfhi` is not an MSI** — no transactional rollback and no per-machine upgrade codes. Phase 2
  replaces it with a WiX package; see [Packaging](#packaging-stage-2--wixmsi).

---

## Packaging (Stage 2 — WiX/MSI)

`sfhi` establishes the layout and the install semantics. Once proven, packaging moves to WiX v5
while `sfhi` remains for development use:

- `src/Package/SystemFitnessHelper.Package.wixproj` using the `WixToolset.Sdk` SDK-style project,
  with an explicit component list generated from the `build.ps1` layout.
- `<ServiceInstall>` / `<ServiceControl>` replace the `sc.exe` calls; `<Shortcut>` replaces the
  COM shortcut code; `<MajorUpgrade>` replaces the hand-rolled upgrade logic; Apps & Features
  registration becomes automatic.
- The ProgramData config seed becomes a permanent component with `NeverOverwrite="yes"`, so
  uninstall leaves user configuration alone unless a `PURGE=1` property is passed.
- Code signing for both the MSI and the service binary.

The layout, the config-path resolution and every service-side fix carry over unchanged; only the
SCM, shortcut and Apps & Features plumbing inside `sfhi` is superseded.
