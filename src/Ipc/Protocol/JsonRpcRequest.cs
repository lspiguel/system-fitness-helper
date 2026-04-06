using System.Text.Json;
using System.Text.Json.Serialization;

namespace SystemFitnessHelper.Ipc.Protocol;

public sealed class JsonRpcRequest
{
    [JsonPropertyName("jsonrpc")]
    public string Jsonrpc { get; init; } = "2.0";

    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("method")]
    public string Method { get; init; } = string.Empty;

    [JsonPropertyName("params")]
    public JsonElement? Params { get; init; }
}
