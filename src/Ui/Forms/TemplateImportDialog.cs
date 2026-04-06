using SystemFitnessHelper.Configuration;

namespace SystemFitnessHelper.Ui.Forms;

public sealed class TemplateImportDialog : Form
{
    private readonly CheckedListBox _ruleList;
    private readonly ComboBox _targetCombo;
    private readonly RuleSet _template;

    public IReadOnlyList<Rule> SelectedRules { get; private set; } = [];
    public string? TargetRuleSetName { get; private set; }

    public TemplateImportDialog(RuleSet template, IReadOnlyCollection<string> ruleSetNames, string? defaultTarget)
    {
        this._template = template;
        this.Text = "Import from Template";
        this.Size = new Size(500, 500);
        this.FormBorderStyle = FormBorderStyle.Sizable;
        this.StartPosition = FormStartPosition.CenterParent;

        Label targetLabel = new() { Text = "Target ruleset:", Top = 10, Left = 10, AutoSize = true };
        this._targetCombo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Top = 10, Left = 120, Width = 200,
        };
        foreach (string name in ruleSetNames)
            this._targetCombo.Items.Add(name);
        if (defaultTarget is not null && this._targetCombo.Items.Contains(defaultTarget))
            this._targetCombo.SelectedItem = defaultTarget;
        else if (this._targetCombo.Items.Count > 0)
            this._targetCombo.SelectedIndex = 0;

        Label rulesLabel = new() { Text = "Select rules to import:", Top = 45, Left = 10, AutoSize = true };
        this._ruleList = new CheckedListBox
        {
            Top = 65, Left = 10,
            Width = this.ClientSize.Width - 20,
            Height = this.ClientSize.Height - 130,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
        };

        foreach (Rule rule in template.Rules)
            this._ruleList.Items.Add($"{rule.Id} — {rule.Description ?? "(no description)"}");

        FlowLayoutPanel buttons = new() { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Height = 40 };
        Button cancelBtn = new() { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 75 };
        Button okBtn = new() { Text = "Import", Width = 75 };
        okBtn.Click += this.OnOkClicked;
        buttons.Controls.Add(cancelBtn);
        buttons.Controls.Add(okBtn);

        this.Controls.Add(targetLabel);
        this.Controls.Add(this._targetCombo);
        this.Controls.Add(rulesLabel);
        this.Controls.Add(this._ruleList);
        this.Controls.Add(buttons);
        this.AcceptButton = okBtn;
        this.CancelButton = cancelBtn;
    }

    private void OnOkClicked(object? sender, EventArgs e)
    {
        this.TargetRuleSetName = this._targetCombo.SelectedItem?.ToString();
        List<Rule> selected = [];
        foreach (int idx in this._ruleList.CheckedIndices)
            selected.Add(this._template.Rules[idx]);
        this.SelectedRules = selected;
        this.DialogResult = DialogResult.OK;
        this.Close();
    }
}
