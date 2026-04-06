using SystemFitnessHelper.Ipc.Messages;
using SystemFitnessHelper.Ipc.Pipes;
using SystemFitnessHelper.Services;

namespace SystemFitnessHelper.TrayApp;

public sealed class ServiceConnection : IDisposable
{
    private readonly CommandPipeClient _commandClient = new();
    private readonly EventPipeClient _eventClient = new();
    private readonly System.Windows.Forms.Timer _healthTimer;
    private bool _isServiceRunning;

    public event EventHandler<ActionExecutedEventArgs>? ActionExecuted
    {
        add => this._eventClient.ActionExecuted += value;
        remove => this._eventClient.ActionExecuted -= value;
    }

    public bool IsServiceRunning => this._isServiceRunning;

    public ServiceConnection()
    {
        this._healthTimer = new System.Windows.Forms.Timer { Interval = 10_000 };
        this._healthTimer.Tick += async (_, _) => await this.CheckHealthAsync().ConfigureAwait(false);
        this._healthTimer.Start();
    }

    public async Task<ActionsResult> GetActionsAsync(string? ruleSetName = null, CancellationToken ct = default) =>
        await this._commandClient.SendAsync<ActionsResult>(
            Methods.Actions,
            new ActionsParams { RuleSetName = ruleSetName },
            ct).ConfigureAwait(false);

    public async Task<ExecuteResult> ExecuteAsync(string? ruleSetName = null, CancellationToken ct = default) =>
        await this._commandClient.SendAsync<ExecuteResult>(
            Methods.Execute,
            new ExecuteParams { RuleSetName = ruleSetName },
            ct).ConfigureAwait(false);

    public Task StartEventListeningAsync(CancellationToken ct) =>
        this._eventClient.StartListeningAsync(ct);

    private async Task CheckHealthAsync()
    {
        try
        {
            using CancellationTokenSource cts = new(TimeSpan.FromSeconds(3));
            await this._commandClient.SendAsync<ActionsResult>(Methods.Actions, null, cts.Token).ConfigureAwait(false);
            this._isServiceRunning = true;
        }
        catch
        {
            this._isServiceRunning = false;
        }
    }

    public void Dispose()
    {
        this._healthTimer.Dispose();
    }
}
