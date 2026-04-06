using System.IO.Pipes;
using System.Text.Json;
using SystemFitnessHelper.Ipc.Messages.Events;
using SystemFitnessHelper.Ipc.Protocol;

namespace SystemFitnessHelper.Ipc.Pipes;

public sealed class EventPipeClient
{
    public event EventHandler<ActionExecutedEventArgs>? ActionExecuted;

    public async Task StartListeningAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using NamedPipeClientStream pipe = new(
                    ".",
                    PipeConstants.SfhEvents,
                    PipeDirection.In,
                    PipeOptions.Asynchronous);

                await pipe.ConnectAsync(ct).ConfigureAwait(false);

                while (!ct.IsCancellationRequested)
                {
                    string json = await PipeFraming.ReadMessageAsync(pipe, ct).ConfigureAwait(false);
                    JsonRpcNotification? notification = JsonSerializer.Deserialize<JsonRpcNotification>(json);
                    if (notification is not null)
                        DispatchNotification(notification);
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (IOException)
            {
                // Server disconnected; reconnect after a short delay.
                await Task.Delay(TimeSpan.FromSeconds(2), ct).ConfigureAwait(false);
            }
        }
    }

    private void DispatchNotification(JsonRpcNotification notification)
    {
        if (notification.Method == Messages.Methods.ActionExecuted && notification.Params.HasValue)
        {
            ActionExecutedEvent? evt = JsonSerializer.Deserialize<ActionExecutedEvent>(
                notification.Params.Value.GetRawText());
            if (evt is not null)
                ActionExecuted?.Invoke(this, new ActionExecutedEventArgs(evt));
        }
    }
}

public sealed class ActionExecutedEventArgs : EventArgs
{
    public ActionExecutedEvent Event { get; }

    public ActionExecutedEventArgs(ActionExecutedEvent @event)
    {
        this.Event = @event;
    }
}
