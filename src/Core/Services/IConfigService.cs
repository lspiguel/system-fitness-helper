// <copyright file="IConfigService.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace SystemFitnessHelper.Services;

/// <summary>
/// Loads and validates the rule configuration file.
/// </summary>
public interface IConfigService
{
    /// <summary>
    /// Discovers and loads the rule file at the given path (or auto-discovers it).
    /// </summary>
    /// <param name="configPath">Explicit path to rules.json, or <c>null</c> to auto-discover.</param>
    /// <returns>A <see cref="ConfigResult"/> describing the loaded rule set and any validation issues.</returns>
    ConfigResult GetConfig(string? configPath);
}
