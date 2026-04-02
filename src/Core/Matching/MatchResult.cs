using SystemFitnessHelper.Configuration;
using SystemFitnessHelper.Fingerprinting;

namespace SystemFitnessHelper.Matching;

/// <summary>
/// Immutable value object (record) produced by <see cref="IRuleMatcher"/> that pairs a
/// <see cref="ProcessFingerprint"/> with the <see cref="Rule"/> it satisfied.
/// </summary>
public sealed record MatchResult(ProcessFingerprint Fingerprint, Rule Rule);
