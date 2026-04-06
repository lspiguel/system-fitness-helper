using System.Text.Json.Serialization;

namespace SystemFitnessHelper.Ipc.Messages;

public sealed class ConfigParams
{
    [JsonPropertyName("configPath")]
    public string? ConfigPath { get; init; }
}
