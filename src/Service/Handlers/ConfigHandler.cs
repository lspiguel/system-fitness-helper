using System.Text.Json;
using SystemFitnessHelper.Ipc.Messages;
using SystemFitnessHelper.Ipc.Protocol;
using SystemFitnessHelper.Services;

namespace SystemFitnessHelper.Service.Handlers;

public sealed class ConfigHandler : IRequestHandler
{
    private readonly IConfigService _configService;

    public string Method => Methods.Config;

    public ConfigHandler(IConfigService configService)
    {
        this._configService = configService;
    }

    public Task<object?> HandleAsync(JsonElement? @params, CancellationToken ct)
    {
        ConfigParams? p = @params.HasValue
            ? JsonSerializer.Deserialize<ConfigParams>(@params.Value.GetRawText())
            : null;

        ConfigResult result = this._configService.GetConfig(p?.ConfigPath);

        if (result.ExitCode != 0 && result.Config is null)
            throw new JsonRpcException(JsonRpcErrorCode.ConfigNotFound, result.ErrorMessage ?? "Config not found.");

        return Task.FromResult<object?>(result);
    }
}
