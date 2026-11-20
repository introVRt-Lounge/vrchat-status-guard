using VrchatPrivacy.Core.Protection;
using VrchatPrivacy.Core.Settings;
using VrchatPrivacy.Core.Worlds;

namespace VrchatPrivacy.App;

public sealed class SettingsForm : Form
{
    private readonly TextBox _worlds = new()
    {
        Multiline = true,
        ScrollBars = ScrollBars.Vertical,
        Dock = DockStyle.Fill,
        AcceptsReturn = true,
    };
    private readonly ComboBox _status = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly AppSettings _settings;

    public SettingsForm(AppSettings settings)
    {
        _settings = settings;
        Text = "Protected worlds";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MinimizeBox = false;
        MaximizeBox = false;
        ClientSize = new Size(480, 320);

        _status.Items.AddRange(new object[] { "Orange (ask me)", "Red (busy / DND)" });
        _status.SelectedIndex = settings.ProtectionStatus == ProtectionStatus.Red ? 1 : 0;
        _worlds.Text = string.Join(Environment.NewLine, settings.ProtectedWorldIds);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(12),
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        layout.Controls.Add(new Label
        {
            Text = "Protected world IDs or VRChat world URLs (one per line).\nDefault includes Cuddle & Sleep.",
            AutoSize = true,
            Dock = DockStyle.Fill,
        }, 0, 0);
        layout.Controls.Add(_worlds, 0, 1);
        layout.Controls.Add(new Label { Text = "Protection status:", AutoSize = true }, 0, 2);
        layout.Controls.Add(_status, 0, 3);

        var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill };
        var ok = new Button { Text = "Save", DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel };
        ok.Click += (_, _) =>
        {
            if (!TryApply())
            {
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        };
        buttons.Controls.Add(ok);
        buttons.Controls.Add(cancel);
        layout.Controls.Add(buttons, 0, 4);
        Controls.Add(layout);
        AcceptButton = ok;
        CancelButton = cancel;
    }

    public AppSettings Result => _settings;

    private bool TryApply()
    {
        var ids = new List<string>();
        foreach (var line in _worlds.Lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                continue;
            }

            if (!WorldId.TryParseFromUserInput(trimmed, out var world))
            {
                MessageBox.Show(this, $"Not a valid world id/URL:\n{trimmed}", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            ids.Add(world.Value);
        }

        if (ids.Count == 0)
        {
            ids.Add(AppSettings.DefaultProtectedWorldId);
        }

        _settings.ProtectedWorldIds = ids.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        _settings.ProtectionStatus = _status.SelectedIndex == 1 ? ProtectionStatus.Red : ProtectionStatus.Orange;
        _settings.EnsureDefaultWorldPresent();
        return true;
    }
}
