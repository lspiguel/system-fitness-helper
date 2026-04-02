using FluentAssertions;
using Moq;
using System.ServiceProcess;
using SystemFitnessHelper.Actions;
using SystemFitnessHelper.Configuration;
using SystemFitnessHelper.Fingerprinting;
using Xunit;

namespace SystemFitnessHelper.Tests.Actions;

public sealed class ActionExecutorTests
{
    private static ProcessFingerprint MakeServiceFp(string serviceName) =>
        new(1234, "svcexe", null, null, null, 0, null, true, serviceName, serviceName,
            ServiceControllerStatus.Running);

    private static ProcessFingerprint MakeProcessFp(string processName) =>
        new(1234, processName, null, null, null, 0, null, false, null, null, null);

    [Fact]
    public void Execute_KillOnService_ReturnsFail()
    {
        var executor = new WindowsActionExecutor();
        var plan     = new ActionPlan(MakeServiceFp("MySvc"), ActionType.Kill, "test-rule");

        var result = executor.Execute(plan);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Stop");
    }

    [Fact]
    public void Execute_SuspendOnService_ReturnsFail()
    {
        var executor = new WindowsActionExecutor();
        var plan     = new ActionPlan(MakeServiceFp("MySvc"), ActionType.Suspend, "test-rule");

        var result = executor.Execute(plan);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public void Execute_NoneAction_ReturnsSuccess()
    {
        var executor = new WindowsActionExecutor();
        var plan     = new ActionPlan(MakeProcessFp("notepad"), ActionType.None, "test-rule");

        var result = executor.Execute(plan);

        result.Success.Should().BeTrue();
    }

    [Fact]
    public void MockExecutor_CanBeUsedInTests()
    {
        var mock = new Mock<IActionExecutor>();
        mock.Setup(e => e.Execute(It.IsAny<ActionPlan>()))
            .Returns(ActionResult.Ok("Mock success"));

        var plan   = new ActionPlan(MakeProcessFp("notepad"), ActionType.Kill, "test-rule");
        var result = mock.Object.Execute(plan);

        result.Success.Should().BeTrue();
        result.Message.Should().Be("Mock success");
    }

    [Fact]
    public void ActionResult_Ok_HasSuccessTrue()
    {
        var result = ActionResult.Ok("all good");
        result.Success.Should().BeTrue();
        result.Message.Should().Be("all good");
        result.Exception.Should().BeNull();
    }

    [Fact]
    public void ActionResult_Fail_HasSuccessFalse()
    {
        var ex     = new InvalidOperationException("oops");
        var result = ActionResult.Fail("something broke", ex);

        result.Success.Should().BeFalse();
        result.Message.Should().Be("something broke");
        result.Exception.Should().BeSameAs(ex);
    }
}
