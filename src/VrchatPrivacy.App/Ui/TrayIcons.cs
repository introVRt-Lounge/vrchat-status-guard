using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using VrchatPrivacy.Core.Protection;

namespace VrchatPrivacy.App.Ui;

/// <summary>
/// Branded tray glyphs from Assets/status-guard-logo.png.
/// SystemIcons.Shield/Application often render blank or generic after Velopack install;
/// Warning still works - that is why paused looked fine and Working did not (#80).
/// </summary>
internal static class TrayIcons
{
    private static readonly Icon WorkingIcon;
    private static readonly Icon PausedIcon;
    private static readonly Icon FailedIcon;
    private static readonly Icon AwaitingLoginIcon;
    private static readonly Icon IdleIcon;

    static TrayIcons()
    {
        using var logo = LoadLogo();
        WorkingIcon = BuildIcon(logo, Color.FromArgb(255, 120, 40), drawSlash: false);
        PausedIcon = BuildIcon(logo, Color.FromArgb(230, 180, 20), drawSlash: true);
        FailedIcon = BuildIcon(logo, Color.FromArgb(200, 40, 40), drawSlash: false);
        AwaitingLoginIcon = BuildIcon(logo, Color.FromArgb(80, 140, 220), drawSlash: false);
        IdleIcon = BuildIcon(logo, Color.FromArgb(110, 110, 120), drawSlash: false);
    }

    public static Icon ForState(ProtectionAppState state) => state switch
    {
        ProtectionAppState.Working => WorkingIcon,
        ProtectionAppState.Paused => PausedIcon,
        ProtectionAppState.Failed => FailedIcon,
        ProtectionAppState.Disconnected => IdleIcon,
        ProtectionAppState.AwaitingLogin => AwaitingLoginIcon,
        _ => IdleIcon,
    };

    public static Icon Default => WorkingIcon;

    private static Bitmap LoadLogo()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "status-guard-logo.png");
        if (File.Exists(path))
        {
            return new Bitmap(path);
        }

        // Fallback: solid brand tile so tray never goes blank if Assets were omitted.
        var bmp = new Bitmap(64, 64, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.FromArgb(255, 100, 40));
        using var brush = new SolidBrush(Color.White);
        using var font = new Font("Segoe UI", 22, FontStyle.Bold, GraphicsUnit.Pixel);
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
        g.DrawString("SG", font, brush, 8, 16);
        return bmp;
    }

    private static Icon BuildIcon(Image logo, Color accent, bool drawSlash)
    {
        const int size = 32;
        using var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.Transparent);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            using (var accentBrush = new SolidBrush(accent))
            {
                g.FillEllipse(accentBrush, 0, 0, size - 1, size - 1);
            }

            var inset = 4;
            g.DrawImage(logo, new Rectangle(inset, inset, size - inset * 2, size - inset * 2));

            if (drawSlash)
            {
                using var pen = new Pen(Color.FromArgb(240, 20, 20), 3.5f)
                {
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round,
                };
                g.DrawLine(pen, 6, size - 7, size - 7, 6);
            }
        }

        return IconFromBitmap(bmp);
    }

    private static Icon IconFromBitmap(Bitmap bmp)
    {
        var hIcon = bmp.GetHicon();
        try
        {
            using var tmp = Icon.FromHandle(hIcon);
            // Clone so we own the icon after DestroyIcon.
            return (Icon)tmp.Clone();
        }
        finally
        {
            _ = DestroyIcon(hIcon);
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool DestroyIcon(IntPtr handle);
}
