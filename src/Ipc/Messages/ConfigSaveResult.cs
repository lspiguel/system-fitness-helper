using System.Text.Json.Serialization;

namespace SystemFitnessHelper.Ipc.Messages;

public sealed class ConfigSaveResult
{
    [JsonPropertyName("success")]
    public bool Success { get; init; }

    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; init; }

    public static ConfigSaveResult Ok() => new() { Success = true };

    public static ConfigSaveResult Fail(string message) => new() { Success = false, ErrorMessage = message };
}
