using System.Text.Json.Serialization;
using SystemFitnessHelper.Configuration;
using SystemFitnessHelper.Services;

namespace SystemFitnessHelper.Ipc.Messages.Events;

public sealed class ActionExecutedEvent
{
    [JsonPropertyName("processName")]
    public string ProcessName { get; init; } = string.Empty;

    [JsonPropertyName("processId")]
    public int ProcessId { get; init; }

    [JsonPropertyName("serviceName")]
    public string? ServiceName { get; init; }

    [JsonPropertyName("ruleId")]
    public string RuleId { get; init; } = string.Empty;

    [JsonPropertyName("action")]
    public ActionType Action { get; init; }

    [JsonPropertyName("success")]
    public bool Success { get; init; }

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; init; }

    public static ActionExecutedEvent From(ActionResultView result) =>
        new()
        {
            ProcessName = result.ProcessName,
            ProcessId = result.ProcessId,
            ServiceName = result.ServiceName,
            RuleId = result.RuleId,
            Action = result.Action,
            Success = result.Success,
            Message = result.Message,
            Timestamp = DateTimeOffset.UtcNow,
        };
}
