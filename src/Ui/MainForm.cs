using SystemFitnessHelper.Services;
using SystemFitnessHelper.Ui.Forms;

namespace SystemFitnessHelper.Ui;

public sealed class MainForm : Form
{
    private readonly ServiceConnection _serviceConnection;
    private readonly ProcessListPanel _processListPanel;
    private readonly ActionsPanel _actionsPanel;
    private readonly ConfigEditorPanel _configEditorPanel;
    private readonly ToolStripButton _refreshButton;
    private readonly ToolStripButton _executeButton;
    private readonly ToolStripComboBox _ruleSetCombo;
    private readonly ToolStripLabel _statusLabel;
    private readonly ToolStripStatusLabel _timestampLabel;
    private readonly ToolStripStatusLabel _activeRuleSetLabel;

    public MainForm(ServiceConnection serviceConnection)
    {
        this._serviceConnection = serviceConnection;
        this.Text = "System Fitness Helper";
        this.Size = new Size(1000, 700);
        this.MinimumSize = new Size(800, 500);

        // ToolStrip
        ToolStrip toolStrip = new();
        this._refreshButton = new ToolStripButton("Refresh") { DisplayStyle = ToolStripItemDisplayStyle.Text };
        this._executeButton = new ToolStripButton("Execute") { DisplayStyle = ToolStripItemDisplayStyle.Text };
        this._ruleSetCombo = new ToolStripComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150 };
        this._statusLabel = new ToolStripLabel("Connecting...");
        this._refreshButton.Click += async (_, _) => await this.RefreshAsync();
        this._executeButton.Click += async (_, _) => await this.ExecuteAsync();
        this._ruleSetCombo.SelectedIndexChanged += async (_, _) => await this.OnRuleSetChangedAsync();
        toolStrip.Items.Add(this._refreshButton);
        toolStrip.Items.Add(this._executeButton);
        toolStrip.Items.Add(new ToolStripSeparator());
        toolStrip.Items.Add(new ToolStripLabel("Ruleset:"));
        toolStrip.Items.Add(this._ruleSetCombo);
        toolStrip.Items.Add(new ToolStripSeparator());
        toolStrip.Items.Add(this._statusLabel);

        // Tab control
        this._processListPanel = new ProcessListPanel(serviceConnection);
        this._actionsPanel = new ActionsPanel(serviceConnection);
        this._configEditorPanel = new ConfigEditorPanel(serviceConnection);

        TabPage processesTab = new("Processes");
        processesTab.Controls.Add(this._processListPanel);
        this._processListPanel.Dock = DockStyle.Fill;

        TabPage actionsTab = new("Actions");
        actionsTab.Controls.Add(this._actionsPanel);
        this._actionsPanel.Dock = DockStyle.Fill;

        TabPage configTab = new("Configuration");
        configTab.Controls.Add(this._configEditorPanel);
        this._configEditorPanel.Dock = DockStyle.Fill;

        TabControl tabs = new();
        tabs.TabPages.Add(processesTab);
        tabs.TabPages.Add(actionsTab);
        tabs.TabPages.Add(configTab);
        tabs.Dock = DockStyle.Fill;

        // Status strip
        StatusStrip statusStrip = new();
        this._timestampLabel = new ToolStripStatusLabel("Last refresh: never");
        this._activeRuleSetLabel = new ToolStripStatusLabel(string.Empty) { Spring = true, TextAlign = ContentAlignment.MiddleRight };
        statusStrip.Items.Add(this._timestampLabel);
        statusStrip.Items.Add(this._activeRuleSetLabel);

        this.Controls.Add(tabs);
        this.Controls.Add(toolStrip);
        this.Controls.Add(statusStrip);

        this.Load += async (_, _) => await this.RefreshAsync();
    }

    private string? ActiveRuleSet =>
        this._ruleSetCombo.SelectedItem as string;

    private async Task RefreshAsync()
    {
        this.UseWaitCursor = true;
        try
        {
            // Load config first to populate ruleset selector
            ConfigResult config = await this._serviceConnection.GetConfigAsync().ConfigureAwait(true);
            PopulateRuleSetCombo(config);
            this._statusLabel.Text = "Connected";

            string? ruleSet = this.ActiveRuleSet;
            await this._processListPanel.RefreshAsync(ruleSet).ConfigureAwait(true);
            await this._actionsPanel.RefreshAsync(ruleSet).ConfigureAwait(true);
            await this._configEditorPanel.RefreshAsync().ConfigureAwait(true);

            this._timestampLabel.Text = $"Last refresh: {DateTime.Now:HH:mm:ss}";
            this._activeRuleSetLabel.Text = ruleSet is not null ? $"Ruleset: {ruleSet}" : string.Empty;
        }
        catch (Exception ex)
        {
            this._statusLabel.Text = "Service not running";
            this._processListPanel.ShowError(ex.Message);
            this._actionsPanel.ShowError(ex.Message);
        }
        finally
        {
            this.UseWaitCursor = false;
        }
    }

    private async Task ExecuteAsync()
    {
        this.UseWaitCursor = true;
        try
        {
            ExecuteResult result = await this._serviceConnection.ExecuteAsync(this.ActiveRuleSet).ConfigureAwait(true);
            int succeeded = result.Results.Count(r => r.Success);
            int failed = result.Results.Count(r => !r.Success);
            MessageBox.Show(
                $"Execute complete. Succeeded: {succeeded}, Failed: {failed}",
                "Execute",
                MessageBoxButtons.OK,
                failed > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
            await this.RefreshAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Execute failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            this.UseWaitCursor = false;
        }
    }

    private async Task OnRuleSetChangedAsync()
    {
        string? ruleSet = this.ActiveRuleSet;
        await this._processListPanel.RefreshAsync(ruleSet).ConfigureAwait(true);
        await this._actionsPanel.RefreshAsync(ruleSet).ConfigureAwait(true);
        this._activeRuleSetLabel.Text = ruleSet is not null ? $"Ruleset: {ruleSet}" : string.Empty;
    }

    private void PopulateRuleSetCombo(ConfigResult config)
    {
        string? current = this.ActiveRuleSet;
        this._ruleSetCombo.Items.Clear();
        foreach (string name in config.AvailableRuleSetNames)
            this._ruleSetCombo.Items.Add(name);

        if (current is not null && this._ruleSetCombo.Items.Contains(current))
            this._ruleSetCombo.SelectedItem = current;
        else if (this._ruleSetCombo.Items.Count > 0)
            this._ruleSetCombo.SelectedIndex = 0;
    }
}
