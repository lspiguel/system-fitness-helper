using System.Text.Json;
using Microsoft.Extensions.Options;
using SystemFitnessHelper.Ipc.Messages;
using SystemFitnessHelper.Ipc.Protocol;
using SystemFitnessHelper.Services;

namespace SystemFitnessHelper.Service.Handlers;

public sealed class ConfigHandler : IRequestHandler
{
    private readonly IConfigService _configService;
    private readonly ServiceConfig _serviceConfig;

    public string Method => Methods.Config;

    public ConfigHandler(IConfigService configService, IOptions<ServiceConfig> serviceConfig)
    {
        this._configService = configService;
        this._serviceConfig = serviceConfig.Value;
    }

    public Task<object?> HandleAsync(JsonElement? @params, CancellationToken ct)
    {
        ConfigParams? p = @params.HasValue
            ? JsonSerializer.Deserialize<ConfigParams>(@params.Value.GetRawText())
            : null;

        ConfigResult result = this._configService.GetConfig(p?.ConfigPath ?? this._serviceConfig.ConfigPath);

        if (result.ExitCode != 0 && result.Config is null)
            throw new JsonRpcException(JsonRpcErrorCode.ConfigNotFound, result.ErrorMessage ?? "Config not found.");

        return Task.FromResult<object?>(result);
    }
}
