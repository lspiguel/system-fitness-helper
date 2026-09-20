using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SystemFitnessHelper.Configuration;
using SystemFitnessHelper.Ipc.Messages;
using SystemFitnessHelper.Service.Handlers;
using SystemFitnessHelper.Service.Pipes;
using SystemFitnessHelper.Services;
using Xunit;

namespace SystemFitnessHelper.Service.Tests.Handlers;

/// <summary>
/// The read handlers used to pass <c>params.ConfigPath</c> straight through, so a request from the
/// TrayApp or Ui — which always sends null — reached Core as null and only found the right file
/// because <c>ConfigurationLoader.DiscoverPath</c> happens to probe ProgramData first. The service
/// must use its own configured path instead of relying on that.
/// </summary>
public sealed class ConfigPathFallbackTests
{
    private const string ServicePath = @"C:\ProgramData\SystemFitnessHelper\rules.json";
    private const string ExplicitPath = @"D:\custom\rules.json";

    [Fact]
    public async Task ConfigHandler_NullParams_UsesServiceConfigPath()
    {
        string? captured = null;
        Mock<IConfigService> mock = new();
        mock.Setup(s => s.GetConfig(It.IsAny<string?>()))
            .Callback<string?>(p => captured = p)
            .Returns(new ConfigResult(
                new RuleSetsConfig { RuleSets = { ["default"] = new RuleSet { IsDefault = true } } },
                ["default"], new ValidationResult(), null, 0));

        ConfigHandler handler = new(mock.Object, TestServiceConfig.Create(ServicePath));

        await handler.HandleAsync(null, CancellationToken.None);

        captured.Should().Be(ServicePath);
    }

    [Fact]
    public async Task ListProcessHandler_NullParams_UsesServiceConfigPath()
    {
        string? captured = null;
        Mock<IListService> mock = new();
        mock.Setup(s => s.GetProcessList(It.IsAny<string?>(), It.IsAny<string?>()))
            .Callback<string?, string?>((p, _) => captured = p)
            .Returns(new ProcessListResult([], [], "default", null, 0));

        ListProcessHandler handler = new(mock.Object, TestServiceConfig.Create(ServicePath));

        await handler.HandleAsync(null, CancellationToken.None);

        captured.Should().Be(ServicePath);
    }

    [Fact]
    public async Task ActionsHandler_NullParams_UsesServiceConfigPath()
    {
        string? captured = null;
        Mock<IActionsService> mock = new();
        mock.Setup(s => s.GetActions(It.IsAny<string?>(), It.IsAny<string?>()))
            .Callback<string?, string?>((p, _) => captured = p)
            .Returns(new ActionsResult([], "default", null, 0));

        ActionsHandler handler = new(mock.Object, TestServiceConfig.Create(ServicePath));

        await handler.HandleAsync(null, CancellationToken.None);

        captured.Should().Be(ServicePath);
    }

    [Fact]
    public async Task ExecuteHandler_NullParams_UsesServiceConfigPath()
    {
        string? captured = null;
        Mock<IExecuteService> mock = new();
        mock.Setup(s => s.Execute(It.IsAny<string?>(), It.IsAny<string?>()))
            .Callback<string?, string?>((p, _) => captured = p)
            .Returns(new ExecuteResult([], false, "default", null, 0));

        ExecuteHandler handler = new(
            mock.Object,
            new EventPipeServer(NullLogger<EventPipeServer>.Instance),
            TestServiceConfig.Create(ServicePath));

        await handler.HandleAsync(null, CancellationToken.None);

        captured.Should().Be(ServicePath);
    }

    [Fact]
    public async Task ExplicitParamsConfigPath_StillWins()
    {
        string? captured = null;
        Mock<IActionsService> mock = new();
        mock.Setup(s => s.GetActions(It.IsAny<string?>(), It.IsAny<string?>()))
            .Callback<string?, string?>((p, _) => captured = p)
            .Returns(new ActionsResult([], "default", null, 0));

        ActionsHandler handler = new(mock.Object, TestServiceConfig.Create(ServicePath));
        JsonElement paramsEl = JsonSerializer.SerializeToElement(new { configPath = ExplicitPath });

        await handler.HandleAsync(paramsEl, CancellationToken.None);

        captured.Should().Be(ExplicitPath);
    }
}
