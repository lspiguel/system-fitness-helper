using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SystemFitnessHelper.Configuration;
using SystemFitnessHelper.Ipc.Protocol;
using SystemFitnessHelper.Service.Handlers;
using SystemFitnessHelper.Service.Pipes;
using SystemFitnessHelper.Services;
using Xunit;

namespace SystemFitnessHelper.Service.Tests.Handlers;

public sealed class ExecuteHandlerTests
{
    private static EventPipeServer CreateEventServer() =>
        new(NullLogger<EventPipeServer>.Instance);

    [Fact]
    public async Task HandleAsync_SuccessfulExecution_ReturnsResult()
    {
        ActionResultView resultView = new("chrome", 1234, null, "rule1", ActionType.Kill, true, "Killed.");
        ExecuteResult executeResult = new([resultView], false, "default", null, 0);

        Mock<IExecuteService> mock = new();
        mock.Setup(s => s.Execute(It.IsAny<string?>(), It.IsAny<string?>())).Returns(executeResult);

        ExecuteHandler handler = new(mock.Object, CreateEventServer());

        object? output = await handler.HandleAsync(null, CancellationToken.None);

        output.Should().BeEquivalentTo(executeResult);
    }

    [Fact]
    public async Task HandleAsync_UnknownRuleSet_ThrowsRuleSetNotFoundError()
    {
        ExecuteResult executeResult = new([], false, null, "RuleSet 'gaming' not found.", 2);

        Mock<IExecuteService> mock = new();
        mock.Setup(s => s.Execute(It.IsAny<string?>(), "gaming")).Returns(executeResult);

        ExecuteHandler handler = new(mock.Object, CreateEventServer());
        JsonElement paramsEl = JsonSerializer.SerializeToElement(new { ruleSetName = "gaming" });

        Func<Task> act = () => handler.HandleAsync(paramsEl, CancellationToken.None);

        await act.Should().ThrowAsync<JsonRpcException>()
            .Where(e => e.Code == JsonRpcErrorCode.RuleSetNotFound);
    }
}
