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
        service.Setup(s => s.GetActions(It.IsAny<string?>()))
               .Returns(new ActionsResult([], "No rules.json found.", 2));

        var result = await ActionsCommand.HandleAsync("any-path", "console", service.Object);

        result.Should().Be(2);
    }

    [Fact]
    public async Task HandleAsync_NoPlans_Returns0()
    {
        var service = new Mock<IActionsService>();
        service.Setup(s => s.GetActions(It.IsAny<string?>()))
               .Returns(new ActionsResult([], null, 0));

        var result = await ActionsCommand.HandleAsync("any-path", "console", service.Object);

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
        service.Setup(s => s.GetActions(It.IsAny<string?>()))
               .Returns(new ActionsResult(plans, null, 0));

        var result = await ActionsCommand.HandleAsync("any-path", "console", service.Object);

        result.Should().Be(0);   // actions command never fails on blocked items
    }
}
