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
    private readonly IExecuteService _executeService;
    private readonly EventPipeServer _eventPipeServer;
    private readonly string _defaultConfigPath;

    public string Method => Methods.Execute;

    public ExecuteHandler(
        IExecuteService executeService,
        EventPipeServer eventPipeServer,
        IOptions<ServiceConfig> config)
    {
        this._executeService = executeService;
        this._eventPipeServer = eventPipeServer;
        this._defaultConfigPath = config.Value.ConfigPath;
    }

    public Task<object?> HandleAsync(JsonElement? @params, CancellationToken ct)
    {
        ExecuteParams? p = @params.HasValue
            ? JsonSerializer.Deserialize<ExecuteParams>(@params.Value.GetRawText())
            : null;

        ExecuteResult result = this._executeService.Execute(
            p?.ConfigPath ?? this._defaultConfigPath,
            p?.RuleSetName);

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
            this._eventPipeServer.Broadcast(notification);
        }

        return Task.FromResult<object?>(result);
    }
}
