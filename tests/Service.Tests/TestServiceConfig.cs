using Microsoft.Extensions.Options;

namespace SystemFitnessHelper.Service.Tests;

/// <summary>
/// Builds the <see cref="IOptions{TOptions}"/> that handlers take for their config-path fallback.
/// </summary>
internal static class TestServiceConfig
{
    public const string Path = @"C:\ProgramData\SystemFitnessHelper\rules.json";

    public static IOptions<ServiceConfig> Create(string? configPath = null) =>
        Options.Create(new ServiceConfig { ConfigPath = configPath ?? Path });
}
