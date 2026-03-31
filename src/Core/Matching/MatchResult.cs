using SystemFitnessHelper.Configuration;
using SystemFitnessHelper.Fingerprinting;

namespace SystemFitnessHelper.Matching;

public sealed record MatchResult(ProcessFingerprint Fingerprint, Rule Rule);
