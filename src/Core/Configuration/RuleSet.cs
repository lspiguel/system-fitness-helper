namespace SystemFitnessHelper.Configuration;

public sealed class RuleSet
{
    public List<Rule> Rules { get; init; } = [];
    public List<string> Protected { get; init; } = [];
}
