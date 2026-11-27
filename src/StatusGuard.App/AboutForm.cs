using System.Diagnostics;
using StatusGuard.App.Ui;
using StatusGuard.App.Updates;
using StatusGuard.Core.Activity;

namespace StatusGuard.App;

/// <summary>Credits (introVRt Lounge) + feedback (GitHub issues / PRs welcome).</summary>
public sealed class AboutForm : Form
{
    private readonly ActivityJournal? _activity;
    private readonly HubCatalog? _catalog;

    public AboutForm(ActivityJournal? activity = null, HubCatalog? catalog = null)
    {
        _activity = activity;
        _catalog = catalog;
        Text = "About - Status Guard";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(520, 640);
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

        var logo = TryLoadLogo(DpiUtil.Scale(this, 120));
        if (logo is not null)
        {
            stack.Controls.Add(new PictureBox
            {
                Image = logo,
                SizeMode = PictureBoxSizeMode.Zoom,
                Size = new Size(DpiUtil.Scale(this, 120), DpiUtil.Scale(this, 120)),
                Margin = new Padding(0, 0, 0, 8),
            });
        }

        stack.Controls.Add(DpiUtil.AutoLabel("Status Guard", VrcTheme.TitleFont, VrcTheme.TextPrimary));
        stack.Controls.Add(DpiUtil.AutoLabel(
            $"Version {AppUpdateService.CurrentVersion}",
            VrcTheme.SmallFont,
            VrcTheme.TextMuted));
        stack.Controls.Add(Spacer(8));
        stack.Controls.Add(DpiUtil.AutoLabel(
            "Do you like to party? Do you also like to chill? Status Guard asks the API to keep your private private - when VRChat's client listens.",
            VrcTheme.BodyFont,
            VrcTheme.TextPrimary,
            maxWidth: 460));

        stack.Controls.Add(Spacer(16));
        stack.Controls.Add(Section("Credits"));
        stack.Controls.Add(DpiUtil.AutoLabel(
            "Brought to you by the introVRt Lounge - movies, games, and VR hangouts with fellow introverts.",
            VrcTheme.BodyFont,
            VrcTheme.TextPrimary,
            maxWidth: 460));
        stack.Controls.Add(LinkRow("Website", LoungeLinks.Website));
        stack.Controls.Add(LinkRow("Discord", LoungeLinks.DiscordInvite));
        stack.Controls.Add(LinkRow("VRChat group", LoungeLinks.VrchatGroup));
        stack.Controls.Add(LinkRow("GitHub org", LoungeLinks.GitHubOrg));

        stack.Controls.Add(Spacer(16));
        stack.Controls.Add(Section("Honest limitations"));
        stack.Controls.Add(DpiUtil.AutoLabel(
            "API status writes often do not update the live game client. Friends can still see Join Me after we set Ask Me. Until VRChat fixes that, mileage will vary - do not rely on this alone for privacy. Braces without a belt.",
            VrcTheme.BodyFont,
            VrcTheme.TextPrimary,
            maxWidth: 460));
        stack.Controls.Add(LinkRow("VRChat feedback (status desync)", LoungeLinks.VrchatStatusApiDesync));
        stack.Controls.Add(DpiUtil.AutoLabel(
            "Platform guidance (via Tupper) says third-party apps must not query the unofficial API more than once per 60 seconds. Abuse can terminate accounts. Status Guard enforces that as a hard mutation budget.",
            VrcTheme.BodyFont,
            VrcTheme.TextPrimary,
            maxWidth: 460));
        stack.Controls.Add(DpiUtil.AutoLabel(
            "Status may take up to ~60s after a recent mutation (or apply almost immediately if the budget is free). Status always comes before optional group repping. Hopping worlds does not reset the timer - we coalesce to your latest world and cancel stale applies.",
            VrcTheme.BodyFont,
            VrcTheme.TextPrimary,
            maxWidth: 460));
        stack.Controls.Add(LinkRow("Citation (C# SDK disclaimer)", LoungeLinks.VrchatApiCsharpDisclaimer));
        stack.Controls.Add(LinkRow("Citation (API specification)", LoungeLinks.VrchatApiSpecification));

        stack.Controls.Add(Spacer(16));
        stack.Controls.Add(Section("Activity log"));
        stack.Controls.Add(DpiUtil.AutoLabel(
            "See world joins, protection decisions, and status/group applies with timestamps.",
            VrcTheme.BodyFont,
            VrcTheme.TextPrimary,
            maxWidth: 460));
        var openLog = new Button { Text = "Open activity log…", AutoSize = true, Margin = new Padding(0, 6, 0, 0) };
        VrcTheme.StyleGhostButton(openLog);
        openLog.Click += (_, _) =>
        {
            if (_activity is null)
            {
                return;
            }

            ActivityLogForm.ShowLog(this, _activity, _catalog);
        };
        stack.Controls.Add(openLog);

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

    private static Image? TryLoadLogo(int sizeHint)
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Assets", "status-guard-logo.png");
            if (!File.Exists(path))
            {
                return null;
            }

            using var fs = File.OpenRead(path);
            using var img = Image.FromStream(fs);
            return new Bitmap(img);
        }
        catch
        {
            return null;
        }
    }

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

    public static void ShowAbout(
        IWin32Window? owner,
        ActivityJournal? activity = null,
        HubCatalog? catalog = null)
    {
        using var form = new AboutForm(activity, catalog);
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
