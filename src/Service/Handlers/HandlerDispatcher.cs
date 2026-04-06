using Microsoft.Extensions.Logging;
using SystemFitnessHelper.Ipc.Protocol;

namespace SystemFitnessHelper.Service.Handlers;

public sealed class HandlerDispatcher
{
    private readonly Dictionary<string, IRequestHandler> _handlers;
    private readonly ILogger<HandlerDispatcher> _logger;

    public HandlerDispatcher(IEnumerable<IRequestHandler> handlers, ILogger<HandlerDispatcher> logger)
    {
        this._handlers = handlers.ToDictionary(h => h.Method, StringComparer.Ordinal);
        this._logger = logger;
    }

    public async Task<JsonRpcResponse> DispatchAsync(JsonRpcRequest request, CancellationToken ct)
    {
        if (!this._handlers.TryGetValue(request.Method, out IRequestHandler? handler))
        {
            return JsonRpcResponse.Failure(request.Id, JsonRpcErrorCode.MethodNotFound,
                $"No handler registered for method '{request.Method}'.");
        }

        try
        {
            object? result = await handler.HandleAsync(request.Params, ct).ConfigureAwait(false);
            return JsonRpcResponse.Success(request.Id, result);
        }
        catch (JsonRpcException ex)
        {
            return JsonRpcResponse.Failure(request.Id, ex.Code, ex.Message);
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Unhandled exception in handler for method '{Method}'.", request.Method);
            return JsonRpcResponse.Failure(request.Id, JsonRpcErrorCode.InternalError,
                "An internal error occurred while processing the request.");
        }
    }
}
