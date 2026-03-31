namespace SystemFitnessHelper.Configuration;

public sealed class Rule
{
    public string Id { get; init; } = string.Empty;
    public string? Description { get; init; }
    public bool Enabled { get; init; } = true;
    public List<FingerprintCondition> Conditions { get; init; } = [];
    public string ConditionLogic { get; init; } = "And";
    public ActionType Action { get; init; } = ActionType.None;
}
