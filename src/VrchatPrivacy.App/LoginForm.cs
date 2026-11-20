namespace VrchatPrivacy.App;

public sealed class LoginForm : Form
{
    private readonly TextBox _username = new() { Dock = DockStyle.Fill };
    private readonly TextBox _password = new() { Dock = DockStyle.Fill, UseSystemPasswordChar = true };
    private readonly TextBox _otp = new() { Dock = DockStyle.Fill };
    private readonly Label _otpLabel = new() { Text = "2FA code (if prompted):", AutoSize = true };
    private readonly Button _ok = new() { Text = "Sign in", DialogResult = DialogResult.None };
    private readonly Button _cancel = new() { Text = "Cancel", DialogResult = DialogResult.Cancel };

    public LoginForm(string? usernameHint = null)
    {
        Text = "VRChat login";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = true;
        ClientSize = new Size(360, 180);
        _username.Text = usernameHint ?? string.Empty;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 5,
            Padding = new Padding(12),
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.Controls.Add(new Label { Text = "Username/email:", AutoSize = true }, 0, 0);
        layout.Controls.Add(_username, 1, 0);
        layout.Controls.Add(new Label { Text = "Password:", AutoSize = true }, 0, 1);
        layout.Controls.Add(_password, 1, 1);
        layout.Controls.Add(_otpLabel, 0, 2);
        layout.Controls.Add(_otp, 1, 2);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        buttons.Controls.Add(_ok);
        buttons.Controls.Add(_cancel);
        layout.Controls.Add(buttons, 0, 4);
        layout.SetColumnSpan(buttons, 2);

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

        Controls.Add(layout);
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
