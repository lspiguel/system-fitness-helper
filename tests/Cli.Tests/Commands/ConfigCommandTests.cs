using FluentAssertions;
using Moq;
using SystemFitnessHelper.Cli.Commands;
using SystemFitnessHelper.Configuration;
using SystemFitnessHelper.Services;
using Xunit;

namespace SystemFitnessHelper.Cli.Tests.Commands;

public sealed class ConfigCommandTests
{
    [Fact]
    public async Task HandleAsync_ServiceReturnsError_Returns2()
    {
        var service = new Mock<IConfigService>();
        service.Setup(s => s.GetConfig(It.IsAny<string?>()))
               .Returns(new ConfigResult(null, new ValidationResult(), "No rules.json found.", 2));

        var result = await ConfigCommand.HandleAsync("any-path", "console", service.Object);

        result.Should().Be(2);
    }

    [Fact]
    public async Task HandleAsync_ServiceReturnsSuccess_Returns0()
    {
        var ruleSet = new RuleSet();
        ruleSet.Rules.Add(new Rule { Id = "r1", Enabled = true, Action = ActionType.Kill, Conditions = [] });
        var service = new Mock<IConfigService>();
        service.Setup(s => s.GetConfig(It.IsAny<string?>()))
               .Returns(new ConfigResult(ruleSet, new ValidationResult(), null, 0));

        var result = await ConfigCommand.HandleAsync("any-path", "console", service.Object);

        result.Should().Be(0);
    }

    [Fact]
    public async Task HandleAsync_ServiceReturnsFailure_Returns2()
    {
        var service = new Mock<IConfigService>();
        service.Setup(s => s.GetConfig(It.IsAny<string?>()))
               .Returns(new ConfigResult(null, new ValidationResult(), "Config is invalid.", 2));

        var result = await ConfigCommand.HandleAsync("any-path", "console", service.Object);

        result.Should().Be(2);
    }

    [Fact]
    public async Task HandleAsync_JsonOutput_WritesJsonAndReturnsExitCode()
    {
        var ruleSet = new RuleSet();
        ruleSet.Rules.Add(new Rule { Id = "r1", Enabled = true, Action = ActionType.Kill, Conditions = [] });
        var service = new Mock<IConfigService>();
        service.Setup(s => s.GetConfig(It.IsAny<string?>()))
               .Returns(new ConfigResult(ruleSet, new ValidationResult(), null, 0));

        var (exitCode, json) = await CaptureConsole(() =>
            ConfigCommand.HandleAsync("any-path", "json", service.Object));

        exitCode.Should().Be(0);
        json.Should().Contain("\"ExitCode\"");
    }

    private static async Task<(int ExitCode, string Output)> CaptureConsole(Func<Task<int>> action)
    {
        var writer = new StringWriter();
        var original = Console.Out;
        Console.SetOut(writer);
        try
        {
            var exitCode = await action();
            return (exitCode, writer.ToString().Trim());
        }
        finally
        {
            Console.SetOut(original);
        }
    }
}
