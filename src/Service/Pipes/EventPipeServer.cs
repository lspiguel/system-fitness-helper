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
    private readonly ConcurrentDictionary<int, Client> _clients = new();
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

        foreach (Client client in this._clients.Values)
            client.Dispose();

        this._clients.Clear();

        if (this._acceptLoop is not null)
        {
            try { await this._acceptLoop.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }
    }

    /// <summary>
    /// Sends a notification to every connected client. Each client has its own write lock, so a
    /// slow reader delays only itself and never the handler that raised the event.
    /// </summary>
    public async Task BroadcastAsync(JsonRpcNotification notification, CancellationToken ct = default)
    {
        string json = JsonSerializer.Serialize(notification);

        IEnumerable<Task> sends = this._clients.ToArray().Select(async pair =>
        {
            (int key, Client client) = pair;

            if (!client.Stream.IsConnected)
            {
                this.Remove(key);
                return;
            }

            await client.WriteLock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                await PipeFraming.WriteMessageAsync(client.Stream, json, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                this._logger.LogDebug(ex, "Dropping event pipe client {Key} after a failed write.", key);
                this.Remove(key);
            }
            finally
            {
                client.WriteLock.Release();
            }
        });

        await Task.WhenAll(sends).ConfigureAwait(false);
    }

    private void Remove(int key)
    {
        if (this._clients.TryRemove(key, out Client? client))
            client.Dispose();
    }

    private async Task RunAcceptLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            NamedPipeServerStream? server = null;
            try
            {
                server = NamedPipeServerStreamAcl.Create(
                    PipeConstants.SfhEvents,
                    PipeDirection.Out,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous,
                    inBufferSize: 0,
                    outBufferSize: 0,
                    PipeSecurityFactory.CreateDefault());

                await server.WaitForConnectionAsync(ct).ConfigureAwait(false);

                int index = Interlocked.Increment(ref this._connectionIndex);
                this._clients[index] = new Client(server);
                server = null; // ownership transfers to the dictionary

                this._logger.LogDebug("Event pipe client {Index} connected.", index);
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

    /// <summary>
    /// A connected event subscriber.
    /// </summary>
    /// <remarks>
    /// There is deliberately no disconnect-monitor task. A previous version started one that read
    /// from this <see cref="PipeDirection.Out"/> stream; reading a write-only stream throws
    /// <see cref="NotSupportedException"/> immediately, so every client was torn down microseconds
    /// after connecting and no event was ever delivered. Disconnects are detected instead by
    /// <see cref="PipeStream.IsConnected"/> and by writes failing in
    /// <see cref="BroadcastAsync"/>.
    /// </remarks>
    private sealed class Client : IDisposable
    {
        public Client(NamedPipeServerStream stream) => this.Stream = stream;

        public NamedPipeServerStream Stream { get; }

        public SemaphoreSlim WriteLock { get; } = new(1, 1);

        public void Dispose()
        {
            try { this.Stream.Dispose(); } catch { }
            this.WriteLock.Dispose();
        }
    }
}
