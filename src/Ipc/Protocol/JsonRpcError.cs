using System.Text.Json.Serialization;

namespace SystemFitnessHelper.Ipc.Protocol;

public sealed class JsonRpcError
{
    [JsonPropertyName("code")]
    public int Code { get; init; }

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    public static JsonRpcError From(JsonRpcErrorCode code, string message) =>
        new() { Code = (int)code, Message = message };
}
