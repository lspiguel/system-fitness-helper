using Microsoft.Extensions.Hosting;
using SystemFitnessHelper.Service.Pipes;

namespace SystemFitnessHelper.Service;

public sealed class ServiceWorker : IHostedService
{
    private readonly CommandPipeServer _commandPipeServer;
    private readonly EventPipeServer _eventPipeServer;

    public ServiceWorker(CommandPipeServer commandPipeServer, EventPipeServer eventPipeServer)
    {
        this._commandPipeServer = commandPipeServer;
        this._eventPipeServer = eventPipeServer;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await this._eventPipeServer.StartAsync(cancellationToken).ConfigureAwait(false);
        await this._commandPipeServer.StartAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await this._commandPipeServer.StopAsync(cancellationToken).ConfigureAwait(false);
        await this._eventPipeServer.StopAsync(cancellationToken).ConfigureAwait(false);
    }
}
