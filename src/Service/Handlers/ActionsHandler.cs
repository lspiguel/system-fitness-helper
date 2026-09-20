using System.Text.Json;
using Microsoft.Extensions.Options;
using SystemFitnessHelper.Ipc.Messages;
using SystemFitnessHelper.Ipc.Protocol;
using SystemFitnessHelper.Services;

namespace SystemFitnessHelper.Service.Handlers;

public sealed class ActionsHandler : IRequestHandler
{
    private readonly IActionsService _actionsService;
    private readonly ServiceConfig _serviceConfig;

    public string Method => Methods.Actions;

    public ActionsHandler(IActionsService actionsService, IOptions<ServiceConfig> serviceConfig)
    {
        this._actionsService = actionsService;
        this._serviceConfig = serviceConfig.Value;
    }

    public Task<object?> HandleAsync(JsonElement? @params, CancellationToken ct)
    {
        ActionsParams? p = @params.HasValue
            ? JsonSerializer.Deserialize<ActionsParams>(@params.Value.GetRawText())
            : null;

        ActionsResult result = this._actionsService.GetActions(
            p?.ConfigPath ?? this._serviceConfig.ConfigPath,
            p?.RuleSetName);

        if (result.ExitCode != 0 && result.ResolvedRuleSetName is null && p?.RuleSetName is not null)
            throw new JsonRpcException(JsonRpcErrorCode.RuleSetNotFound, result.ErrorMessage ?? $"RuleSet '{p.RuleSetName}' not found.");

        if (result.ExitCode != 0 && result.ErrorMessage is not null && result.ResolvedRuleSetName is null)
            throw new JsonRpcException(JsonRpcErrorCode.ConfigNotFound, result.ErrorMessage);

        return Task.FromResult<object?>(result);
    }
}
