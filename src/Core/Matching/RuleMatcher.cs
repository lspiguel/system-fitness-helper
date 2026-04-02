// <copyright file="RuleMatcher.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using SystemFitnessHelper.Configuration;
using SystemFitnessHelper.Fingerprinting;

namespace SystemFitnessHelper.Matching;

/// <summary>
/// Default implementation of <see cref="IRuleMatcher"/>.
/// Iterates all enabled rules and evaluates each fingerprint against them;
/// condition predicates within a rule are combined using the rule's <c>ConditionLogic</c> ("And" or "Or").
/// </summary>
public sealed class RuleMatcher : IRuleMatcher
{
    public IReadOnlyList<MatchResult> Match(
        IReadOnlyList<ProcessFingerprint> fingerprints,
        RuleSet ruleSet)
    {
        var results = new List<MatchResult>();
        foreach (var fp in fingerprints)
        {
            foreach (var rule in ruleSet.Rules.Where(r => r.Enabled))
            {
                if (Matches(fp, rule))
                {
                    results.Add(new MatchResult(fp, rule));
                }
            }
        }

        return results;
    }

    private static bool Matches(ProcessFingerprint fp, Rule rule)
    {
        if (rule.Conditions.Count == 0)
        {
            return false;
        }

        return rule.ConditionLogic.ToLowerInvariant() switch
        {
            "or" => rule.Conditions.Any(c => c.Evaluate(fp)),
            _ => rule.Conditions.All(c => c.Evaluate(fp)), // "And" is default
        };
    }
}
