// <copyright file="IRuleMatcher.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using SystemFitnessHelper.Configuration;
using SystemFitnessHelper.Fingerprinting;

namespace SystemFitnessHelper.Matching;

/// <summary>
/// Performs matching between process fingerprints and a set of rules.
/// </summary>
/// <remarks>
/// Implementations evaluate the provided <see cref="ProcessFingerprint"/> collection
/// against the rules contained in a <see cref="RuleSet"/> and produce a collection
/// of <see cref="MatchResult"/> instances representing each fingerprint/rule pair
/// that satisfied at least one enabled rule.
/// </remarks>
public interface IRuleMatcher
{
    /// <summary>
    /// Matches the given process fingerprints against the provided rule set.
    /// </summary>
    /// <param name="fingerprints">
    /// A read-only list of <see cref="ProcessFingerprint"/> instances to evaluate.
    /// The list may be empty, in which case the result should be an empty list.
    /// </param>
    /// <param name="ruleSet">
    /// The <see cref="RuleSet"/> that contains the rules to apply. Implementations
    /// should consider rule enablement state when determining matches.
    /// </param>
    /// <returns>
    /// A read-only list of <see cref="MatchResult"/> objects. Each entry represents
    /// a fingerprint and the rule that matched it. Only fingerprint/rule pairs that
    /// satisfied at least one enabled rule are included.
    /// </returns>
    IReadOnlyList<MatchResult> Match(
        IReadOnlyList<ProcessFingerprint> fingerprints,
        RuleSet ruleSet);
}
