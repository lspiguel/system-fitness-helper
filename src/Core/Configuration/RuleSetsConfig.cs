// <copyright file="RuleSetsConfig.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace SystemFitnessHelper.Configuration;

/// <summary>
/// Root deserialization model for the multi-ruleset config file.
/// Holds a dictionary of named <see cref="RuleSet"/> entries; exactly one must have <see cref="RuleSet.IsDefault"/> set to <c>true</c>.
/// </summary>
public sealed class RuleSetsConfig
{
    /// <summary>
    /// Gets the map of named rulesets. Keys are case-insensitive for lookup purposes but preserved as-written for display.
    /// </summary>
    public Dictionary<string, RuleSet> RuleSets { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}
