namespace SystemFitnessHelper.Configuration;

/// <summary>
/// Root configuration object deserialised from <c>rules.json</c>.
/// Holds the list of matching rules and the user-defined protected service names.
/// </summary>
public sealed class RuleSet
{
    public List<Rule> Rules { get; init; } = [];
    public List<string> Protected { get; init; } = [];
}
