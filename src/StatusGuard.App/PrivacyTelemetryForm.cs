using StatusGuard.Core.Settings;
using StatusGuard.Core.Telemetry;
using StatusGuard.App.Ui;

namespace StatusGuard.App;

/// <summary>Hub → Privacy: opt out of early-adopter extreme telemetry.</summary>
public sealed class PrivacyTelemetryForm : Form
{
    public PrivacyTelemetryForm(AppSettings settings)
    {
        Text = "Privacy";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(520, 320);
        VrcTheme.StyleForm(this);

        var title = DpiUtil.AutoLabel("Usage diagnostics", VrcTheme.HeadingFont, VrcTheme.TextPrimary);
        title.Location = new Point(24, 20);

        var body = new Label
        {
            Location = new Point(24, 56),
            Size = new Size(470, 120),
            Font = VrcTheme.SmallFont,
            ForeColor = VrcTheme.TextMuted,
            Text =
                "Early Status Guard builds treat first users as guinea pigs: we collect anonymous " +
                "timings and which screens you hit so we can tell if protection works. We do not " +
                "collect VRChat usernames, cookies, world IDs, or chat. Details: TELEMETRY.md in the repo. " +
                "A later release will tone this way down.",
        };

        var box = new CheckBox
        {
            Checked = settings.TelemetryEnabled,
            Location = new Point(24, 190),
            Size = new Size(470, 40),
            Font = VrcTheme.BodyFont,
            ForeColor = VrcTheme.TextPrimary,
            Text = "Send anonymous usage diagnostics (opt out anytime)",
        };

        var save = new Button { Text = "Save", Size = new Size(100, 36), Location = new Point(280, 250) };
        var cancel = new Button { Text = "Cancel", Size = new Size(100, 36), Location = new Point(390, 250), DialogResult = DialogResult.Cancel };
        VrcTheme.StylePrimaryButton(save);
        VrcTheme.StyleGhostButton(cancel);
        save.Click += (_, _) =>
        {
            settings.TelemetryEnabled = box.Checked;
            settings.TelemetryPromptShown = true;
            TelemetryGate.SetEnabled(box.Checked);
            TelemetryGate.Track(
                "telemetry_consent",
                ("enabled", box.Checked ? "1" : "0"),
                ("source", "hub_privacy"));
            DialogResult = DialogResult.OK;
            Close();
        };

        Controls.Add(title);
        Controls.Add(body);
        Controls.Add(box);
        Controls.Add(save);
        Controls.Add(cancel);
        AcceptButton = save;
        CancelButton = cancel;
    }
}
