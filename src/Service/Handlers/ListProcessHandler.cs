using System.Text.Json;
using Microsoft.Extensions.Options;
using SystemFitnessHelper.Ipc.Messages;
using SystemFitnessHelper.Ipc.Protocol;
using SystemFitnessHelper.Services;

namespace SystemFitnessHelper.Service.Handlers;

public sealed class ListProcessHandler : IRequestHandler
{
    private readonly IListService _listService;
    private readonly string _defaultConfigPath;

    public string Method => Methods.List;

    public ListProcessHandler(IListService listService, IOptions<ServiceConfig> config)
    {
        this._listService = listService;
        this._defaultConfigPath = config.Value.ConfigPath;
    }

    public Task<object?> HandleAsync(JsonElement? @params, CancellationToken ct)
    {
        ListParams? p = @params.HasValue
            ? JsonSerializer.Deserialize<ListParams>(@params.Value.GetRawText())
            : null;

        ProcessListResult result = this._listService.GetProcessList(
            p?.ConfigPath ?? this._defaultConfigPath,
            p?.RuleSetName);

        if (result.ExitCode != 0 && result.ResolvedRuleSetName is null && p?.RuleSetName is not null)
            throw new JsonRpcException(JsonRpcErrorCode.RuleSetNotFound, result.ErrorMessage ?? $"RuleSet '{p.RuleSetName}' not found.");

        if (result.ExitCode != 0 && result.ErrorMessage is not null && result.ResolvedRuleSetName is null)
            throw new JsonRpcException(JsonRpcErrorCode.ConfigNotFound, result.ErrorMessage);

        return Task.FromResult<object?>(result);
    }
}
