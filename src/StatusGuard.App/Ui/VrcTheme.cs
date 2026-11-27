using StatusGuard.Core.Protection;

namespace StatusGuard.App.Ui;

/// <summary>
/// Visual language inspired by VRChat's dark client UI: deep charcoal panels,
/// teal accent (legacy Quick Menu DNA), and social-status colours for blue/orange/red.
/// </summary>
public static class VrcTheme
{
    public static readonly Color BgDeep = Color.FromArgb(18, 20, 28);
    public static readonly Color BgPanel = Color.FromArgb(28, 32, 44);
    public static readonly Color BgElevated = Color.FromArgb(38, 44, 60);
    public static readonly Color BgHover = Color.FromArgb(48, 56, 78);
    public static readonly Color Accent = Color.FromArgb(0, 196, 180); // legacy teal
    public static readonly Color AccentDim = Color.FromArgb(0, 140, 130);
    public static readonly Color TextPrimary = Color.FromArgb(245, 247, 250);
    public static readonly Color TextMuted = Color.FromArgb(160, 170, 190);
    public static readonly Color Border = Color.FromArgb(55, 62, 82);

    public static readonly Color StatusBlue = Color.FromArgb(72, 167, 255);
    public static readonly Color StatusOrange = Color.FromArgb(255, 158, 64);
    public static readonly Color StatusRed = Color.FromArgb(255, 82, 82);

    public static Font TitleFont { get; } = new("Segoe UI Semibold", 18f, FontStyle.Bold);
    public static Font HeadingFont { get; } = new("Segoe UI Semibold", 13f, FontStyle.Bold);
    public static Font BodyFont { get; } = new("Segoe UI", 10.5f, FontStyle.Regular);
    public static Font SmallFont { get; } = new("Segoe UI", 9f, FontStyle.Regular);

    public static Color ForStatus(ProtectionStatus status) => status switch
    {
        ProtectionStatus.Blue => StatusBlue,
        ProtectionStatus.Red => StatusRed,
        _ => StatusOrange,
    };

    public static void StyleForm(Form form)
    {
        form.BackColor = BgDeep;
        form.ForeColor = TextPrimary;
        form.Font = BodyFont;
        form.AutoScaleMode = AutoScaleMode.Dpi;
        form.AutoScaleDimensions = new SizeF(96F, 96F);
    }

    public static void StylePrimaryButton(Button button)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.BackColor = Accent;
        button.ForeColor = Color.FromArgb(10, 20, 24);
        button.Font = new Font("Segoe UI Semibold", 10.5f, FontStyle.Bold);
        button.Cursor = Cursors.Hand;
        DpiUtil.MakeAutoButton(button);
    }

    public static void StyleGhostButton(Button button)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderColor = Border;
        button.FlatAppearance.BorderSize = 1;
        button.BackColor = BgElevated;
        button.ForeColor = TextPrimary;
        button.Font = BodyFont;
        button.Cursor = Cursors.Hand;
        DpiUtil.MakeAutoButton(button);
    }

    public static Panel Card(Control? parent = null)
    {
        var panel = new Panel
        {
            BackColor = BgPanel,
            Padding = new Padding(16),
        };
        panel.Paint += (_, e) =>
        {
            using var pen = new Pen(Border);
            e.Graphics.DrawRectangle(pen, 0, 0, panel.Width - 1, panel.Height - 1);
        };
        parent?.Controls.Add(panel);
        return panel;
    }

    public static Label MutedLabel(string text) => new()
    {
        Text = text,
        ForeColor = TextMuted,
        Font = SmallFont,
        AutoSize = true,
    };

    public static Label Heading(string text) => new()
    {
        Text = text,
        ForeColor = TextPrimary,
        Font = HeadingFont,
        AutoSize = true,
    };

    public static Image SolidBitmap(Color color, int w, int h)
    {
        var bmp = new Bitmap(Math.Max(1, w), Math.Max(1, h));
        using var g = Graphics.FromImage(bmp);
        g.Clear(color);
        return bmp;
    }
}

public static class StatusUx
{
    public static string ShortName(ProtectionStatus status) => status switch
    {
        ProtectionStatus.Blue => "Blue · Join Me",
        ProtectionStatus.Red => "Red · Do Not Disturb",
        _ => "Orange · Ask Me",
    };

    public static string Meaning(ProtectionStatus status) => status switch
    {
        ProtectionStatus.Blue =>
            "Auto-accept vibe: friends can see where you are and join you.",
        ProtectionStatus.Red =>
            "DND: your location stays hidden and invite requests are quieted.",
        _ =>
            "Ask Me: location stays hidden; people can still request to join.",
    };

    public static string ApiHint(ProtectionStatus status) =>
        ProtectionStatusMapping.ToApiStatus(status);
}
