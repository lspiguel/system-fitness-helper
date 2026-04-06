using System.Text.Json.Serialization;

namespace SystemFitnessHelper.Ipc.Messages;

public sealed class ListParams
{
    [JsonPropertyName("configPath")]
    public string? ConfigPath { get; init; }

    [JsonPropertyName("ruleSetName")]
    public string? RuleSetName { get; init; }
}
