using FluentAssertions;
using Moq;
using SystemFitnessHelper.Cli.Commands;
using SystemFitnessHelper.Configuration;
using SystemFitnessHelper.Services;
using Xunit;

namespace SystemFitnessHelper.Cli.Tests.Commands;

public sealed class ActionsCommandTests
{
    [Fact]
    public async Task HandleAsync_ErrorResult_Returns2()
    {
        var service = new Mock<IActionsService>();
        service.Setup(s => s.GetActions(It.IsAny<string?>(), It.IsAny<string?>()))
               .Returns(new ActionsResult([], null, "No rules.json found.", 2));

        var result = await ActionsCommand.HandleAsync("any-path", "console", null, service.Object);

        result.Should().Be(2);
    }

    [Fact]
    public async Task HandleAsync_NoPlans_Returns0()
    {
        var service = new Mock<IActionsService>();
        service.Setup(s => s.GetActions(It.IsAny<string?>(), It.IsAny<string?>()))
               .Returns(new ActionsResult([], "work", null, 0));

        var result = await ActionsCommand.HandleAsync("any-path", "console", null, service.Object);

        result.Should().Be(0);
    }

    [Fact]
    public async Task HandleAsync_WithPlansIncludingBlocked_Returns0()
    {
        var plans = new[]
        {
            new ActionPlanView("notepad", 1234, null, "r1", ActionType.Kill, false, null),
            new ActionPlanView("svchost",  5678, "MyService", "r2", ActionType.Stop, true, "Protected service."),
        };
        var service = new Mock<IActionsService>();
        service.Setup(s => s.GetActions(It.IsAny<string?>(), It.IsAny<string?>()))
               .Returns(new ActionsResult(plans, "work", null, 0));

        var result = await ActionsCommand.HandleAsync("any-path", "console", null, service.Object);

        result.Should().Be(0);   // actions command never fails on blocked items
    }

    [Fact]
    public async Task HandleAsync_PassesRuleSetNameToService()
    {
        var service = new Mock<IActionsService>();
        service.Setup(s => s.GetActions(It.IsAny<string?>(), "gaming"))
               .Returns(new ActionsResult([], "gaming", null, 0));

        var result = await ActionsCommand.HandleAsync("any-path", "console", "gaming", service.Object);

        result.Should().Be(0);
        service.Verify(s => s.GetActions(It.IsAny<string?>(), "gaming"), Times.Once);
    }
}
