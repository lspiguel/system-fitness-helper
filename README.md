# System Fitness Helper

A Windows tool that finds and stops background processes left behind by apps you're not using — update checkers, telemetry agents, crash reporters — and reclaims the CPU, memory, and bandwidth they waste.

## How it works

You define rules describing which services and processes to target. The tool fingerprints running processes, matches them against your rules, and stops or kills the ones that shouldn't be running. Actions are logged and reversible.

The project is built in phases, starting with a simple CLI and growing into a background service with a tray app, then adding baseline snapshots and unknown-process detection, and hopefully AI-assisted recommendations.

## Status

Phase 1 is functionally complete: a Windows Service, a system-tray app and a dashboard, talking
over JSON-RPC 2.0 on named pipes, plus a CLI. Installed either from an MSI or with the `sfhi`
development installer.

One item remains before Phase 1 closes: **nothing is code-signed yet**, so Windows shows a
SmartScreen warning and an unverified publisher on first run. See
[Stage 3 — Code Signing](docs/phase-1.md#stage-3--code-signing).

See [docs/plan.md](docs/plan.md) for the full design, [docs/phase-1.md](docs/phase-1.md) for how
the current version is built, and [docs/installation.md](docs/installation.md) to install it.

## Platform

Windows 10/11 — .NET (LTS) — MIT License
