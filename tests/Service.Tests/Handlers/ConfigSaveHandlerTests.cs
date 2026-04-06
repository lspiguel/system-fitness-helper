using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Options;
using SystemFitnessHelper.Configuration;
using SystemFitnessHelper.Ipc.Messages;
using SystemFitnessHelper.Service.Handlers;
using Xunit;

namespace SystemFitnessHelper.Service.Tests.Handlers;

public sealed class ConfigSaveHandlerTests
{
    private static IOptions<ServiceConfig> DefaultOptions() =>
        Options.Create(new ServiceConfig { ConfigPath = @"C:\test\rules.json" });

    [Fact]
    public async Task HandleAsync_NoDefaultRuleSet_ReturnsFail()
    {
        RuleSetsConfig config = new()
        {
            RuleSets = { ["work"] = new RuleSet { IsDefault = false } },
        };
        ConfigSaveParams p = new() { RuleSetsConfig = config };
        JsonElement paramsEl = JsonSerializer.SerializeToElement(p);

        ConfigSaveHandler handler = new(DefaultOptions());
        object? output = await handler.HandleAsync(paramsEl, CancellationToken.None);

        output.Should().BeOfType<ConfigSaveResult>()
            .Which.Success.Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsync_NullConfig_ReturnsFail()
    {
        JsonElement paramsEl = JsonSerializer.SerializeToElement(new { configPath = (string?)null });

        ConfigSaveHandler handler = new(DefaultOptions());
        object? output = await handler.HandleAsync(paramsEl, CancellationToken.None);

        output.Should().BeOfType<ConfigSaveResult>()
            .Which.Success.Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsync_NullParams_ReturnsFail()
    {
        ConfigSaveHandler handler = new(DefaultOptions());
        object? output = await handler.HandleAsync(null, CancellationToken.None);

        output.Should().BeOfType<ConfigSaveResult>()
            .Which.Success.Should().BeFalse();
    }
}
