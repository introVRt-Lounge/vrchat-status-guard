using StatusGuard.Core.Settings;
using StatusGuard.Core.Telemetry;
using StatusGuard.App.Telemetry;
using StatusGuard.App.Ui;

namespace StatusGuard.App;

/// <summary>First-run tour: login (if needed), protect one world, then tray tips.</summary>
public sealed class FirstRunWizardForm : Form
{
    private enum Phase
    {
        Intro,
        Configure,
        Summary,
        TrayTips,
    }

    private readonly AppSettings _settings;
    private readonly HubCatalog _catalog;
    private readonly Panel _host = new() { Dock = DockStyle.Fill, BackColor = VrcTheme.BgDeep };
    private Phase _phase = Phase.Intro;
    private AddWorldFlowControl.Result? _choice;
    private Icon? _trayPreviewIcon;

    /// <summary>
    /// Optional: ensure a VRChat session before Configure. Return true when authenticated.
    /// Cancelled / failed login should return false (wizard stays on Intro).
    /// </summary>
    public Func<Task<bool>>? EnsureAuthenticatedAsync { get; init; }

    public FirstRunWizardForm(AppSettings settings, HubCatalog catalog, Icon? trayIcon = null)
    {
        _settings = settings;
        _catalog = catalog;
        _trayPreviewIcon = trayIcon;

        Text = "Welcome to Status Guard";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = true;
        ClientSize = new Size(760, 640);
        VrcTheme.StyleForm(this);

        Controls.Add(_host);
        Shown += (_, _) =>
        {
            _catalog.BeginWarmup();
            Render();
        };
    }

    public AppSettings Result => _settings;

    private void Render()
    {
        _host.Controls.Clear();
        switch (_phase)
        {
            case Phase.Intro:
                RenderIntro();
                break;
            case Phase.Configure:
                RenderConfigure();
                break;
            case Phase.Summary:
                _ = RenderSummaryAsync();
                break;
            case Phase.TrayTips:
                RenderTrayTips();
                break;
        }
    }

    private void RenderIntro()
    {
        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(40) };
        var brand = new Label
        {
            Text = "Status Guard",
            Font = VrcTheme.TitleFont,
            ForeColor = VrcTheme.Accent,
            AutoSize = true,
            Location = new Point(40, 48),
        };
        var headline = new Label
        {
            Text = "Set up your first protected world",
            Font = new Font("Segoe UI Semibold", 22f, FontStyle.Bold),
            ForeColor = VrcTheme.TextPrimary,
            AutoSize = true,
            Location = new Point(40, 100),
            MaximumSize = new Size(640, 0),
        };
        var body = new Label
        {
            Text =
                "First we sign you into VRChat (unofficial API - no public OAuth yet), then you pick a " +
                "protected world. When you join it, we'll ask the API for Ask Me, Do Not Disturb, or Join Me, " +
                "and optionally set a group you're representing.\n\n" +
                "Takes about a minute. You can change everything later from the tray.",
            Font = VrcTheme.BodyFont,
            ForeColor = VrcTheme.TextMuted,
            Location = new Point(40, 180),
            Size = new Size(640, 110),
        };

        var telemetry = new CheckBox
        {
            Checked = _settings.TelemetryEnabled,
            AutoSize = false,
            Location = new Point(40, 300),
            Size = new Size(660, 72),
            Font = VrcTheme.SmallFont,
            ForeColor = VrcTheme.TextMuted,
            Text =
                "Early builds: you're a guinea pig. Send anonymous usage diagnostics so we can see if " +
                "protection works and where setup stalls (timings, screens hit - not your VRChat identity). " +
                "Uncheck to opt out. Change anytime in Protection hub → Privacy.",
        };

        var start = new Button { Text = "Let's go", Size = new Size(140, 40), Location = new Point(40, 390) };
        VrcTheme.StylePrimaryButton(start);
        start.Click += async (_, _) =>
        {
            start.Enabled = false;
            try
            {
                ApplyTelemetryChoice(telemetry.Checked);
                TelemetryUi.WizardStep("intro", "continue");
                if (EnsureAuthenticatedAsync is not null)
                {
                    var ok = await EnsureAuthenticatedAsync().ConfigureAwait(true);
                    if (!ok)
                    {
                        TelemetryUi.WizardStep("login", "cancel_or_fail");
                        return;
                    }

                    TelemetryUi.WizardStep("login", "ok");
                }

                _phase = Phase.Configure;
                TelemetryUi.WizardStep("configure", "enter");
                Render();
            }
            finally
            {
                if (!IsDisposed && start is { IsDisposed: false })
                {
                    start.Enabled = true;
                }
            }
        };
        var skip = new Button { Text = "Skip for now", Size = new Size(120, 36), Location = new Point(200, 392) };
        VrcTheme.StyleGhostButton(skip);
        skip.Click += (_, _) =>
        {
            ApplyTelemetryChoice(telemetry.Checked);
            TelemetryUi.WizardStep("intro", "skip");
            _settings.OnboardingCompleted = true;
            DialogResult = DialogResult.OK;
            Close();
        };

