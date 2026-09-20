using System.Text.Json;
using Microsoft.Extensions.Options;
using SystemFitnessHelper.Ipc.Messages;
using SystemFitnessHelper.Ipc.Messages.Events;
using SystemFitnessHelper.Ipc.Protocol;
using SystemFitnessHelper.Service.Pipes;
using SystemFitnessHelper.Services;

namespace SystemFitnessHelper.Service.Handlers;

public sealed class ExecuteHandler : IRequestHandler
{
    /// <summary>
    /// Serialises execution. The command pipe now handles requests concurrently, and two clients
    /// running the action plan at once would race to stop the same services and kill the same PIDs.
    /// </summary>
    private static readonly SemaphoreSlim ExecuteLock = new(1, 1);

    private readonly IExecuteService _executeService;
    private readonly EventPipeServer _eventPipeServer;
    private readonly ServiceConfig _serviceConfig;

    public string Method => Methods.Execute;

    public ExecuteHandler(
        IExecuteService executeService,
        EventPipeServer eventPipeServer,
        IOptions<ServiceConfig> serviceConfig)
    {
        this._executeService = executeService;
        this._eventPipeServer = eventPipeServer;
        this._serviceConfig = serviceConfig.Value;
    }

    public async Task<object?> HandleAsync(JsonElement? @params, CancellationToken ct)
    {
        ExecuteParams? p = @params.HasValue
            ? JsonSerializer.Deserialize<ExecuteParams>(@params.Value.GetRawText())
            : null;

        await ExecuteLock.WaitAsync(ct).ConfigureAwait(false);

        ExecuteResult result;
        try
        {
            result = this._executeService.Execute(
                p?.ConfigPath ?? this._serviceConfig.ConfigPath,
                p?.RuleSetName);
        }
        finally
        {
            ExecuteLock.Release();
        }

        if (result.ExitCode != 0 && result.ResolvedRuleSetName is null && p?.RuleSetName is not null)
            throw new JsonRpcException(JsonRpcErrorCode.RuleSetNotFound, result.ErrorMessage ?? $"RuleSet '{p.RuleSetName}' not found.");

        if (result.ExitCode != 0 && result.ErrorMessage is not null && result.ResolvedRuleSetName is null)
            throw new JsonRpcException(JsonRpcErrorCode.ConfigNotFound, result.ErrorMessage);

        foreach (var actionResult in result.Results)
        {
            var notification = new JsonRpcNotification
            {
                Method = Methods.ActionExecuted,
                Params = JsonSerializer.SerializeToElement(ActionExecutedEvent.From(actionResult)),
            };
            await this._eventPipeServer.BroadcastAsync(notification, ct).ConfigureAwait(false);
        }

        return result;
    }
}
