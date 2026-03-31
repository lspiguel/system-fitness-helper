using SystemFitnessHelper.Configuration;
using SystemFitnessHelper.Fingerprinting;

namespace SystemFitnessHelper.Matching;

public interface IRuleMatcher
{
    IReadOnlyList<MatchResult> Match(
        IReadOnlyList<ProcessFingerprint> fingerprints,
        RuleSet ruleSet);
}
