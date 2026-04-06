using System.Text.Json;
using Microsoft.Extensions.Options;
using SystemFitnessHelper.Configuration;
using SystemFitnessHelper.Ipc.Messages;
using SystemFitnessHelper.Ipc.Protocol;

namespace SystemFitnessHelper.Service.Handlers;

public sealed class ConfigSaveHandler : IRequestHandler
{
    private readonly string _defaultConfigPath;

    public string Method => Methods.ConfigSave;

    public ConfigSaveHandler(IOptions<ServiceConfig> config)
    {
        this._defaultConfigPath = config.Value.ConfigPath;
    }

    public Task<object?> HandleAsync(JsonElement? @params, CancellationToken ct)
    {
        if (!@params.HasValue)
            return Task.FromResult<object?>(ConfigSaveResult.Fail("Missing params."));

        ConfigSaveParams? p = JsonSerializer.Deserialize<ConfigSaveParams>(@params.Value.GetRawText());

        if (p?.RuleSetsConfig is null)
            return Task.FromResult<object?>(ConfigSaveResult.Fail("RuleSetsConfig is required."));

        RuleSetsConfig ruleSetsConfig = p.RuleSetsConfig;

        if (!ruleSetsConfig.RuleSets.Values.Any(rs => rs.IsDefault))
            return Task.FromResult<object?>(ConfigSaveResult.Fail("Exactly one ruleset must have IsDefault = true."));

        string targetPath = p.ConfigPath ?? this._defaultConfigPath;

        try
        {
            string dir = Path.GetDirectoryName(targetPath)
                ?? throw new InvalidOperationException($"Cannot determine directory for path '{targetPath}'.");
            Directory.CreateDirectory(dir);

            string json = JsonSerializer.Serialize(ruleSetsConfig, new JsonSerializerOptions { WriteIndented = true });
            string tmpPath = targetPath + ".tmp";
            File.WriteAllText(tmpPath, json);
            File.Replace(tmpPath, targetPath, null);

            return Task.FromResult<object?>(ConfigSaveResult.Ok());
        }
        catch (Exception ex)
        {
            return Task.FromResult<object?>(ConfigSaveResult.Fail($"Failed to write config: {ex.Message}"));
        }
    }
}
