using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SystemFitnessHelper.Ipc.Protocol;
using SystemFitnessHelper.Service.Handlers;
using Xunit;

namespace SystemFitnessHelper.Service.Tests.Pipes;

public sealed class HandlerDispatcherTests
{
    [Fact]
    public async Task DispatchAsync_UnknownMethod_ReturnsMethodNotFoundError()
    {
        HandlerDispatcher dispatcher = new([], NullLogger<HandlerDispatcher>.Instance);
        JsonRpcRequest request = new() { Id = 1, Method = "sfh.unknown" };

        JsonRpcResponse response = await dispatcher.DispatchAsync(request, CancellationToken.None);

        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be((int)JsonRpcErrorCode.MethodNotFound);
    }

    [Fact]
    public async Task DispatchAsync_HandlerThrowsJsonRpcException_ReturnsErrorResponse()
    {
        Mock<IRequestHandler> handler = new();
        handler.SetupGet(h => h.Method).Returns("sfh.config");
        handler.Setup(h => h.HandleAsync(It.IsAny<JsonElement?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new JsonRpcException(JsonRpcErrorCode.ConfigNotFound, "Config not found."));

        HandlerDispatcher dispatcher = new([handler.Object], NullLogger<HandlerDispatcher>.Instance);
        JsonRpcRequest request = new() { Id = 2, Method = "sfh.config" };

        JsonRpcResponse response = await dispatcher.DispatchAsync(request, CancellationToken.None);

        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be((int)JsonRpcErrorCode.ConfigNotFound);
        response.Error.Message.Should().Be("Config not found.");
    }

    [Fact]
    public async Task DispatchAsync_HandlerThrowsUnexpectedException_ReturnsInternalError()
    {
        Mock<IRequestHandler> handler = new();
        handler.SetupGet(h => h.Method).Returns("sfh.list");
        handler.Setup(h => h.HandleAsync(It.IsAny<JsonElement?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Something went wrong internally."));

        HandlerDispatcher dispatcher = new([handler.Object], NullLogger<HandlerDispatcher>.Instance);
        JsonRpcRequest request = new() { Id = 3, Method = "sfh.list" };

        JsonRpcResponse response = await dispatcher.DispatchAsync(request, CancellationToken.None);

        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be((int)JsonRpcErrorCode.InternalError);
        response.Error.Message.Should().NotContain("Something went wrong internally"); // no leakage
    }

    [Fact]
    public async Task DispatchAsync_HandlerSucceeds_ReturnsResultResponse()
    {
        Mock<IRequestHandler> handler = new();
        handler.SetupGet(h => h.Method).Returns("sfh.actions");
        handler.Setup(h => h.HandleAsync(It.IsAny<JsonElement?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new { ok = true });

        HandlerDispatcher dispatcher = new([handler.Object], NullLogger<HandlerDispatcher>.Instance);
        JsonRpcRequest request = new() { Id = 4, Method = "sfh.actions" };

        JsonRpcResponse response = await dispatcher.DispatchAsync(request, CancellationToken.None);

        response.Error.Should().BeNull();
        response.Result.Should().NotBeNull();
    }
}
