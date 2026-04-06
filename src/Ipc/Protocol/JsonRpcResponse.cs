using System.Text.Json;
using System.Text.Json.Serialization;

namespace SystemFitnessHelper.Ipc.Protocol;

public sealed class JsonRpcResponse
{
    [JsonPropertyName("jsonrpc")]
    public string Jsonrpc { get; init; } = "2.0";

    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("result")]
    public JsonElement? Result { get; init; }

    [JsonPropertyName("error")]
    public JsonRpcError? Error { get; init; }

    public static JsonRpcResponse Success(int id, object? result, System.Text.Json.Serialization.Metadata.JsonTypeInfo? typeInfo = null)
    {
        JsonElement element = result is null
            ? default
            : JsonSerializer.SerializeToElement(result);
        return new() { Id = id, Result = element };
    }

    public static JsonRpcResponse Failure(int id, JsonRpcErrorCode code, string message) =>
        new() { Id = id, Error = JsonRpcError.From(code, message) };
}
