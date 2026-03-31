using SystemFitnessHelper.Configuration;
using SystemFitnessHelper.Fingerprinting;

namespace SystemFitnessHelper.Matching;

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
                    results.Add(new MatchResult(fp, rule));
            }
        }
        return results;
    }

    private static bool Matches(ProcessFingerprint fp, Rule rule)
    {
        if (rule.Conditions.Count == 0)
            return false;

        return rule.ConditionLogic.ToLowerInvariant() switch
        {
            "or" => rule.Conditions.Any(c => c.Evaluate(fp)),
            _    => rule.Conditions.All(c => c.Evaluate(fp)),  // "And" is default
        };
    }
}
