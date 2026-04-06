using System.ComponentModel;
using SystemFitnessHelper.Configuration;
using SystemFitnessHelper.Services;

namespace SystemFitnessHelper.Ui.Forms;

public sealed class ProcessListPanel : UserControl
{
    private readonly ServiceConnection _serviceConnection;
    private readonly DataGridView _grid;
    private readonly BindingList<ProcessRowViewModel> _rows = new();
    private Label? _errorLabel;

    public ProcessListPanel(ServiceConnection serviceConnection)
    {
        this._serviceConnection = serviceConnection;

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
            ProcessListResult result = await this._serviceConnection.GetProcessListAsync(ruleSetName).ConfigureAwait(true);
            this._rows.Clear();

            Dictionary<int, string> matchedRules = result.Matches
                .GroupBy(m => m.Fingerprint.ProcessId)
                .ToDictionary(g => g.Key, g => g.First().Rule.Id);
            Dictionary<int, ActionType> matchedActions = result.Matches
                .GroupBy(m => m.Fingerprint.ProcessId)
                .ToDictionary(g => g.Key, g => g.First().Rule.Action);

            foreach (var fp in result.Fingerprints)
            {
                matchedRules.TryGetValue(fp.ProcessId, out string? matchedRule);
                matchedActions.TryGetValue(fp.ProcessId, out ActionType action);
                this._rows.Add(new ProcessRowViewModel
                {
                    Pid = fp.ProcessId,
                    ProcessName = fp.ProcessName,
                    ServiceName = fp.ServiceName ?? string.Empty,
                    Status = fp.ServiceStatus?.ToString() ?? "N/A",
                    MemoryMb = fp.WorkingSetBytes / (1024 * 1024),
                    MatchedRule = matchedRule ?? string.Empty,
                    MatchedAction = action,
                });
            }
        }
        catch (Exception ex)
        {
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

        ProcessRowViewModel row = this._rows[e.RowIndex];
        Color bgColor = row.MatchedAction switch
        {
            ActionType.Kill or ActionType.Stop when !string.IsNullOrEmpty(row.MatchedRule) => Color.LightCoral,
            ActionType.Suspend when !string.IsNullOrEmpty(row.MatchedRule) => Color.LightYellow,
            _ => SystemColors.Window,
        };

        this._grid.Rows[e.RowIndex].DefaultCellStyle.BackColor = bgColor;
    }
}

public sealed class ProcessRowViewModel
{
    public int Pid { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public long MemoryMb { get; set; }
    public string MatchedRule { get; set; } = string.Empty;
    [System.ComponentModel.Browsable(false)]
    public ActionType MatchedAction { get; set; }
}
