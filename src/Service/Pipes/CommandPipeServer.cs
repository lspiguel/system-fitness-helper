using System.IO.Pipes;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SystemFitnessHelper.Ipc.Pipes;
using SystemFitnessHelper.Ipc.Protocol;
using SystemFitnessHelper.Service.Handlers;

namespace SystemFitnessHelper.Service.Pipes;

public sealed class CommandPipeServer
{
    private readonly HandlerDispatcher _dispatcher;
    private readonly ILogger<CommandPipeServer> _logger;
    private CancellationTokenSource? _cts;
    private Task? _acceptLoop;

    public CommandPipeServer(HandlerDispatcher dispatcher, ILogger<CommandPipeServer> logger)
    {
        this._dispatcher = dispatcher;
        this._logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        this._cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        this._acceptLoop = Task.Run(() => this.RunAcceptLoopAsync(this._cts.Token));
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (this._cts is not null)
            await this._cts.CancelAsync().ConfigureAwait(false);

        if (this._acceptLoop is not null)
        {
            try { await this._acceptLoop.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }
    }

    private async Task RunAcceptLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            NamedPipeServerStream? server = null;
            try
            {
                server = new NamedPipeServerStream(
                    PipeConstants.SfhCommand,
                    PipeDirection.InOut,
                    maxNumberOfServerInstances: 1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await server.WaitForConnectionAsync(ct).ConfigureAwait(false);

                string requestJson;
                try
                {
                    requestJson = await PipeFraming.ReadMessageAsync(server, ct).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    this._logger.LogWarning(ex, "Failed to read request from client.");
                    server.Disconnect();
                    server.Dispose();
                    continue;
                }

                JsonRpcRequest? request = null;
                JsonRpcResponse response;

                try
                {
                    request = JsonSerializer.Deserialize<JsonRpcRequest>(requestJson);
                }
                catch (JsonException ex)
                {
                    this._logger.LogWarning(ex, "Failed to deserialize request.");
                }

                if (request is null || string.IsNullOrEmpty(request.Method))
                {
                    response = JsonRpcResponse.Failure(request?.Id ?? 0, JsonRpcErrorCode.ParseError, "Invalid JSON-RPC request.");
                }
                else
                {
                    response = await this._dispatcher.DispatchAsync(request, ct).ConfigureAwait(false);
                }

                string responseJson = JsonSerializer.Serialize(response);
                await PipeFraming.WriteMessageAsync(server, responseJson, ct).ConfigureAwait(false);

                server.Disconnect();
                server.Dispose();
            }
            catch (OperationCanceledException)
            {
                server?.Dispose();
                break;
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, "Unexpected error in command pipe accept loop.");
                server?.Dispose();
            }
        }
    }
}
