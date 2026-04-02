// <copyright file="RuleSet.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace SystemFitnessHelper.Configuration;

/// <summary>
/// Root configuration object deserialised from <c>rules.json</c>.
/// Holds the list of matching rules and the user-defined protected service names.
/// </summary>
public sealed class RuleSet
{
    public List<Rule> Rules { get; init; } = [];
    public List<string> Protected { get; init; } = [];
}
