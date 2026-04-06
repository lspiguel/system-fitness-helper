using SystemFitnessHelper.Configuration;
using SystemFitnessHelper.Ipc.Messages;

namespace SystemFitnessHelper.Ui.Forms;

public sealed class ConfigEditorPanel : UserControl
{
    private readonly ServiceConnection _serviceConnection;
    private RuleSetsConfig? _config;

    private readonly ComboBox _ruleSetCombo;
    private readonly DataGridView _ruleGrid;
    private readonly Button _saveButton;
    private readonly Button _addRuleButton;
    private readonly Button _editRuleButton;
    private readonly Button _deleteRuleButton;
    private readonly Button _addFromTemplateButton;
    private readonly Button _newRuleSetButton;
    private readonly Button _deleteRuleSetButton;
    private readonly Button _setDefaultButton;

    public ConfigEditorPanel(ServiceConnection serviceConnection)
    {
        this._serviceConnection = serviceConnection;

        // Top bar - ruleset selector
        Label editingLabel = new() { Text = "Editing ruleset:", AutoSize = true, Top = 8, Left = 5 };
        this._ruleSetCombo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Top = 5, Left = 110, Width = 200,
        };
        this._ruleSetCombo.SelectedIndexChanged += this.OnRuleSetComboChanged;

        this._newRuleSetButton = new Button { Text = "New Ruleset", Top = 5, Left = 320, Width = 100, Height = 26 };
        this._deleteRuleSetButton = new Button { Text = "Delete Ruleset", Top = 5, Left = 425, Width = 110, Height = 26 };
        this._setDefaultButton = new Button { Text = "Set as Default", Top = 5, Left = 540, Width = 110, Height = 26 };

        this._newRuleSetButton.Click += this.OnNewRuleSetClicked;
        this._deleteRuleSetButton.Click += this.OnDeleteRuleSetClicked;
        this._setDefaultButton.Click += this.OnSetDefaultClicked;

        Panel topBar = new()
        {
            Dock = DockStyle.Top,
            Height = 40,
        };
        topBar.Controls.AddRange([editingLabel, this._ruleSetCombo, this._newRuleSetButton, this._deleteRuleSetButton, this._setDefaultButton]);

