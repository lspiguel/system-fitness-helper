using System.Text.Json;
using System.Text.Json.Serialization;

namespace SystemFitnessHelper.Ipc.Protocol;

public sealed class JsonRpcNotification
{
    [JsonPropertyName("jsonrpc")]
    public string Jsonrpc { get; init; } = "2.0";

    [JsonPropertyName("method")]
    public string Method { get; init; } = string.Empty;

    [JsonPropertyName("params")]
    public JsonElement? Params { get; init; }
}
