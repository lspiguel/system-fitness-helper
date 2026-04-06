using FluentAssertions;
using Moq;
using SystemFitnessHelper.Cli.Commands;
using SystemFitnessHelper.Configuration;
using SystemFitnessHelper.Services;
using Xunit;

namespace SystemFitnessHelper.Cli.Tests.Commands;

public sealed class ExecuteCommandTests
{
    [Fact]
    public async Task HandleAsync_NotElevated_ReturnsWithoutCallingServices()
    {
        var actionsService = new Mock<IActionsService>();
        var executeService = new Mock<IExecuteService>();

        var result = await ExecuteCommand.HandleAsync(
            "any-path", "console", skipPrompt: true, ruleSetName: null,
            actionsService.Object, executeService.Object,
            isElevated: () => false,
            relaunchAsAdmin: () => 42);

        result.Should().Be(42);
        executeService.Verify(s => s.Execute(It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_Console_ErrorResult_Returns2()
    {
        var actionsService = new Mock<IActionsService>();
        actionsService.Setup(s => s.GetActions(It.IsAny<string?>(), It.IsAny<string?>()))
                      .Returns(new ActionsResult([], null, "No rules.json found.", 2));
        var executeService = new Mock<IExecuteService>();

        var result = await ExecuteCommand.HandleAsync(
            "any-path", "console", skipPrompt: true, ruleSetName: null,
            actionsService.Object, executeService.Object,
            isElevated: () => true);

        result.Should().Be(2);
        executeService.Verify(s => s.Execute(It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_Console_NoPlans_Returns0WithoutExecuting()
    {
        var actionsService = new Mock<IActionsService>();
        actionsService.Setup(s => s.GetActions(It.IsAny<string?>(), It.IsAny<string?>()))
                      .Returns(new ActionsResult([], "work", null, 0));
        var executeService = new Mock<IExecuteService>();

        var result = await ExecuteCommand.HandleAsync(
            "any-path", "console", skipPrompt: true, ruleSetName: null,
            actionsService.Object, executeService.Object,
            isElevated: () => true);

        result.Should().Be(0);
        executeService.Verify(s => s.Execute(It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_Console_SuccessfulExecution_Returns0()
    {
        var plans = new[]
        {
            new ActionPlanView("notepad", 1234, null, "r1", ActionType.Kill, false, null),
        };
        var actionsService = new Mock<IActionsService>();
        actionsService.Setup(s => s.GetActions(It.IsAny<string?>(), It.IsAny<string?>()))
                      .Returns(new ActionsResult(plans, "work", null, 0));
        var executeResult = new ExecuteResult(
            [new ActionResultView("notepad", 1234, null, "r1", ActionType.Kill, true, "done")],
            false, "work", null, 0);
        var executeService = new Mock<IExecuteService>();
        executeService.Setup(s => s.Execute(It.IsAny<string?>(), It.IsAny<string?>())).Returns(executeResult);

        var result = await ExecuteCommand.HandleAsync(
            "any-path", "console", skipPrompt: true, ruleSetName: null,
            actionsService.Object, executeService.Object,
            isElevated: () => true);

        result.Should().Be(0);
        executeService.Verify(s => s.Execute(It.IsAny<string?>(), It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Console_FailedExecution_Returns1()
    {
        var plans = new[]
        {
            new ActionPlanView("notepad", 1234, null, "r1", ActionType.Kill, false, null),
        };
        var actionsService = new Mock<IActionsService>();
        actionsService.Setup(s => s.GetActions(It.IsAny<string?>(), It.IsAny<string?>()))
                      .Returns(new ActionsResult(plans, "work", null, 0));
        var executeResult = new ExecuteResult(
            [new ActionResultView("notepad", 1234, null, "r1", ActionType.Kill, false, "process not found")],
            true, "work", null, 1);
        var executeService = new Mock<IExecuteService>();
        executeService.Setup(s => s.Execute(It.IsAny<string?>(), It.IsAny<string?>())).Returns(executeResult);

        var result = await ExecuteCommand.HandleAsync(
            "any-path", "console", skipPrompt: true, ruleSetName: null,
            actionsService.Object, executeService.Object,
            isElevated: () => true);

        result.Should().Be(1);
    }

    [Fact]
    public async Task HandleAsync_JsonMode_SkipsPromptAndSerializesResult()
    {
        var executeResult = new ExecuteResult([], false, "work", null, 0);
        var actionsService = new Mock<IActionsService>();
        var executeService = new Mock<IExecuteService>();
        executeService.Setup(s => s.Execute(It.IsAny<string?>(), It.IsAny<string?>())).Returns(executeResult);

        var (exitCode, json) = await CaptureConsole(() =>
            ExecuteCommand.HandleAsync(
                "any-path", "json", skipPrompt: false, ruleSetName: null,   // prompt is skipped automatically in json mode
                actionsService.Object, executeService.Object,
                isElevated: () => true));

        exitCode.Should().Be(0);
        json.Should().Contain("\"ExitCode\"");
        actionsService.Verify(s => s.GetActions(It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_Console_PassesRuleSetNameToBothServices()
    {
        var plans = new[]
        {
            new ActionPlanView("notepad", 1234, null, "r1", ActionType.Kill, false, null),
        };
        var actionsService = new Mock<IActionsService>();
        actionsService.Setup(s => s.GetActions(It.IsAny<string?>(), "gaming"))
                      .Returns(new ActionsResult(plans, "gaming", null, 0));
        var executeResult = new ExecuteResult([], false, "gaming", null, 0);
        var executeService = new Mock<IExecuteService>();
        executeService.Setup(s => s.Execute(It.IsAny<string?>(), "gaming")).Returns(executeResult);

        var result = await ExecuteCommand.HandleAsync(
            "any-path", "console", skipPrompt: true, ruleSetName: "gaming",
            actionsService.Object, executeService.Object,
            isElevated: () => true);

        result.Should().Be(0);
        actionsService.Verify(s => s.GetActions(It.IsAny<string?>(), "gaming"), Times.Once);
        executeService.Verify(s => s.Execute(It.IsAny<string?>(), "gaming"), Times.Once);
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
