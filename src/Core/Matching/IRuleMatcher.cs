// <copyright file="IRuleMatcher.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using SystemFitnessHelper.Configuration;
using SystemFitnessHelper.Fingerprinting;

namespace SystemFitnessHelper.Matching;

/// <summary>
/// Matches a collection of process fingerprints against a rule set and returns every
/// fingerprint/rule pair that satisfied at least one enabled rule.
/// </summary>
public interface IRuleMatcher
{
    IReadOnlyList<MatchResult> Match(
        IReadOnlyList<ProcessFingerprint> fingerprints,
        RuleSet ruleSet);
}
