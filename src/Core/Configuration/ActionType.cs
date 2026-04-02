// <copyright file="ActionType.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace SystemFitnessHelper.Configuration;

/// <summary>
/// Specifies the remediation action to take against a matched process or service.
/// </summary>
public enum ActionType
{
    /// <summary>
    /// No action.
    /// </summary>
    None,

    /// <summary>
    /// Stop service.
    /// </summary>
    Stop,

    /// <summary>
    /// Kill process.
    /// </summary>
    Kill,

    /// <summary>
    /// Suspend process (Windows only).
    /// </summary>
    Suspend,
}
