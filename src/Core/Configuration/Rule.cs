namespace SystemFitnessHelper.Configuration;

/// <summary>
/// Defines a single matching rule: a set of <see cref="FingerprintCondition"/> predicates combined
/// by <see cref="ConditionLogic"/> ("And" or "Or"), and the <see cref="ActionType"/> to apply when matched.
/// </summary>
public sealed class Rule
{
    public string Id { get; init; } = string.Empty;
    public string? Description { get; init; }
    public bool Enabled { get; init; } = true;
    public List<FingerprintCondition> Conditions { get; init; } = [];
    public string ConditionLogic { get; init; } = "And";
    public ActionType Action { get; init; } = ActionType.None;
}
