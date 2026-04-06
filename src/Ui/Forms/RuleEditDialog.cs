using SystemFitnessHelper.Configuration;

namespace SystemFitnessHelper.Ui.Forms;

public sealed class RuleEditDialog : Form
{
    private readonly TextBox _idBox;
    private readonly TextBox _descriptionBox;
    private readonly CheckBox _enabledCheck;
    private readonly ComboBox _actionCombo;
    public Rule? ResultRule { get; private set; }

    public RuleEditDialog(Rule? existing)
    {
        this.Text = existing is null ? "Add Rule" : "Edit Rule";
        this.Size = new Size(400, 250);
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.StartPosition = FormStartPosition.CenterParent;
        this.MaximizeBox = false;
        this.MinimizeBox = false;

        TableLayoutPanel layout = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 5,
            Padding = new Padding(10),
        };

        layout.Controls.Add(new Label { Text = "ID:", Anchor = AnchorStyles.Right }, 0, 0);
        this._idBox = new TextBox { Text = existing?.Id ?? string.Empty, Dock = DockStyle.Fill };
        layout.Controls.Add(this._idBox, 1, 0);

        layout.Controls.Add(new Label { Text = "Description:", Anchor = AnchorStyles.Right }, 0, 1);
        this._descriptionBox = new TextBox { Text = existing?.Description ?? string.Empty, Dock = DockStyle.Fill };
        layout.Controls.Add(this._descriptionBox, 1, 1);

        layout.Controls.Add(new Label { Text = "Enabled:", Anchor = AnchorStyles.Right }, 0, 2);
        this._enabledCheck = new CheckBox { Checked = existing?.Enabled ?? true };
        layout.Controls.Add(this._enabledCheck, 1, 2);

        layout.Controls.Add(new Label { Text = "Action:", Anchor = AnchorStyles.Right }, 0, 3);
        this._actionCombo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Dock = DockStyle.Fill,
        };
        foreach (ActionType a in Enum.GetValues<ActionType>())
            this._actionCombo.Items.Add(a.ToString());
        this._actionCombo.SelectedItem = (existing?.Action ?? ActionType.None).ToString();
        layout.Controls.Add(this._actionCombo, 1, 3);

        FlowLayoutPanel buttons = new() { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Height = 40 };
        Button cancelBtn = new() { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 75 };
        Button okBtn = new() { Text = "OK", Width = 75 };
        okBtn.Click += this.OnOkClicked;
        buttons.Controls.Add(cancelBtn);
        buttons.Controls.Add(okBtn);

        this.Controls.Add(layout);
        this.Controls.Add(buttons);
        this.AcceptButton = okBtn;
        this.CancelButton = cancelBtn;
    }

    private void OnOkClicked(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(this._idBox.Text))
        {
            MessageBox.Show("ID is required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        Enum.TryParse<ActionType>(this._actionCombo.SelectedItem?.ToString(), out ActionType action);

        this.ResultRule = new Rule
        {
            Id = this._idBox.Text.Trim(),
            Description = string.IsNullOrWhiteSpace(this._descriptionBox.Text) ? null : this._descriptionBox.Text.Trim(),
            Enabled = this._enabledCheck.Checked,
            Action = action,
        };

        this.DialogResult = DialogResult.OK;
        this.Close();
    }
}
