namespace StatusGuard.App;

public sealed class LoginForm : Form
{
    private readonly TextBox _username = new() { Dock = DockStyle.Fill };
    private readonly TextBox _password = new() { Dock = DockStyle.Fill, UseSystemPasswordChar = true };
    private readonly TextBox _otp = new() { Dock = DockStyle.Fill };
    private readonly Label _otpLabel = new() { Text = "2FA code (if prompted):", AutoSize = true, Dock = DockStyle.Fill };
    private readonly Button _ok = new() { Text = "Login", Width = 110, Height = 36 };
    private readonly Button _cancel = new() { Text = "Cancel", Width = 110, Height = 36, DialogResult = DialogResult.Cancel };

    public LoginForm(string? usernameHint = null)
    {
        Text = "VRChat login";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = true;
        AutoScaleMode = AutoScaleMode.Dpi;
        MinimumSize = new Size(440, 280);
        ClientSize = new Size(440, 280);
        _username.Text = usernameHint ?? string.Empty;

        var fields = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 3,
            Padding = new Padding(16, 16, 16, 8),
        };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));

        fields.Controls.Add(new Label { Text = "Username/email:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        fields.Controls.Add(_username, 1, 0);
        fields.Controls.Add(new Label { Text = "Password:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        fields.Controls.Add(_password, 1, 1);
        fields.Controls.Add(_otpLabel, 0, 2);
        fields.Controls.Add(_otp, 1, 2);

        var bottom = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 56,
            Padding = new Padding(16, 8, 16, 12),
        };
        _cancel.Anchor = AnchorStyles.Right | AnchorStyles.Top;
        _ok.Anchor = AnchorStyles.Right | AnchorStyles.Top;
        bottom.Controls.Add(_cancel);
        bottom.Controls.Add(_ok);
        bottom.Resize += (_, _) =>
        {
            _cancel.Left = bottom.ClientSize.Width - bottom.Padding.Right - _cancel.Width;
            _cancel.Top = 8;
            _ok.Left = _cancel.Left - 12 - _ok.Width;
            _ok.Top = 8;
        };

        _ok.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
            {
                MessageBox.Show(this, "Username and password are required.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        };

        Controls.Add(fields);
        Controls.Add(bottom);
        // Force initial button positions before first resize event.
        bottom.PerformLayout();
        _cancel.Left = bottom.ClientSize.Width - bottom.Padding.Right - _cancel.Width;
        _cancel.Top = 8;
        _ok.Left = _cancel.Left - 12 - _ok.Width;
        _ok.Top = 8;

        AcceptButton = _ok;
        CancelButton = _cancel;
    }

    public string Username => _username.Text.Trim();
    public string Password => _password.Text;
    public string OtpCode => _otp.Text.Trim();

    public void PromptForOtp(string message)
    {
        _otpLabel.Text = message;
        _otp.Focus();
    }
}
