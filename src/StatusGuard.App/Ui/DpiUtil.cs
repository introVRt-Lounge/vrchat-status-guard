namespace StatusGuard.App.Ui;

public static class DpiUtil
{
    public static float ScaleFactor(Control c) =>
        Math.Max(1f, c.DeviceDpi / 96f);

    public static int Scale(Control c, int px) =>
        (int)Math.Round(px * ScaleFactor(c));

    public static Size Scale(Control c, int width, int height) =>
        new(Scale(c, width), Scale(c, height));

    public static Padding ScalePadding(Control c, int all) =>
        new(Scale(c, all));

    public static Padding Scale(Control c, int left, int top, int right, int bottom) =>
        new(Scale(c, left), Scale(c, top), Scale(c, right), Scale(c, bottom));

    /// <summary>Label that grows with its text (no fixed height clipping).</summary>
    public static Label AutoLabel(string text, Font font, Color color, int maxWidth = 0)
    {
        var label = new Label
        {
            Text = text,
            Font = font,
            ForeColor = color,
            AutoSize = true,
            UseCompatibleTextRendering = false,
        };
        if (maxWidth > 0)
        {
            label.MaximumSize = new Size(maxWidth, 0);
        }

        return label;
    }

    public static void MakeAutoButton(Button button)
    {
        button.AutoSize = true;
        button.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        button.Padding = new Padding(14, 8, 14, 8);
        button.MinimumSize = new Size(0, 0);
    }
}
