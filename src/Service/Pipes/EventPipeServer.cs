using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SystemFitnessHelper.Ipc.Pipes;
using SystemFitnessHelper.Ipc.Protocol;

namespace SystemFitnessHelper.Service.Pipes;

public sealed class EventPipeServer
{
    private readonly ILogger<EventPipeServer> _logger;
    private readonly ConcurrentDictionary<int, NamedPipeServerStream> _clients = new();
    private int _connectionIndex;
    private CancellationTokenSource? _cts;
    private Task? _acceptLoop;

    public EventPipeServer(ILogger<EventPipeServer> logger)
    {
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

        foreach (NamedPipeServerStream client in this._clients.Values)
        {
            try { client.Dispose(); } catch { }
        }

        this._clients.Clear();

        if (this._acceptLoop is not null)
        {
            try { await this._acceptLoop.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }
    }

    public void Broadcast(JsonRpcNotification notification)
    {
        string json = JsonSerializer.Serialize(notification);
        foreach ((int key, NamedPipeServerStream client) in this._clients)
        {
            try
            {
                PipeFraming.WriteMessageAsync(client, json).GetAwaiter().GetResult();
            }
            catch
            {
                this._clients.TryRemove(key, out _);
                try { client.Dispose(); } catch { }
            }
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
                    PipeConstants.SfhEvents,
                    PipeDirection.Out,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await server.WaitForConnectionAsync(ct).ConfigureAwait(false);

                int index = Interlocked.Increment(ref this._connectionIndex);
                this._clients[index] = server;

                // Monitor for disconnect on a background task
                _ = Task.Run(async () =>
                {
                    byte[] buf = new byte[1];
                    try
                    {
                        // Clients should not send data; any read completing means disconnect
                        await server.ReadAsync(buf, ct).ConfigureAwait(false);
                    }
                    catch { }
                    finally
                    {
                        this._clients.TryRemove(index, out _);
                        try { server.Dispose(); } catch { }
                    }
                }, CancellationToken.None);
            }
            catch (OperationCanceledException)
            {
                server?.Dispose();
                break;
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, "Unexpected error in event pipe accept loop.");
                server?.Dispose();
            }
        }
    }
}
