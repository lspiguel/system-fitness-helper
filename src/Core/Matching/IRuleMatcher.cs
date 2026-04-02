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
