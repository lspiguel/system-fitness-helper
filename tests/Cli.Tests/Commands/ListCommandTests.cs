using System.Text.Json;
using FluentAssertions;
using Moq;
using SystemFitnessHelper.Cli.Commands;
using SystemFitnessHelper.Configuration;
using SystemFitnessHelper.Fingerprinting;
using SystemFitnessHelper.Matching;
using SystemFitnessHelper.Services;
using Xunit;

namespace SystemFitnessHelper.Cli.Tests.Commands;

public sealed class ListCommandTests
{
    // -------------------------------------------------------------------------
    // format=table, output=console
    // -------------------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_Table_Console_ErrorResult_Returns2()
    {
        var service = new Mock<IListService>();
        service.Setup(s => s.GetProcessList(It.IsAny<string?>()))
               .Returns(new ProcessListResult([], [], "No rules.json found.", 2));

        var result = await ListCommand.HandleAsync("any-path", "table", "console", service.Object);

        result.Should().Be(2);
    }

    [Fact]
    public async Task HandleAsync_Table_Console_NoMatches_Returns0()
    {
        var service = new Mock<IListService>();
        service.Setup(s => s.GetProcessList(It.IsAny<string?>()))
               .Returns(new ProcessListResult([], [], null, 0));

        var result = await ListCommand.HandleAsync("any-path", "table", "console", service.Object);

        result.Should().Be(0);
    }

    [Fact]
    public async Task HandleAsync_Table_Console_WithMatches_Returns0()
    {
        var fp = PlainProcess("notepad");
        var rule = new Rule { Id = "r1", Enabled = true, Action = ActionType.Kill, Conditions = [] };
        var service = new Mock<IListService>();
        service.Setup(s => s.GetProcessList(It.IsAny<string?>()))
               .Returns(new ProcessListResult([fp], [new MatchResult(fp, rule)], null, 0));

        var result = await ListCommand.HandleAsync("any-path", "table", "console", service.Object);

        result.Should().Be(0);
    }

    // -------------------------------------------------------------------------
    // format=table, output=json
    // -------------------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_Table_Json_WritesJsonAndReturnsExitCode()
    {
        var service = new Mock<IListService>();
        service.Setup(s => s.GetProcessList(It.IsAny<string?>()))
               .Returns(new ProcessListResult([], [], null, 0));

        var (exitCode, json) = await CaptureConsole(() =>
            ListCommand.HandleAsync("any-path", "table", "json", service.Object));

        exitCode.Should().Be(0);
        json.Should().Contain("\"ExitCode\"");
    }

    // -------------------------------------------------------------------------
    // format=template
    // -------------------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_Template_OutputsRuleSetJsonAndReturns0()
    {
        var ruleSet = new RuleSet();
        var service = new Mock<IListService>();
        service.Setup(s => s.BuildTemplate()).Returns(ruleSet);

        var (exitCode, json) = await CaptureConsole(() =>
            ListCommand.HandleAsync(null, "template", "console", service.Object));

        exitCode.Should().Be(0);
        var deserialized = JsonSerializer.Deserialize<RuleSet>(json, JsonOptions);
        deserialized.Should().NotBeNull();
        service.Verify(s => s.GetProcessList(It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_Template_NeverCallsGetProcessList()
    {
        var service = new Mock<IListService>();
        service.Setup(s => s.BuildTemplate()).Returns(new RuleSet());

        await CaptureConsole(() =>
            ListCommand.HandleAsync(null, "template", "console", service.Object));

        service.Verify(s => s.GetProcessList(It.IsAny<string?>()), Times.Never);
    }

    // -------------------------------------------------------------------------
    // helpers
    // -------------------------------------------------------------------------

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    private static ProcessFingerprint PlainProcess(string name) =>
        new(1234, name, null, null, null, 0, null, false, null, null, null);

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
