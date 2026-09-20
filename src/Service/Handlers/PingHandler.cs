using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SystemFitnessHelper.Ipc.Messages;

namespace SystemFitnessHelper.Service.Handlers;

/// <summary>
/// Answers a cheap liveness probe.
/// </summary>
/// <remarks>
/// Clients previously health-checked with <c>sfh.actions</c>, which enumerates every process on
/// the machine and runs the full rule match — once every ten seconds, per client, just to set a
/// boolean. Reporting the resolved config path here also makes a misconfigured path visible
/// without digging through the log.
/// </remarks>
public sealed class PingHandler : IRequestHandler
{
    private static readonly string AssemblyVersion =
        Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";

    private readonly ServiceConfig _serviceConfig;

    public string Method => Methods.Ping;

    public PingHandler(IOptions<ServiceConfig> serviceConfig)
    {
        this._serviceConfig = serviceConfig.Value;
    }

    public Task<object?> HandleAsync(JsonElement? @params, CancellationToken ct) =>
        Task.FromResult<object?>(new PingResult
        {
            Ok = true,
            Version = AssemblyVersion,
            ConfigPath = this._serviceConfig.ConfigPath,
            ConfigExists = File.Exists(this._serviceConfig.ConfigPath),
        });
}
