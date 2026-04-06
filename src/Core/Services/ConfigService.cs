// <copyright file="ConfigService.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using SystemFitnessHelper.Configuration;

namespace SystemFitnessHelper.Services;

/// <summary>
/// Default implementation of <see cref="IConfigService"/>.
/// </summary>
public sealed class ConfigService : IConfigService
{
    /// <inheritdoc/>
    public ConfigResult GetConfig(string? configPath)
    {
        var path = ConfigurationLoader.DiscoverPath(configPath);
        if (path is null)
        {
            return new ConfigResult(
                Config: null,
                AvailableRuleSetNames: [],
                Validation: new ValidationResult(),
                ErrorMessage: "No rules.json found. Use --config to specify a path.",
                ExitCode: 2);
        }

        var (config, validation) = ConfigurationLoader.Load(path);
        var names = config is not null
            ? config.RuleSets.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList()
            : (IReadOnlyList<string>)[];

        return new ConfigResult(
            Config: config,
            AvailableRuleSetNames: names,
            Validation: validation,
            ErrorMessage: null,
            ExitCode: validation.IsValid ? 0 : 2);
    }
}
