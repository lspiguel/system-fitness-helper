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
               .Returns(new ConfigResult(null, [], new ValidationResult(), "No rules.json found.", 2));

        var result = await ConfigCommand.HandleAsync("any-path", "console", service.Object);

        result.Should().Be(2);
    }

    [Fact]
    public async Task HandleAsync_ServiceReturnsSuccess_Returns0()
    {
        var config = MakeConfig("work", isDefault: true, ruleId: "r1");
        var service = new Mock<IConfigService>();
        service.Setup(s => s.GetConfig(It.IsAny<string?>()))
               .Returns(new ConfigResult(config, ["work"], new ValidationResult(), null, 0));

        var result = await ConfigCommand.HandleAsync("any-path", "console", service.Object);

        result.Should().Be(0);
    }

    [Fact]
    public async Task HandleAsync_ServiceReturnsFailure_Returns2()
    {
        var service = new Mock<IConfigService>();
        service.Setup(s => s.GetConfig(It.IsAny<string?>()))
               .Returns(new ConfigResult(null, [], new ValidationResult(), "Config is invalid.", 2));

        var result = await ConfigCommand.HandleAsync("any-path", "console", service.Object);

        result.Should().Be(2);
    }

    [Fact]
    public async Task HandleAsync_JsonOutput_WritesJsonAndReturnsExitCode()
    {
        var config = MakeConfig("work", isDefault: true, ruleId: "r1");
        var service = new Mock<IConfigService>();
        service.Setup(s => s.GetConfig(It.IsAny<string?>()))
               .Returns(new ConfigResult(config, ["work"], new ValidationResult(), null, 0));

        var (exitCode, json) = await CaptureConsole(() =>
            ConfigCommand.HandleAsync("any-path", "json", service.Object));

        exitCode.Should().Be(0);
        json.Should().Contain("\"ExitCode\"");
    }

    [Fact]
    public async Task HandleAsync_MultipleRulesets_RendersAll()
    {
        var config = new RuleSetsConfig
        {
            RuleSets = new Dictionary<string, RuleSet>(StringComparer.OrdinalIgnoreCase)
            {
                ["work"] = new RuleSet { IsDefault = true, Rules = [], Protected = [] },
                ["gaming"] = new RuleSet { IsDefault = false, Rules = [], Protected = [] },
            },
        };
        var service = new Mock<IConfigService>();
        service.Setup(s => s.GetConfig(It.IsAny<string?>()))
               .Returns(new ConfigResult(config, ["gaming", "work"], new ValidationResult(), null, 0));

        var result = await ConfigCommand.HandleAsync("any-path", "console", service.Object);

        result.Should().Be(0);
    }

    private static RuleSetsConfig MakeConfig(string name, bool isDefault, string ruleId)
    {
        var ruleSet = new RuleSet { IsDefault = isDefault, Rules = [], Protected = [] };
        ruleSet.Rules.Add(new Rule { Id = ruleId, Enabled = true, Action = ActionType.Kill, Conditions = [] });
        return new RuleSetsConfig
        {
            RuleSets = new Dictionary<string, RuleSet>(StringComparer.OrdinalIgnoreCase)
            {
                [name] = ruleSet,
            },
        };
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
