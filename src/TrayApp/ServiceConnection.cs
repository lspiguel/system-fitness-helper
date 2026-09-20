using SystemFitnessHelper.Ipc.Messages;
using SystemFitnessHelper.Ipc.Pipes;
using SystemFitnessHelper.Services;

namespace SystemFitnessHelper.TrayApp;

public sealed class ServiceConnection : IDisposable
{
    /// <summary>Stopping a stubborn Windows service can take 30 s on its own.</summary>
    private static readonly TimeSpan ExecuteTimeout = TimeSpan.FromMinutes(5);

    private static readonly TimeSpan HealthTimeout = TimeSpan.FromSeconds(3);

    private readonly CommandPipeClient _commandClient = new();
    private readonly EventPipeClient _eventClient = new();
    private readonly System.Windows.Forms.Timer _healthTimer;
    private bool _isServiceRunning;
    private string? _configPath;

    public event EventHandler<ActionExecutedEventArgs>? ActionExecuted
    {
        add => this._eventClient.ActionExecuted += value;
        remove => this._eventClient.ActionExecuted -= value;
    }

    public bool IsServiceRunning => this._isServiceRunning;

    /// <summary>Rules file the service reports it is using; null until the first successful ping.</summary>
    public string? ConfigPath => this._configPath;

    public ServiceConnection()
    {
        this._healthTimer = new System.Windows.Forms.Timer { Interval = 10_000 };
        this._healthTimer.Tick += async (_, _) => await this.CheckHealthAsync().ConfigureAwait(false);
        this._healthTimer.Start();

        // Don't make the user wait a full interval to find out whether the service is up.
        _ = this.CheckHealthAsync();
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
            ct,
            ExecuteTimeout).ConfigureAwait(false);

    public Task StartEventListeningAsync(CancellationToken ct) =>
        this._eventClient.StartListeningAsync(ct);

    /// <summary>
    /// Probes the service with <c>sfh.ping</c>. This used to call <c>sfh.actions</c>, which scans
    /// every process on the machine and runs the full rule match — every ten seconds, purely to
    /// set a boolean.
    /// </summary>
    private async Task CheckHealthAsync()
    {
        try
        {
            PingResult ping = await this._commandClient
                .SendAsync<PingResult>(Methods.Ping, null, default, HealthTimeout)
                .ConfigureAwait(false);

            this._isServiceRunning = ping.Ok;
            this._configPath = ping.ConfigPath;
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
