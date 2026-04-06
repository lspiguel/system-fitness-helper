using System.Text.Json;

namespace SystemFitnessHelper.Service.Handlers;

public interface IRequestHandler
{
    string Method { get; }
    Task<object?> HandleAsync(JsonElement? @params, CancellationToken ct);
}
