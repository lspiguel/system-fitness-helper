using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SystemFitnessHelper.Ipc.Messages;
using SystemFitnessHelper.Ipc.Pipes;
using SystemFitnessHelper.Ipc.Protocol;
using SystemFitnessHelper.Service.Handlers;
using SystemFitnessHelper.Service.Pipes;
using Xunit;

namespace SystemFitnessHelper.Service.Tests.Pipes;

/// <summary>
/// Regression tests for the command pipe's concurrency behaviour.
/// </summary>
/// <remarks>
/// The server used to allow a single pipe instance and handle each request inline. One slow
/// request therefore blocked every other client past its connect timeout, and the log filled with
/// <c>IOException: Pipe is broken</c> as the server answered clients that had already given up.
/// These tests run against a real named pipe.
/// </remarks>
[Collection("CommandPipeServer")]
public sealed class CommandPipeServerConcurrencyTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task ConcurrentClients_EachReceiveTheirOwnResponse()
    {
        const int clientCount = 10;

        // Echoes the caller's ruleSetName, so a response delivered to the wrong client is visible.
        Mock<IRequestHandler> handler = new();
        handler.SetupGet(h => h.Method).Returns(Methods.Actions);
        handler.Setup(h => h.HandleAsync(It.IsAny<JsonElement?>(), It.IsAny<CancellationToken>()))
            .Returns<JsonElement?, CancellationToken>(async (p, _) =>
            {
                string echo = p!.Value.GetProperty("ruleSetName").GetString()!;
                await Task.Delay(100).ConfigureAwait(false);
                return new Echo { Value = echo };
            });

        await using ServerHarness harness = await ServerHarness.StartAsync(handler.Object);

        IEnumerable<Task<string>> calls = Enumerable.Range(0, clientCount).Select(async i =>
        {
            CommandPipeClient client = new();
            Echo result = await client.SendAsync<Echo>(
                Methods.Actions,
                new ActionsParams { RuleSetName = $"ruleset-{i}" },
                default,
                TestTimeout).ConfigureAwait(false);
            return result.Value;
        });

        string[] results = await Task.WhenAll(calls);

        results.Should().BeEquivalentTo(
            Enumerable.Range(0, clientCount).Select(i => $"ruleset-{i}"),
            "every concurrent client must get its own response back");
    }

    [Fact]
    public async Task SlowRequest_DoesNotBlockOtherClients()
    {
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        Mock<IRequestHandler> slow = new();
        slow.SetupGet(h => h.Method).Returns(Methods.Execute);
        slow.Setup(h => h.HandleAsync(It.IsAny<JsonElement?>(), It.IsAny<CancellationToken>()))
            .Returns<JsonElement?, CancellationToken>(async (_, _) =>
            {
                await release.Task.ConfigureAwait(false);
                return new Echo { Value = "slow" };
            });

        Mock<IRequestHandler> fast = new();
        fast.SetupGet(h => h.Method).Returns(Methods.Ping);
        fast.Setup(h => h.HandleAsync(It.IsAny<JsonElement?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Echo { Value = "fast" });

        await using ServerHarness harness = await ServerHarness.StartAsync(slow.Object, fast.Object);

        // Occupy the server with a request that will not return until we let it.
        Task<Echo> slowCall = new CommandPipeClient()
            .SendAsync<Echo>(Methods.Execute, null, default, TestTimeout);

        // The fast call must complete while the slow one is still in flight.
        Echo fastResult = await new CommandPipeClient()
            .SendAsync<Echo>(Methods.Ping, null, default, TimeSpan.FromSeconds(10));

        fastResult.Value.Should().Be("fast");
        slowCall.IsCompleted.Should().BeFalse("the slow request should still be running");

        release.SetResult();
        (await slowCall).Value.Should().Be("slow");
    }

    private sealed class Echo
    {
        public string Value { get; init; } = string.Empty;
    }

    private sealed class ServerHarness : IAsyncDisposable
    {
        private readonly CommandPipeServer _server;

        private ServerHarness(CommandPipeServer server) => this._server = server;

        public static async Task<ServerHarness> StartAsync(params IRequestHandler[] handlers)
        {
            HandlerDispatcher dispatcher = new(handlers, NullLogger<HandlerDispatcher>.Instance);
            CommandPipeServer server = new(dispatcher, NullLogger<CommandPipeServer>.Instance);
            await server.StartAsync(CancellationToken.None);

            // Give the accept loop a moment to publish the first pipe instance.
            await Task.Delay(200);
            return new ServerHarness(server);
        }

        public async ValueTask DisposeAsync() =>
            await this._server.StopAsync(CancellationToken.None);
    }
}

/// <summary>
/// Keeps pipe-server tests off each other's pipe name, which is a fixed machine-wide resource.
/// </summary>
[CollectionDefinition("CommandPipeServer", DisableParallelization = true)]
public sealed class CommandPipeServerCollection
{
}
