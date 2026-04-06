using System.Text.Json.Serialization;
using SystemFitnessHelper.Configuration;

namespace SystemFitnessHelper.Ipc.Messages;

public sealed class ConfigSaveParams
{
    [JsonPropertyName("ruleSetsConfig")]
    public RuleSetsConfig? RuleSetsConfig { get; init; }

    [JsonPropertyName("configPath")]
    public string? ConfigPath { get; init; }
}
