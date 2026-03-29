# System Fit Helper

## Overview

A lightweight Windows 11 system service that monitors running processes and services, identifies resource-consuming background daemons left behind by applications not actively in use, and takes action to reclaim system resources. The tool gives users granular control over what runs on their machine — starting with manual rules and evolving toward AI-assisted recommendations.

-----

## Problem Statement

Many desktop applications (browsers, creative suites, gaming platforms, communication tools) install persistent background services and scheduled tasks that consume CPU, memory, disk I/O, and network bandwidth even when the parent application isn’t running. Examples include update checkers, telemetry agents, helper services, and crash reporters. Windows provides no unified, user-friendly way to manage these.

-----

## Target Platform

- **OS:** Windows 11 (64-bit)
- **Runtime:** .NET 8+ (LTS)
- **Tooling:** Visual Studio 2022 Community / VS Code, MSTest or xUnit, Windows Service hosting via `Microsoft.Extensions.Hosting.WindowsServices`
- **Licensing constraint:** All dependencies must be MIT, Apache 2.0, BSD, or similarly permissive

-----

## Phased Development

### Phase 1 — Rule-Based Service & Process Management

A Windows Service with a companion CLI/tray application. The user maintains a predefined list of services and processes to target.

- **Configuration store** — JSON or YAML file defining rules: service names, process names, conditions (e.g. “kill when parent app is not running”), and schedules
- **Service monitor** — Enumerate running Windows services and processes via `System.Diagnostics` and `System.ServiceProcess`, match against the rule list
- **Action engine** — Stop services (`sc stop`), kill processes, or suspend them based on rule definitions
- **User triggers** — CLI commands or tray-app buttons to run a sweep on demand, enable/disable rules, and view current status
- **Logging** — Structured logging (Serilog or `Microsoft.Extensions.Logging`) with rotation, recording every action taken and its outcome
- **Safety guards** — Whitelist of critical Windows services that must never be touched; confirmation prompts for destructive actions; rollback capability to restart a stopped service

### Phase 2 — Snapshot & Unknown Process Detection

Extends Phase 1 with baseline profiling and anomaly detection.

- **Baseline snapshot** — Capture a “clean” snapshot of all running processes and services at a user-chosen moment (e.g. right after boot, before launching any apps)
- **Delta monitoring** — Periodically compare the current process/service list against the baseline; flag new or unknown entries
- **Resource attribution** — Track per-process CPU, memory, disk, and network usage over time using performance counters or ETW (Event Tracing for Windows)
- **Interactive review** — Present unknown processes to the user via the tray app or a lightweight dashboard UI, with contextual info (file path, publisher, digital signature, parent process)
- **User decisions** — Allow the user to classify each unknown process as “allow,” “kill once,” “always kill,” or “ask me next time,” persisting decisions back into the rule store

### Phase 3 — AI-Assisted Analysis & Recommendations

Adds an intelligent layer that reduces the burden on the user.

- **Process fingerprinting** — Collect metadata (name, path, publisher, command-line args, resource profile, network connections) into a structured descriptor for each unknown process
- **AI integration** — Send fingerprints to a local or cloud-based LLM (Anthropic Claude API via `Anthropic.SDK` NuGet package) for analysis, asking: “What is this process? Is it safe to stop? What are the consequences?”
- **Recommendation engine** — Present AI-generated recommendations with confidence levels and supporting reasoning; the user always has final approval
- **Learning loop** — Store user decisions alongside AI recommendations to improve future suggestions (local SQLite or LiteDB)
- **Privacy controls** — All process metadata stays local by default; cloud AI calls are opt-in with clear disclosure of what data is sent

-----

## Quality Requirements

- **Unit test coverage:** ≥ 95% line coverage across all phases, enforced in CI
- **Testing framework:** xUnit + Moq (or NSubstitute) for mocking OS-level APIs behind interfaces
- **Integration tests:** Separate suite for real service start/stop operations, run in a sandboxed environment
- **Static analysis:** Enable `dotnet analyzers` and treat warnings as errors

-----

## Non-Functional Requirements

- The System Fit Helper service itself must have a minimal resource footprint (target < 30 MB RAM, < 1% CPU at idle)
- All destructive actions must be reversible or at minimum logged with enough detail to manually undo
- The system must degrade gracefully if the AI backend is unavailable (fall back to Phase 2 behavior)
- Configuration and rule changes must not require a service restart
