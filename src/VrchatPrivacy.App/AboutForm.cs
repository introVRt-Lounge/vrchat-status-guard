using System.Diagnostics;
using VrchatPrivacy.App.Ui;
using VrchatPrivacy.App.Updates;

namespace VrchatPrivacy.App;

/// <summary>Credits (introVRt Lounge) + feedback (GitHub issues / PRs welcome).</summary>
public sealed class AboutForm : Form
{
    public AboutForm()
    {
        Text = "About - VRChat Status Guard";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(520, 520);
        VrcTheme.StyleForm(this);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = DpiUtil.Scale(this, 20, 16, 20, 16),
        };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var scroll = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = VrcTheme.BgDeep,
        };

        var stack = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Width = 460,
        };

        stack.Controls.Add(DpiUtil.AutoLabel("VRChat Status Guard", VrcTheme.TitleFont, VrcTheme.TextPrimary));
        stack.Controls.Add(DpiUtil.AutoLabel(
            $"Version {AppUpdateService.CurrentVersion}",
            VrcTheme.SmallFont,
            VrcTheme.TextMuted));
        stack.Controls.Add(Spacer(8));
        stack.Controls.Add(DpiUtil.AutoLabel(
            "Windows tray helper that applies Ask Me / Do Not Disturb / Join Me when you enter worlds you protect.",
            VrcTheme.BodyFont,
            VrcTheme.TextPrimary,
            maxWidth: 460));

        stack.Controls.Add(Spacer(16));
        stack.Controls.Add(Section("Credits"));
        stack.Controls.Add(DpiUtil.AutoLabel(
            "Built for the introVRt Lounge community - movies, games, and VR hangouts with fellow introverts.",
            VrcTheme.BodyFont,
            VrcTheme.TextPrimary,
            maxWidth: 460));
        stack.Controls.Add(LinkRow("Website", LoungeLinks.Website));
        stack.Controls.Add(LinkRow("Discord", LoungeLinks.DiscordInvite));
        stack.Controls.Add(LinkRow("GitHub org", LoungeLinks.GitHubOrg));
        stack.Controls.Add(DpiUtil.AutoLabel(
            "In VRChat: search for introVRt Lounge in Groups, or ask on Discord for the current group link.",
            VrcTheme.SmallFont,
            VrcTheme.TextMuted,
            maxWidth: 460));

        stack.Controls.Add(Spacer(16));
        stack.Controls.Add(Section("Feedback"));
        stack.Controls.Add(DpiUtil.AutoLabel(
            "Bugs and ideas go to GitHub Issues. Pull requests are welcome - see CONTRIBUTING in the repo.",
            VrcTheme.BodyFont,
            VrcTheme.TextPrimary,
            maxWidth: 460));
        stack.Controls.Add(LinkRow("File feedback (Issues)", LoungeLinks.FeedbackIssues));
        stack.Controls.Add(LinkRow("Pull requests", LoungeLinks.PullRequests));
        stack.Controls.Add(LinkRow("Source repository", LoungeLinks.Repo));

        stack.Controls.Add(Spacer(16));
        stack.Controls.Add(DpiUtil.AutoLabel(
            "MIT License. Unofficial tool - not affiliated with VRChat Inc.",
            VrcTheme.SmallFont,
            VrcTheme.TextMuted,
            maxWidth: 460));

        scroll.Controls.Add(stack);
        root.Controls.Add(scroll, 0, 0);

        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Padding = new Padding(0, 8, 0, 0),
        };
        var close = new Button { Text = "Close", DialogResult = DialogResult.OK };
        VrcTheme.StylePrimaryButton(close);
        footer.Controls.Add(close);
        root.Controls.Add(footer, 0, 1);

        Controls.Add(root);
        AcceptButton = close;
        CancelButton = close;
    }

    private static Control Spacer(int height) => new Panel { Height = height, Width = 1 };

    private Label Section(string text) =>
        DpiUtil.AutoLabel(text, VrcTheme.HeadingFont, VrcTheme.Accent);

    private Control LinkRow(string label, string url)
    {
        var row = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0, 4, 0, 0),
        };
        row.Controls.Add(new Label
        {
            AutoSize = true,
            Text = label + ":",
            ForeColor = VrcTheme.TextMuted,
            Font = VrcTheme.SmallFont,
            Margin = new Padding(0, 4, 8, 0),
        });
        var link = new LinkLabel
        {
            AutoSize = true,
            Text = url.Replace("https://", ""),
            Font = VrcTheme.SmallFont,
            LinkColor = VrcTheme.Accent,
            ActiveLinkColor = VrcTheme.TextPrimary,
            VisitedLinkColor = VrcTheme.AccentDim,
            Margin = new Padding(0, 4, 0, 0),
        };
        link.Links.Add(0, link.Text.Length, url);
        link.LinkClicked += (_, e) =>
        {
            if (e.Link?.LinkData is string href)
            {
                OpenUrl(href);
            }
        };
        row.Controls.Add(link);
        return row;
    }

    public static void ShowAbout(IWin32Window? owner)
    {
        using var form = new AboutForm();
        if (owner is not null)
        {
            form.ShowDialog(owner);
        }
        else
        {
            form.StartPosition = FormStartPosition.CenterScreen;
            form.ShowDialog();
        }
    }

    public static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch
        {
            // Shell can refuse; About still shows the URL as text.
        }
    }
}
