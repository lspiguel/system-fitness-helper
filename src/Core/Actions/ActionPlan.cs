using SystemFitnessHelper.Configuration;
using SystemFitnessHelper.Fingerprinting;

namespace SystemFitnessHelper.Actions;

public sealed record ActionPlan(ProcessFingerprint Fingerprint, ActionType Action, string RuleId);
