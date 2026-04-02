using SystemFitnessHelper.Configuration;
using SystemFitnessHelper.Fingerprinting;

namespace SystemFitnessHelper.Actions;

/// <summary>
/// Immutable value object (record) that pairs a matched <see cref="ProcessFingerprint"/> with the
/// <see cref="ActionType"/> to execute and the ID of the rule that triggered it.
/// </summary>
public sealed record ActionPlan(ProcessFingerprint Fingerprint, ActionType Action, string RuleId);
