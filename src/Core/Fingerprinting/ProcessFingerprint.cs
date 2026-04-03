// <copyright file="ProcessFingerprint.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using System.ServiceProcess;

namespace SystemFitnessHelper.Fingerprinting;

/// <summary>
/// Immutable snapshot (record) of a running process captured by <see cref="IProcessScanner"/>.
/// Aggregates identity, path, command-line, publisher, resource usage, parent process,
/// and optional Windows Service metadata for use by the rule-matching pipeline.
/// </summary>
/// <param name="ProcessId">The operating system process identifier (PID).</param>
/// <param name="ProcessName">The process executable name (for example, <c>notepad.exe</c>).</param>
/// <param name="ExecutablePath">Full path to the process executable, or <c>null</c> if unavailable.</param>
/// <param name="CommandLine">The full command line used to start the process, or <c>null</c> if unavailable.</param>
/// <param name="Publisher">The signing publisher or product company string if obtainable, otherwise <c>null</c>.</param>
/// <param name="WorkingSetBytes">Current working set memory usage in bytes.</param>
/// <param name="ParentProcessName">Name of the parent process if known; otherwise <c>null</c>.</param>
/// <param name="IsService">Indicates whether the process hosts a Windows Service.</param>
/// <param name="ServiceName">The service short name when <paramref name="IsService"/> is <c>true</c>; otherwise <c>null</c>.</param>
/// <param name="ServiceDisplayName">The user-facing service display name when applicable; otherwise <c>null</c>.</param>
/// <param name="ServiceStatus">Current <see cref="ServiceControllerStatus"/> for the service when applicable; otherwise <c>null</c>.</param>
public sealed record ProcessFingerprint(
    int ProcessId,
    string ProcessName,
    string? ExecutablePath,
    string? CommandLine,
    string? Publisher,
    long WorkingSetBytes,
    string? ParentProcessName,
    bool IsService,
    string? ServiceName,
    string? ServiceDisplayName,
    ServiceControllerStatus? ServiceStatus);