        panel.Controls.Add(brand);
        panel.Controls.Add(headline);
        panel.Controls.Add(body);
        panel.Controls.Add(telemetry);
        panel.Controls.Add(start);
        panel.Controls.Add(skip);
        _host.Controls.Add(panel);
        TelemetryUi.WizardStep("intro", "shown");
    }

    private void ApplyTelemetryChoice(bool enabled)
    {
        _settings.TelemetryEnabled = enabled;
        _settings.TelemetryPromptShown = true;
        TelemetryGate.SetEnabled(enabled);
        TelemetryGate.Track(
            "telemetry_consent",
            ("enabled", enabled ? "1" : "0"),
            ("source", "wizard"));
    }

    private void RenderConfigure()
    {
        var flow = new AddWorldFlowControl(_catalog, AddWorldFlowControl.Mode.FromRecent) { Dock = DockStyle.Fill };
        flow.Completed += (_, result) =>
        {
            _choice = result;
            if (!_settings.ProtectedWorldIds.Any(id =>
                    string.Equals(id, result.World.Id, StringComparison.OrdinalIgnoreCase)))
            {
                _settings.ProtectedWorldIds.Add(result.World.Id);
            }

            _settings.SetStatusForWorld(result.World.Id, result.Status);
            _settings.SetRepresentGroupId(result.World.Id, result.Group?.Id);
            // First protected choice also becomes the default status.
            _settings.ProtectionStatus = result.Status;
            _settings.EnsureDefaultWorldPresent();
            TelemetryUi.WizardStep(
                "configure",
                result.Group is null ? "world_only" : "world_and_group");
            _phase = Phase.Summary;
            Render();
        };
        _host.Controls.Add(flow);
    }

    private async Task RenderSummaryAsync()
    {
        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(32), AutoScroll = true };
        panel.Controls.Add(new Label
        {
            Text = "You're set",
            Font = VrcTheme.TitleFont,
            ForeColor = VrcTheme.TextPrimary,
            AutoSize = true,
            Location = new Point(32, 24),
        });
        panel.Controls.Add(new Label
        {
            Text = "Here's what will happen when you join that world:",
            Font = VrcTheme.BodyFont,
            ForeColor = VrcTheme.TextMuted,
            AutoSize = true,
            Location = new Point(34, 64),
        });

        if (_choice is null)
        {
            _host.Controls.Add(panel);
            return;
        }

        var card = VrcTheme.Card();
        card.Location = new Point(32, 100);
        card.Size = new Size(660, 280);

        var worldPic = new PictureBox
        {
            Location = new Point(24, 24),
            Size = new Size(220, 124),
            SizeMode = PictureBoxSizeMode.Zoom,
            Image = await _catalog.GetImageAsync(_choice.World.ThumbnailImageUrl, 160, 90).ConfigureAwait(true),
        };
        var worldName = new Label
        {
            Text = _choice.World.Name,
            Font = VrcTheme.HeadingFont,
            ForeColor = VrcTheme.TextPrimary,
            Location = new Point(264, 28),
            AutoSize = true,
            MaximumSize = new Size(360, 0),
        };
        var status = new Label
        {
            Text = StatusUx.ShortName(_choice.Status),
            Font = new Font("Segoe UI Semibold", 14f, FontStyle.Bold),
            ForeColor = VrcTheme.ForStatus(_choice.Status),
            Location = new Point(264, 70),
            AutoSize = true,
        };
        var meaning = new Label
        {
            Text = StatusUx.Meaning(_choice.Status),
            Font = VrcTheme.BodyFont,
            ForeColor = VrcTheme.TextMuted,
            Location = new Point(264, 104),
            Size = new Size(360, 60),
        };
        card.Controls.Add(worldPic);
        card.Controls.Add(worldName);
        card.Controls.Add(status);
        card.Controls.Add(meaning);

        if (_choice.Group is not null)
        {
            var gPic = new PictureBox
            {
                Location = new Point(24, 168),
                Size = new Size(72, 72),
                SizeMode = PictureBoxSizeMode.Zoom,
                Image = await _catalog.GetImageAsync(_choice.Group.IconUrl ?? _choice.Group.BannerUrl, 72, 72)
                    .ConfigureAwait(true),
            };
            var gName = new Label
            {
                Text = _choice.Group.Name,
                Font = VrcTheme.HeadingFont,
                ForeColor = VrcTheme.TextPrimary,
                Location = new Point(112, 178),
                AutoSize = true,
            };
            var gHeader = new Label
            {
                Text = $"Representing · {_choice.Group.HeaderLabel}",
                Font = VrcTheme.BodyFont,
                ForeColor = VrcTheme.Accent,
                Location = new Point(112, 210),
                AutoSize = true,
            };
            card.Controls.Add(gPic);
            card.Controls.Add(gName);
            card.Controls.Add(gHeader);
        }
        else
        {
            card.Controls.Add(new Label
            {
                Text = "Group representation: unchanged",
                ForeColor = VrcTheme.TextMuted,
                Location = new Point(24, 180),
                AutoSize = true,
            });
        }

        var next = new Button { Text = "How to use the tray", Size = new Size(180, 40), Location = new Point(32, 400) };
        VrcTheme.StylePrimaryButton(next);
        next.Click += (_, _) =>
        {
            TelemetryUi.WizardStep("summary", "continue");
            _phase = Phase.TrayTips;
            Render();
        };

        panel.Controls.Add(card);
        panel.Controls.Add(next);
        _host.Controls.Add(panel);
        TelemetryUi.WizardStep("summary", "shown");
    }

    private void RenderTrayTips()
    {
        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(40) };
        panel.Controls.Add(new Label
        {
            Text = "Find Status Guard in the system tray",
            Font = VrcTheme.TitleFont,
            ForeColor = VrcTheme.TextPrimary,
            AutoSize = true,
            Location = new Point(40, 36),
            MaximumSize = new Size(640, 0),
        });

        var iconBox = new Panel
        {
            Location = new Point(40, 110),
            Size = new Size(96, 96),
            BackColor = VrcTheme.BgPanel,
        };
        iconBox.Paint += (_, e) =>
        {
            using var pen = new Pen(VrcTheme.Accent, 2);
            e.Graphics.DrawRectangle(pen, 1, 1, iconBox.Width - 3, iconBox.Height - 3);
            var icon = _trayPreviewIcon ?? SystemIcons.Shield;
            e.Graphics.DrawIcon(icon, new Rectangle(24, 24, 48, 48));
        };

        panel.Controls.Add(iconBox);
        panel.Controls.Add(new Label
        {
            Text = "This is your tray icon (look near the clock).",
            Font = VrcTheme.BodyFont,
            ForeColor = VrcTheme.TextMuted,
            Location = new Point(152, 140),
            AutoSize = true,
        });

        var tips = new FlowLayoutPanel
        {
            Location = new Point(DpiUtil.Scale(this, 40), DpiUtil.Scale(this, 220)),
            Width = DpiUtil.Scale(this, 660),
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = VrcTheme.BgDeep,
        };
        tips.Controls.Add(MakeTipCard("Double-click", "Opens this protection hub — see protected worlds, add more, change status/group."));
        tips.Controls.Add(MakeTipCard("Single-click", "Pauses or resumes protection. Icon/tooltip update so you know it's working."));
        tips.Controls.Add(MakeTipCard("Right-click", "Full menu: Logout / Login, Apply status now, Activity log, Exit."));
        panel.Controls.Add(tips);

        var done = new Button { Text = "Finish", Location = new Point(DpiUtil.Scale(this, 40), DpiUtil.Scale(this, 530)) };
        VrcTheme.StylePrimaryButton(done);
        done.Click += (_, _) =>
        {
            TelemetryUi.WizardStep("tray_tips", "finish");
            _settings.OnboardingCompleted = true;
            DialogResult = DialogResult.OK;
            Close();
        };
        TelemetryUi.WizardStep("tray_tips", "shown");
        // Place Finish below tips once laid out.
        tips.Layout += (_, _) =>
        {
            done.Top = tips.Bottom + DpiUtil.Scale(this, 16);
        };
        panel.Controls.Add(done);
        _host.Controls.Add(panel);
    }

    private Panel MakeTipCard(string title, string body)
    {
        var pad = DpiUtil.Scale(this, 16);
        var card = VrcTheme.Card();
        card.Width = DpiUtil.Scale(this, 640);
        card.Margin = new Padding(0, 0, 0, DpiUtil.Scale(this, 10));
        card.AutoSize = true;
        card.Padding = new Padding(pad);
        var stack = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Width = DpiUtil.Scale(this, 600),
            BackColor = VrcTheme.BgPanel,
        };
        stack.Controls.Add(DpiUtil.AutoLabel(title, VrcTheme.HeadingFont, VrcTheme.Accent, DpiUtil.Scale(this, 600)));
        stack.Controls.Add(DpiUtil.AutoLabel(body, VrcTheme.BodyFont, VrcTheme.TextPrimary, DpiUtil.Scale(this, 600)));
        card.Controls.Add(stack);
        card.PerformLayout();
        card.Height = stack.PreferredSize.Height + (pad * 2);
        return card;
    }
}
