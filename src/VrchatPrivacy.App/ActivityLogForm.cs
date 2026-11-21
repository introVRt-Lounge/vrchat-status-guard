using System.Diagnostics;
using VrchatPrivacy.App.Ui;
using VrchatPrivacy.Core.Logging;

namespace VrchatPrivacy.App;

/// <summary>Live view of guard.log - world joins, decisions, ApplyStatus, pauses.</summary>
public sealed class ActivityLogForm : Form
{
    private readonly TextBox _body;
    private readonly Label _pathLabel;
    private readonly CheckBox _follow;
    private readonly System.Windows.Forms.Timer _timer;
    private string _lastText = string.Empty;

    public ActivityLogForm()
    {
        Text = "Activity log - VRChat Status Guard";
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimizeBox = true;
        MaximizeBox = true;
        ShowInTaskbar = true;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(640, 420);
        ClientSize = new Size(780, 520);
        VrcTheme.StyleForm(this);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = DpiUtil.Scale(this, 12, 10, 12, 10),
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var header = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
        };
        header.Controls.Add(DpiUtil.AutoLabel(
            "What Status Guard is doing and when (joins, decisions, status/group applies).",
            VrcTheme.BodyFont,
            VrcTheme.TextPrimary,
            maxWidth: 720));
        _pathLabel = DpiUtil.AutoLabel(AppLog.FilePath, VrcTheme.SmallFont, VrcTheme.TextMuted, maxWidth: 720);
        header.Controls.Add(_pathLabel);
        root.Controls.Add(header, 0, 0);

        _body = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            Font = new Font("Consolas", 9.5f),
            BackColor = VrcTheme.BgDeep,
            ForeColor = VrcTheme.TextPrimary,
            BorderStyle = BorderStyle.FixedSingle,
        };
        root.Controls.Add(_body, 0, 1);

        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, 8, 0, 0),
        };

        var reload = new Button { Text = "Reload", AutoSize = true };
        VrcTheme.StyleGhostButton(reload);
        reload.Click += (_, _) => RefreshLog(forceScroll: false);

        _follow = new CheckBox
        {
            Text = "Auto-refresh",
            Checked = true,
            AutoSize = true,
            ForeColor = VrcTheme.TextPrimary,
            Font = VrcTheme.SmallFont,
            Margin = new Padding(12, 8, 8, 0),
        };

        var openFolder = new Button { Text = "Open folder", AutoSize = true };
        VrcTheme.StyleGhostButton(openFolder);
        openFolder.Click += (_, _) => OpenLogFolder();

        var copyPath = new Button { Text = "Copy path", AutoSize = true };
        VrcTheme.StyleGhostButton(copyPath);
        copyPath.Click += (_, _) =>
        {
            try
            {
                Clipboard.SetText(AppLog.FilePath);
            }
            catch
            {
                // clipboard quirks
            }
        };

        var close = new Button { Text = "Close", DialogResult = DialogResult.OK, AutoSize = true };
        VrcTheme.StylePrimaryButton(close);

        footer.Controls.Add(reload);
        footer.Controls.Add(_follow);
        footer.Controls.Add(openFolder);
        footer.Controls.Add(copyPath);
        footer.Controls.Add(close);
        root.Controls.Add(footer, 0, 2);

        Controls.Add(root);
        AcceptButton = close;
        CancelButton = close;

        _timer = new System.Windows.Forms.Timer { Interval = 2000 };
        _timer.Tick += (_, _) =>
        {
            if (_follow.Checked)
            {
                RefreshLog(forceScroll: true);
            }
        };

        Load += (_, _) =>
        {
            RefreshLog(forceScroll: true);
            _timer.Start();
        };
        FormClosed += (_, _) => _timer.Stop();
    }

    private void RefreshLog(bool forceScroll)
    {
        var text = AppLog.ReadRecent(500);
        _pathLabel.Text = AppLog.FilePath;
        if (string.Equals(text, _lastText, StringComparison.Ordinal))
        {
            return;
        }

        var stickToEnd = forceScroll ||
            (_body.SelectionStart >= Math.Max(0, _body.TextLength - 4));
        _lastText = text;
        _body.Text = text;
        if (stickToEnd)
        {
            _body.SelectionStart = _body.TextLength;
            _body.ScrollToCaret();
        }
    }

    private static void OpenLogFolder()
    {
        try
        {
            var dir = Path.GetDirectoryName(AppLog.FilePath);
            if (string.IsNullOrWhiteSpace(dir))
            {
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = dir,
                UseShellExecute = true,
            });
        }
        catch
        {
            // shell can refuse
        }
    }

    public static void ShowLog(IWin32Window? owner)
    {
        using var form = new ActivityLogForm();
        if (owner is not null)
        {
            form.ShowDialog(owner);
        }
        else
        {
            form.ShowDialog();
        }
    }
}
