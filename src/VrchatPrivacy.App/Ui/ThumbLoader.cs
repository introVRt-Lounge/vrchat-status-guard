namespace VrchatPrivacy.App.Ui;

public sealed class ThumbLoader : IDisposable
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(20) };
    private readonly Image _placeholder;

    public ThumbLoader(int width, int height, string glyph = "?")
    {
        Width = width;
        Height = height;
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "VrchatStatusGuard/0.1.0 (status-guard@introvrtlounge.com)");
        _placeholder = CreatePlaceholder(width, height, glyph);
    }

    public int Width { get; }
    public int Height { get; }
    public Image Placeholder => _placeholder;

    public async Task<Image> LoadAsync(string? url, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return (Image)_placeholder.Clone();
        }

        try
        {
            await using var stream = await _http.GetStreamAsync(url, ct).ConfigureAwait(false);
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms, ct).ConfigureAwait(false);
            ms.Position = 0;
            using var img = Image.FromStream(ms);
            return new Bitmap(img, new Size(Width, Height));
        }
        catch
        {
            return (Image)_placeholder.Clone();
        }
    }

    public void Dispose()
    {
        _http.Dispose();
        _placeholder.Dispose();
    }

    private static Image CreatePlaceholder(int w, int h, string glyph)
    {
        var bmp = new Bitmap(w, h);
        using var g = Graphics.FromImage(bmp);
        g.Clear(VrcTheme.BgElevated);
        using var pen = new Pen(VrcTheme.Border);
        g.DrawRectangle(pen, 0, 0, w - 1, h - 1);
        TextRenderer.DrawText(
            g,
            glyph,
            VrcTheme.HeadingFont,
            new Rectangle(0, 0, w, h),
            VrcTheme.TextMuted,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        return bmp;
    }
}
