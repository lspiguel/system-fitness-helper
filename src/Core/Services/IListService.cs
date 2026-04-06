// <copyright file="IListService.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using SystemFitnessHelper.Configuration;

namespace SystemFitnessHelper.Services;

/// <summary>
/// Enumerates running processes and matches them against the configured rules.
/// </summary>
public interface IListService
{
    /// <summary>
    /// Scans running processes and matches them against the loaded rule set.
    /// </summary>
    /// <param name="configPath">Explicit path to rules.json, or <c>null</c> to auto-discover.</param>
    /// <returns>A <see cref="ProcessListResult"/> with all fingerprints and matches.</returns>
    ProcessListResult GetProcessList(string? configPath);

    /// <summary>
    /// Builds a RuleSet template from the currently running processes (no config required).
    /// </summary>
    /// <returns>A <see cref="RuleSet"/> with one disabled template rule per unique process/service.</returns>
    RuleSet BuildTemplate();
}
