using System.ComponentModel;
using SystemFitnessHelper.Services;

namespace SystemFitnessHelper.Ui.Forms;

public sealed class ActionsPanel : UserControl
{
    private readonly ServiceConnection _serviceConnection;
    private readonly Action<string?> _setStatus;
    private readonly DataGridView _grid;
    private readonly BindingList<ActionPlanRowViewModel> _rows = new();
    private Label? _errorLabel;

    public ActionsPanel(ServiceConnection serviceConnection, Action<string?> setStatus)
    {
        this._serviceConnection = serviceConnection;
        this._setStatus = setStatus;

        this._grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        };
        this._grid.DataSource = this._rows;
        this._grid.CellFormatting += this.OnCellFormatting;

        this.Controls.Add(this._grid);
    }

    public async Task RefreshAsync(string? ruleSetName)
    {
        this.RemoveErrorLabel();
        try
        {
            ActionsResult result = await this._serviceConnection.GetActionsAsync(ruleSetName).ConfigureAwait(true);
            this._setStatus(null);
            this._rows.Clear();

            foreach (ActionPlanView plan in result.Plans)
            {
                this._rows.Add(new ActionPlanRowViewModel
                {
                    Process = plan.ProcessName,
                    Service = plan.ServiceName ?? string.Empty,
                    Rule = plan.RuleId,
                    Action = plan.Action.ToString(),
                    Blocked = plan.Blocked ? "Yes" : "No",
                    Reason = plan.BlockReason ?? string.Empty,
                    IsBlocked = plan.Blocked,
                });
            }
        }
        catch (Exception ex)
        {
            this._setStatus(ex.Message);
            this.ShowError(ex.Message);
        }
    }

    public void ShowError(string message)
    {
        this._rows.Clear();
        this.RemoveErrorLabel();
        this._errorLabel = new Label
        {
            Text = $"Error: {message}",
            Dock = DockStyle.Fill,
            ForeColor = Color.Red,
            TextAlign = ContentAlignment.MiddleCenter,
        };
        this.Controls.Add(this._errorLabel);
    }

    private void RemoveErrorLabel()
    {
        if (this._errorLabel is not null)
        {
            this.Controls.Remove(this._errorLabel);
            this._errorLabel.Dispose();
            this._errorLabel = null;
        }
    }

    private void OnCellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || e.RowIndex >= this._rows.Count)
            return;

        if (this._rows[e.RowIndex].IsBlocked)
            this._grid.Rows[e.RowIndex].DefaultCellStyle.BackColor = Color.Orange;
    }
}

public sealed class ActionPlanRowViewModel
{
    public string Process { get; set; } = string.Empty;
    public string Service { get; set; } = string.Empty;
    public string Rule { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Blocked { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    [System.ComponentModel.Browsable(false)]
    public bool IsBlocked { get; set; }
}
