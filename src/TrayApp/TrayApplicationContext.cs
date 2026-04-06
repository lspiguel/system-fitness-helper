using SystemFitnessHelper.Ipc.Pipes;
using SystemFitnessHelper.Services;

namespace SystemFitnessHelper.TrayApp;

public sealed class TrayApplicationContext : ApplicationContext, IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ServiceConnection _serviceConnection;
    private readonly UiLauncher _uiLauncher;
    private readonly CancellationTokenSource _cts = new();
    private readonly ToolStripMenuItem _executeMenuItem;
    private readonly ToolStripMenuItem _openDashboardMenuItem;

    public TrayApplicationContext()
    {
        this._serviceConnection = new ServiceConnection();
        this._uiLauncher = new UiLauncher();

        this._executeMenuItem = new ToolStripMenuItem("Execute Now", null, this.OnExecuteNowClicked);
        this._openDashboardMenuItem = new ToolStripMenuItem("Open Dashboard", null, this.OnOpenDashboardClicked);

        ContextMenuStrip contextMenu = new();
        contextMenu.Items.Add(this._executeMenuItem);
        contextMenu.Items.Add(this._openDashboardMenuItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(new ToolStripMenuItem("Exit", null, this.OnExitClicked));
        contextMenu.Opening += this.OnContextMenuOpening;

        this._notifyIcon = new NotifyIcon
        {
            Text = "System Fitness Helper",
            Icon = SystemIcons.Application,
            ContextMenuStrip = contextMenu,
            Visible = true,
        };

        this._serviceConnection.ActionExecuted += this.OnActionExecuted;
        _ = Task.Run(() => this._serviceConnection.StartEventListeningAsync(this._cts.Token));
    }

    private void OnContextMenuOpening(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        bool running = this._serviceConnection.IsServiceRunning;
        this._executeMenuItem.Enabled = running;
        this._openDashboardMenuItem.Enabled = running;
    }

    private async void OnExecuteNowClicked(object? sender, EventArgs e)
    {
        try
        {
            ExecuteResult result = await this._serviceConnection.ExecuteAsync().ConfigureAwait(false);
            int succeeded = result.Results.Count(r => r.Success);
            int failed = result.Results.Count(r => !r.Success);
            this._notifyIcon.ShowBalloonTip(
                3000,
                "Execute Complete",
                $"Succeeded: {succeeded}, Failed: {failed}",
                failed > 0 ? ToolTipIcon.Warning : ToolTipIcon.Info);
        }
        catch (Exception ex)
        {
            this._notifyIcon.ShowBalloonTip(3000, "Execute Failed", ex.Message, ToolTipIcon.Error);
        }
    }

    private void OnOpenDashboardClicked(object? sender, EventArgs e) =>
        this._uiLauncher.LaunchOrActivate();

    private void OnExitClicked(object? sender, EventArgs e)
    {
        this._notifyIcon.Visible = false;
        Application.Exit();
    }

    private void OnActionExecuted(object? sender, ActionExecutedEventArgs e)
    {
        string status = e.Event.Success ? "Success" : "Failed";
        this._notifyIcon.ShowBalloonTip(
            2000,
            "Action Executed",
            $"{e.Event.ProcessName} → {e.Event.Action}: {status}",
            e.Event.Success ? ToolTipIcon.Info : ToolTipIcon.Warning);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            this._cts.Cancel();
            this._notifyIcon.Dispose();
            this._serviceConnection.Dispose();
            this._cts.Dispose();
        }

        base.Dispose(disposing);
    }
}
