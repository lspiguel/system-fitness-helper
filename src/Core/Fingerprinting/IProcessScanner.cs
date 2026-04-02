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
    IReadOnlyList<ProcessFingerprint> Scan();
}
