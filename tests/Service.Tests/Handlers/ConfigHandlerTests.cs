using System.Text.Json;
using FluentAssertions;
using Moq;
using SystemFitnessHelper.Configuration;
using SystemFitnessHelper.Ipc.Protocol;
using SystemFitnessHelper.Service.Handlers;
using SystemFitnessHelper.Services;
using Xunit;

namespace SystemFitnessHelper.Service.Tests.Handlers;

public sealed class ConfigHandlerTests
{
    [Fact]
    public async Task HandleAsync_ValidConfig_ReturnsConfigResult()
    {
        RuleSetsConfig config = new() { RuleSets = { ["default"] = new RuleSet { IsDefault = true } } };
        ConfigResult result = new(config, ["default"], new ValidationResult(), null, 0);

        Mock<IConfigService> mock = new();
        mock.Setup(s => s.GetConfig(It.IsAny<string?>())).Returns(result);

        ConfigHandler handler = new(mock.Object);

        object? output = await handler.HandleAsync(null, CancellationToken.None);

        output.Should().BeEquivalentTo(result);
    }

    [Fact]
    public async Task HandleAsync_ConfigNotFound_ThrowsJsonRpcException()
    {
        ConfigResult result = new(null, [], new ValidationResult(), "No config found.", 2);

        Mock<IConfigService> mock = new();
        mock.Setup(s => s.GetConfig(It.IsAny<string?>())).Returns(result);

        ConfigHandler handler = new(mock.Object);

        Func<Task> act = () => handler.HandleAsync(null, CancellationToken.None);

        await act.Should().ThrowAsync<JsonRpcException>()
            .Where(e => e.Code == JsonRpcErrorCode.ConfigNotFound);
    }

    [Fact]
    public async Task HandleAsync_UsesParamsConfigPath_WhenProvided()
    {
        string capturedPath = string.Empty;
        ConfigResult result = new(
            new RuleSetsConfig { RuleSets = { ["default"] = new RuleSet { IsDefault = true } } },
            ["default"], new ValidationResult(), null, 0);

        Mock<IConfigService> mock = new();
        mock.Setup(s => s.GetConfig(It.IsAny<string?>()))
            .Callback<string?>(p => capturedPath = p ?? string.Empty)
            .Returns(result);

        ConfigHandler handler = new(mock.Object);

        JsonElement paramsEl = JsonSerializer.SerializeToElement(new { configPath = @"D:\custom\rules.json" });
        await handler.HandleAsync(paramsEl, CancellationToken.None);

        capturedPath.Should().Be(@"D:\custom\rules.json");
    }
}
