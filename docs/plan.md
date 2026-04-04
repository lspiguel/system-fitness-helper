# System Fitness Helper

## Overview

A lightweight Windows system service that monitors running processes and services, identifies resource-consuming background daemons left behind by applications not actively in use, and takes action to reclaim system resources. The tool gives users granular control over what runs on their machine — starting with manual rules and evolving toward AI-assisted recommendations.

-----

## Problem Statement

Many desktop applications (browsers, creative suites, gaming platforms, communication tools) install persistent background services and scheduled tasks that consume CPU, memory, disk I/O, and network bandwidth even when the parent application isn't running. Examples include update checkers, telemetry agents, helper services, and crash reporters. Windows provides no unified, user-friendly way to manage these.

-----

## Target Platform

- **OS:** Windows 10/11 (64-bit)
- **Runtime:** .NET (LTS)
- **Tooling:** Visual Studio Community / VS Code, xUnit, Windows Service hosting via `Microsoft.Extensions.Hosting.WindowsServices`
- **Licensing:** MIT

-----

## Phased Development

### [Phase 0 — Rule-Based CLI](phase-0.md)

A command-line tool the user runs on demand. The user maintains a predefined list of services and processes to target.

- **Configuration store** — JSON file defining rules: service names, process names, fingerprint, conditions (e.g. "do not touch", "kill when parent app is not running")
- **Process fingerprinting** — Collect metadata (name, path, publisher, command-line args, resource profile, network connections) into a structured descriptor for each process
- **Fuzzy matching** — Use regular expressions and expressions/logic against the fingerprint to allow easier process recognition when applications change over time
- **Service monitor** — Enumerate running Windows services and processes via `System.Diagnostics` and `System.ServiceProcess`, build fingerprint, match against the rule list
- **Action engine** — Stop services (`sc stop`), kill processes, or suspend them based on rule definitions
- **CLI commands**
  - `config` — Display the current configuration and validate its syntax
  - `list` — Enumerate all running processes and services, highlighting those matched by configuration rules
  - `actions` — List the actions the engine would take based on current rules and running processes
  - `execute` — Run all targeted actions and report the result of each
- **Logging** — Structured logging (Serilog or `Microsoft.Extensions.Logging`) with rotation, recording every action taken and its outcome
- **Safety guards** — Whitelist of critical Windows services that must never be touched; confirmation prompts for destructive actions; rollback capability to restart a stopped service

### Phase 0.B - Refactoring for Messaging

A preliminary step consisting on creating a new Service layer on top of Core, that implements the same logic as the config, list, actions and execute commands, receives a message and does not use console output, instead producing object instances as a response (this will be later used for JSON-RPC). All CLI commands will now include the --output json global option, so that this change will be used as an intermediate step toward Phase 1.

### Phase 1 — Windows Service & Tray Application

Moves the Phase 0 logic into a background Windows Service and adds a tray application as the user-facing interface. No new functional capabilities beyond what the CLI already provided. 

- **Windows Service host** — Wrap the existing engine in a `Microsoft.Extensions.Hosting.WindowsServices` host so it runs in the background without a console window, listens on a command named pipe, publishes events on another named pipe (to notify on actions taken on schedule)
- **Tray application** — Minimalistic system-tray icon with a context menu that allows to run an execute command (thru the command named pipe) and listens for events (on the event named pipe) to display transient information to the user, or to launch the UI component (the idea being that the system tray has a minimal overhead while the UI Component has most of the system load impact, but is used only when needed)
- **UI Component** - Full user interface that communicates with the service via the command pipe to execute the list, actions and execute commands and display the responses in a full Windows Forms UI, also allowing retrieval of the current configuration and to edit it by changing rules, and creating new ones based on user manual input or by adding rules obtained thru the list command with JSON output 
- **IPC channel** — Lightweight local communication (named pipe) between the tray app and the service so the tray can send commands and receive status updates, consists of a command pipe to send to the service and obtain responses, and a event pipe where the service publishes notifications; all communication uses JSON-RPC 2.0
- **Service installer** — Tooling to install, start, stop, and uninstall the Windows Service, deploys a minimal initial configuration

### Phase 2 — Snapshot & Unknown Process Detection

Extends Phase 1 with baseline profiling and anomaly detection.

- **Baseline snapshot** — Capture a "clean" snapshot of all running processes and services at a user-chosen moment (e.g. right after boot, before launching any apps)
- **Delta monitoring** — Periodically compare the current process/service list against the baseline; flag new or unknown entries
- **Resource attribution** — Track per-process CPU, memory, disk, and network usage over time using performance counters or ETW (Event Tracing for Windows)
- **Interactive review** — Present on demand unknown processes to the user via the tray app or a lightweight dashboard UI, with contextual info (file path, publisher, digital signature, parent process, resource usage)
- **User decisions** — Allow the user to classify each unknown process as "allow," "kill once," "always kill," or "ask me next time," persisting decisions back into the rule store

### Phase 3 — AI-Assisted Analysis & Recommendations

Adds an intelligent layer that reduces the burden on the user.

- **AI integration** — Send fingerprints to a local or cloud-based LLM for analysis, asking: "What is this process? Is it safe to stop? What are the consequences?"
- **Recommendation engine** — Present AI-generated recommendations with confidence levels and supporting reasoning; the user always has final approval
- **Learning loop** — Store user decisions alongside AI recommendations to improve future suggestions (local SQLite or LiteDB)
- **Privacy controls** — All process metadata stays local by default; cloud AI calls are opt-in with clear disclosure of what data is sent

-----

## Quality Requirements

- **Unit test coverage:** ≥ 95% line coverage across all phases, enforced in CI
- **Testing framework:** xUnit + Moq for mocking OS-level APIs behind interfaces
- **Integration tests:** Separate suite for real service start/stop operations, run in a sandboxed environment
- **Static analysis:** Enable `dotnet analyzers` and treat warnings as errors

-----

## Non-Functional Requirements

- The System Fitness Helper service itself must have a minimal resource footprint (target < 30 MB RAM, < 1% CPU at idle)
- All destructive actions must be reversible or at minimum logged with enough detail to manually undo
- The system must degrade gracefully if the AI backend is unavailable (fall back to Phase 2 behavior)
- Configuration and rule changes must not require a service restart
