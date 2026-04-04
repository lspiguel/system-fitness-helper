// <copyright file="ConfigurationBuilder.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using SystemFitnessHelper.Fingerprinting;

namespace SystemFitnessHelper.Configuration;

/// <summary>
/// Builds a <see cref="RuleSet"/> template from a snapshot of running processes and services,
/// producing one disabled <see cref="Rule"/> per fingerprint as a starting point for user configuration.
/// </summary>
public static class ConfigurationBuilder
{
    /// <summary>
    /// Converts a list of <see cref="ProcessFingerprint"/> entries into a <see cref="RuleSet"/>
    /// where each rule targets the corresponding process or service by name, is disabled by default,
    /// and has the appropriate action (<see cref="ActionType.Stop"/> for services,
    /// <see cref="ActionType.Kill"/> for plain processes).
    /// </summary>
    public static RuleSet Build(IReadOnlyList<ProcessFingerprint> fingerprints)
    {
        var rules = fingerprints
            .Select((fp, index) => new Rule
            {
                Id = $"rule-{index + 1:D4}",
                Description = fp.IsService
                    ? (fp.ServiceDisplayName ?? fp.ServiceName ?? fp.ProcessName)
                    : fp.ProcessName,
                Enabled = false,
                Conditions =
                [
                    new FingerprintCondition
                    {
                        Field = fp.IsService ? "ServiceName" : "ProcessName",
                        Op = "eq",
                        Value = fp.IsService ? (fp.ServiceName ?? fp.ProcessName) : fp.ProcessName,
                    },
                ],
                Action = fp.IsService ? ActionType.Stop : ActionType.Kill,
            })
            .ToList();

        return new RuleSet { Rules = rules };
    }
}
