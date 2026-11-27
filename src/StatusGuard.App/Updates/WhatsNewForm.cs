namespace StatusGuard.App.Updates;

public sealed class WhatsNewForm : Form
{
    public WhatsNewForm(ReleaseNotes notes)
    {
        Text = "What's New - Status Guard";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(480, 360);
        Size = new Size(560, 480);
        Font = new Font("Segoe UI", 10f);
        ShowInTaskbar = true;

        var title = new Label
        {
            AutoSize = false,
            Dock = DockStyle.Top,
            Height = 36,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(12, 8, 12, 0),
            Text = notes.MatchedExactTag
                ? notes.Title
                : $"{notes.Title} (showing latest; no notes for exact tag)",
            Font = new Font(Font, FontStyle.Bold),
        };

        var body = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            BorderStyle = BorderStyle.FixedSingle,
            Text = notes.Body.Replace("\r\n", "\n").Replace("\n", Environment.NewLine),
            Margin = new Padding(12),
        };

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 48,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(8),
        };

        var close = new Button { Text = "Close", DialogResult = DialogResult.OK, AutoSize = true };
        buttons.Controls.Add(close);

        if (!string.IsNullOrWhiteSpace(notes.HtmlUrl))
        {
            var open = new Button { Text = "Open on GitHub", AutoSize = true };
            open.Click += (_, _) =>
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = notes.HtmlUrl,
                        UseShellExecute = true,
                    });
                }
                catch
                {
                    // ignore
                }
            };
            buttons.Controls.Add(open);
        }

        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 4, 12, 4) };
        panel.Controls.Add(body);

        Controls.Add(panel);
        Controls.Add(buttons);
        Controls.Add(title);
        AcceptButton = close;
        CancelButton = close;
    }
}
