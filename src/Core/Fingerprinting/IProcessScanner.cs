// <copyright file="IProcessScanner.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace SystemFitnessHelper.Fingerprinting;

/// <summary>
/// Enumerates all running processes on the current machine and returns a
/// <see cref="ProcessFingerprint"/> snapshot for each one.
/// </summary>
public interface IProcessScanner
{
    /// <summary>
    /// Scans the system for running processes and returns an immutable snapshot for each discovered process.
    /// </summary>
    /// <returns>
    /// A read-only list of <see cref="ProcessFingerprint"/> instances representing the current processes.
    /// If no processes are discovered (unlikely), an empty list is returned.
    /// </returns>
    IReadOnlyList<ProcessFingerprint> Scan();
}
