// <copyright file="ProtectedServices.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace SystemFitnessHelper.Safety;

/// <summary>
/// Hard-coded lists of Windows service and process names that must never be targeted,
/// regardless of what the user's rule set requests.
/// These act as a last-resort safety net beneath the user-configurable protected list in <see cref="SafetyGuard"/>.
/// </summary>
public static class ProtectedServices
{
    /// <summary>Service names that must never be touched, regardless of rules.</summary>
    public static readonly IReadOnlySet<string> HardCodedServiceNames =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "wuauserv", // Windows Update
            "WinDefend", // Windows Defender Antivirus
            "MpsSvc", // Windows Firewall
            "EventLog", // Windows Event Log
            "lsass", // Local Security Authority
            "SamSs", // Security Accounts Manager
            "Schedule", // Task Scheduler
            "Winmgmt", // Windows Management Instrumentation
            "RpcSs", // Remote Procedure Call
            "DcomLaunch", // DCOM Server Process Launcher
            "nsi", // Network Store Interface Service
            "NlaSvc", // Network Location Awareness
        };

    /// <summary>Process names representing critical Windows infrastructure.</summary>
    public static readonly IReadOnlySet<string> HardCodedProcessNames =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "System",
            "Registry",
            "smss",
            "csrss",
            "wininit",
            "services",
            "lsass",
            "svchost", // Never kill svchost directly; stop individual services instead
        };
}