        // Rule grid
        this._ruleGrid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            AllowUserToAddRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        };
        SetupRuleGridColumns();

        // Bottom toolbar
        this._saveButton = new Button { Text = "Save", Width = 80, Height = 26 };
        this._addRuleButton = new Button { Text = "Add Rule", Width = 80, Height = 26 };
        this._editRuleButton = new Button { Text = "Edit Rule", Width = 80, Height = 26 };
        this._deleteRuleButton = new Button { Text = "Delete Rule", Width = 90, Height = 26 };
        this._addFromTemplateButton = new Button { Text = "Add from Template", Width = 130, Height = 26 };

        this._saveButton.Click += async (_, _) => await this.OnSaveClickedAsync();
        this._addRuleButton.Click += this.OnAddRuleClicked;
        this._editRuleButton.Click += this.OnEditRuleClicked;
        this._deleteRuleButton.Click += this.OnDeleteRuleClicked;
        this._addFromTemplateButton.Click += async (_, _) => await this.OnAddFromTemplateClickedAsync();

        FlowLayoutPanel bottomBar = new()
        {
            Dock = DockStyle.Bottom,
            Height = 36,
            FlowDirection = FlowDirection.LeftToRight,
        };
        bottomBar.Controls.AddRange([this._saveButton, this._addRuleButton, this._editRuleButton, this._deleteRuleButton, this._addFromTemplateButton]);

        this.Controls.Add(this._ruleGrid);
        this.Controls.Add(topBar);
        this.Controls.Add(bottomBar);
    }

    public async Task RefreshAsync()
    {
        try
        {
            this._config = (await this._serviceConnection.GetConfigAsync().ConfigureAwait(true)).Config;
            if (this._config is null)
                return;

            this.PopulateRuleSetCombo();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to load config: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void PopulateRuleSetCombo()
    {
        if (this._config is null)
            return;

        string? current = this._ruleSetCombo.SelectedItem as string;
        this._ruleSetCombo.Items.Clear();

        foreach ((string name, RuleSet rs) in this._config.RuleSets)
        {
            string display = rs.IsDefault ? $"{name} [DEFAULT]" : name;
            this._ruleSetCombo.Items.Add(display);
        }

        if (current is not null)
        {
            int idx = FindComboIndex(current);
            if (idx >= 0) this._ruleSetCombo.SelectedIndex = idx;
            else if (this._ruleSetCombo.Items.Count > 0) this._ruleSetCombo.SelectedIndex = 0;
        }
        else if (this._ruleSetCombo.Items.Count > 0)
        {
            this._ruleSetCombo.SelectedIndex = 0;
        }
    }

    private int FindComboIndex(string name)
    {
        for (int i = 0; i < this._ruleSetCombo.Items.Count; i++)
        {
            string item = this._ruleSetCombo.Items[i]?.ToString() ?? string.Empty;
            if (item.StartsWith(name, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return -1;
    }

    private string? SelectedRuleSetName()
    {
        string? display = this._ruleSetCombo.SelectedItem?.ToString();
        if (display is null)
            return null;
        int suffixIdx = display.IndexOf(" [DEFAULT]", StringComparison.Ordinal);
        return suffixIdx >= 0 ? display[..suffixIdx] : display;
    }

    private RuleSet? SelectedRuleSet()
    {
        string? name = this.SelectedRuleSetName();
        if (name is null || this._config is null)
            return null;
        this._config.RuleSets.TryGetValue(name, out RuleSet? rs);
        return rs;
    }

    private void OnRuleSetComboChanged(object? sender, EventArgs e)
    {
        this.BindRuleGrid();
    }

    private void BindRuleGrid()
    {
        this._ruleGrid.Rows.Clear();
        RuleSet? rs = this.SelectedRuleSet();
        if (rs is null)
            return;

        foreach (Rule rule in rs.Rules)
        {
            string conditions = string.Join(", ", rule.Conditions.Select(c => $"{c.Field} {c.Op} {c.Value}"));
            this._ruleGrid.Rows.Add(rule.Id, rule.Enabled, rule.Action.ToString(), conditions, rule.Description);
        }
    }

    private void SetupRuleGridColumns()
    {
        this._ruleGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", HeaderText = "ID", ReadOnly = true });
        this._ruleGrid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Enabled", HeaderText = "Enabled" });
        this._ruleGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Action", HeaderText = "Action" });
        this._ruleGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Conditions", HeaderText = "Conditions", ReadOnly = true });
        this._ruleGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Description", HeaderText = "Description" });
    }

    private void OnNewRuleSetClicked(object? sender, EventArgs e)
    {
        if (this._config is null)
            return;

        string name = Microsoft.VisualBasic.Interaction.InputBox("Enter a unique name for the new ruleset:", "New Ruleset");
        if (string.IsNullOrWhiteSpace(name))
            return;

        if (this._config.RuleSets.ContainsKey(name))
        {
            MessageBox.Show("A ruleset with that name already exists.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        this._config.RuleSets[name] = new RuleSet { IsDefault = false };
        this.PopulateRuleSetCombo();
        int idx = this.FindComboIndex(name);
        if (idx >= 0)
            this._ruleSetCombo.SelectedIndex = idx;
    }

    private void OnDeleteRuleSetClicked(object? sender, EventArgs e)
    {
        string? name = this.SelectedRuleSetName();
        if (name is null || this._config is null)
            return;

        RuleSet? rs = this.SelectedRuleSet();
        if (rs is not null && rs.IsDefault && this._config.RuleSets.Count == 1)
        {
            MessageBox.Show("Cannot delete the only default ruleset.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        if (MessageBox.Show($"Delete ruleset '{name}'?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        this._config.RuleSets.Remove(name);
        this.PopulateRuleSetCombo();
    }

    private void OnSetDefaultClicked(object? sender, EventArgs e)
    {
        string? name = this.SelectedRuleSetName();
        if (name is null || this._config is null)
            return;

        foreach (string key in this._config.RuleSets.Keys.ToList())
        {
            RuleSet rs = this._config.RuleSets[key];
            this._config.RuleSets[key] = new RuleSet
            {
                IsDefault = string.Equals(key, name, StringComparison.OrdinalIgnoreCase),
                Rules = rs.Rules,
                Protected = rs.Protected,
            };
        }

        this.PopulateRuleSetCombo();
    }

    private async Task OnSaveClickedAsync()
    {
        if (this._config is null)
            return;

        try
        {
            ConfigSaveResult result = await this._serviceConnection.SaveConfigAsync(this._config).ConfigureAwait(true);
            if (result.Success)
                MessageBox.Show("Configuration saved successfully.", "Save", MessageBoxButtons.OK, MessageBoxIcon.Information);
            else
                MessageBox.Show($"Save failed: {result.ErrorMessage}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Save failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OnAddRuleClicked(object? sender, EventArgs e)
    {
        RuleSet? rs = this.SelectedRuleSet();
        if (rs is null)
            return;

        using RuleEditDialog dialog = new(null);
        if (dialog.ShowDialog(this) == DialogResult.OK && dialog.ResultRule is not null)
        {
            rs.Rules.Add(dialog.ResultRule);
            this.BindRuleGrid();
        }
    }

    private void OnEditRuleClicked(object? sender, EventArgs e)
    {
        RuleSet? rs = this.SelectedRuleSet();
        if (rs is null || this._ruleGrid.CurrentRow is null)
            return;

        int rowIdx = this._ruleGrid.CurrentRow.Index;
        if (rowIdx < 0 || rowIdx >= rs.Rules.Count)
            return;

        Rule existing = rs.Rules[rowIdx];
        using RuleEditDialog dialog = new(existing);
        if (dialog.ShowDialog(this) == DialogResult.OK && dialog.ResultRule is not null)
        {
            rs.Rules[rowIdx] = dialog.ResultRule;
            this.BindRuleGrid();
        }
    }

    private void OnDeleteRuleClicked(object? sender, EventArgs e)
    {
        RuleSet? rs = this.SelectedRuleSet();
        if (rs is null || this._ruleGrid.CurrentRow is null)
            return;

        int rowIdx = this._ruleGrid.CurrentRow.Index;
        if (rowIdx < 0 || rowIdx >= rs.Rules.Count)
            return;

        if (MessageBox.Show("Delete selected rule?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        rs.Rules.RemoveAt(rowIdx);
        this.BindRuleGrid();
    }

    private async Task OnAddFromTemplateClickedAsync()
    {
        try
        {
            RuleSet template = await this._serviceConnection.GetTemplateAsync().ConfigureAwait(true);
            using TemplateImportDialog dialog = new(template, this._config?.RuleSets.Keys.ToList() ?? [], this.SelectedRuleSetName());
            if (dialog.ShowDialog(this) == DialogResult.OK && this._config is not null)
            {
                string? targetName = dialog.TargetRuleSetName;
                if (targetName is not null && this._config.RuleSets.TryGetValue(targetName, out RuleSet? targetRs))
                {
                    targetRs.Rules.AddRange(dialog.SelectedRules);
                    this.BindRuleGrid();
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to load template: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
