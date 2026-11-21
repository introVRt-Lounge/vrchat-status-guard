using VrchatPrivacy.App.Ui;
using VrchatPrivacy.Core.Activity;
using DiagProcess = System.Diagnostics.Process;
using DiagProcessStartInfo = System.Diagnostics.ProcessStartInfo;
using VrchatPrivacy.Core.Logging;
using VrchatPrivacy.Core.Protection;

namespace VrchatPrivacy.App;

/// <summary>Basic (pretty) + Debug (raw guard.log) activity views.</summary>
public sealed class ActivityLogForm : Form
{
    private const int WorldThumbW = 160;
    private const int WorldThumbH = 90;
    private const int GroupIconSize = 72;

    private readonly ActivityJournal _journal;
    private readonly HubCatalog? _catalog;
    private readonly Panel _basicHost;
    private readonly FlowLayoutPanel _cards;
    private readonly TextBox _debugBody;
    private readonly Label _pathLabel;
    private readonly RadioButton _basicRadio;
    private readonly RadioButton _debugRadio;
    private readonly CheckBox _follow;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly List<string> _renderedKeys = new();
    private string _lastDebug = string.Empty;
    private int _enrichGeneration;
    private bool _layoutGuard;

    public ActivityLogForm(ActivityJournal journal, HubCatalog? catalog = null)
    {
        _journal = journal;
        _catalog = catalog;

        Text = "Activity - Status Guard";
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimizeBox = true;
        MaximizeBox = true;
        ShowInTaskbar = true;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(680, 460);
        ClientSize = new Size(820, 560);
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
            "What Status Guard did for you - and when.",
            VrcTheme.HeadingFont,
            VrcTheme.TextPrimary));

        var modeRow = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0, 8, 0, 4),
        };
        _basicRadio = new RadioButton
        {
            Text = "Basic",
            Checked = true,
            AutoSize = true,
            ForeColor = VrcTheme.TextPrimary,
            Font = VrcTheme.BodyFont,
            Margin = new Padding(0, 0, 16, 0),
        };
        _debugRadio = new RadioButton
        {
            Text = "Debug",
            AutoSize = true,
            ForeColor = VrcTheme.TextMuted,
            Font = VrcTheme.BodyFont,
        };
        _basicRadio.CheckedChanged += OnModeRadioChanged;
        _debugRadio.CheckedChanged += OnModeRadioChanged;
        modeRow.Controls.Add(_basicRadio);
        modeRow.Controls.Add(_debugRadio);
        header.Controls.Add(modeRow);

        _pathLabel = DpiUtil.AutoLabel(string.Empty, VrcTheme.SmallFont, VrcTheme.TextMuted, maxWidth: 760);
        header.Controls.Add(_pathLabel);
        root.Controls.Add(header, 0, 0);

        var content = new Panel { Dock = DockStyle.Fill, BackColor = VrcTheme.BgDeep };
        _basicHost = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = VrcTheme.BgDeep };
        _cards = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(0, 4, 0, 4),
            BackColor = VrcTheme.BgDeep,
        };
        _basicHost.Controls.Add(_cards);
        _basicHost.Resize += (_, _) =>
        {
            if (_layoutGuard)
            {
                return;
            }

            var w = CardWidth();
            foreach (Control c in _cards.Controls)
            {
                if (c.Width != w)
                {
                    c.Width = w;
                }
            }
        };

        _debugBody = new TextBox
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
            Visible = false,
        };
        content.Controls.Add(_debugBody);
        content.Controls.Add(_basicHost);
        root.Controls.Add(content, 0, 1);

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
        reload.Click += (_, _) =>
        {
            _renderedKeys.Clear();
            _lastDebug = string.Empty;
            RefreshAll(force: true);
        };

        _follow = new CheckBox
        {
            Text = "Live updates",
            Checked = true,
            AutoSize = true,
            ForeColor = VrcTheme.TextPrimary,
            Font = VrcTheme.SmallFont,
            Margin = new Padding(12, 8, 8, 0),
        };

        var openFolder = new Button { Text = "Open folder", AutoSize = true };
        VrcTheme.StyleGhostButton(openFolder);
        openFolder.Click += (_, _) => OpenLogFolder();

        var close = new Button { Text = "Close", DialogResult = DialogResult.OK, AutoSize = true };
        VrcTheme.StylePrimaryButton(close);

        footer.Controls.Add(reload);
        footer.Controls.Add(_follow);
        footer.Controls.Add(openFolder);
        footer.Controls.Add(close);
        root.Controls.Add(footer, 0, 2);

        Controls.Add(root);
        AcceptButton = close;
        CancelButton = close;

        _timer = new System.Windows.Forms.Timer { Interval = 2500 };
        _timer.Tick += (_, _) =>
        {
            if (_follow.Checked && _debugRadio.Checked)
            {
                RefreshDebug();
            }
        };

        _journal.Changed += OnJournalChanged;

        Load += (_, _) =>
        {
            ApplyMode(rebuild: true);
            _timer.Start();
        };
        FormClosed += (_, _) =>
        {
            _timer.Stop();
            _journal.Changed -= OnJournalChanged;
        };
    }

    private void OnModeRadioChanged(object? sender, EventArgs e)
    {
        if (sender is RadioButton { Checked: true })
        {
            ApplyMode(rebuild: true);
        }
    }

    private void OnJournalChanged(object? sender, EventArgs e)
    {
        if (IsDisposed || !_follow.Checked || !_basicRadio.Checked)
        {
            return;
        }

        BeginInvoke(() => RefreshBasic(force: false));
    }

    private void ApplyMode(bool rebuild)
    {
        var basic = _basicRadio.Checked;
        _basicHost.Visible = basic;
        _debugBody.Visible = !basic;
        _basicRadio.ForeColor = basic ? VrcTheme.TextPrimary : VrcTheme.TextMuted;
        _debugRadio.ForeColor = basic ? VrcTheme.TextMuted : VrcTheme.TextPrimary;
        _pathLabel.Text = basic ? _journal.FilePath : AppLog.FilePath;
        if (rebuild)
        {
            RefreshAll(force: true);
        }
    }

    private void RefreshAll(bool force)
    {
        if (_basicRadio.Checked)
        {
            RefreshBasic(force);
        }
        else
        {
            if (force)
            {
                _lastDebug = string.Empty;
            }

            RefreshDebug();
        }

        _pathLabel.Text = _basicRadio.Checked ? _journal.FilePath : AppLog.FilePath;
    }

    private void RefreshDebug()
    {
        var text = AppLog.ReadRecent(500);
        if (string.Equals(text, _lastDebug, StringComparison.Ordinal))
        {
            return;
        }

        var stick = _debugBody.SelectionStart >= Math.Max(0, _debugBody.TextLength - 4);
        _lastDebug = text;
        _debugBody.Text = text;
        if (stick)
        {
            _debugBody.SelectionStart = _debugBody.TextLength;
            _debugBody.ScrollToCaret();
        }
    }

    private void RefreshBasic(bool force)
    {
        var entries = _journal.ReadRecent(60);
        var newestFirst = entries.Reverse().ToList();
        var keys = newestFirst.Select(EntryKey).ToList();

        if (!force && keys.Count == _renderedKeys.Count && keys.SequenceEqual(_renderedKeys))
        {
            return;
        }

        // Incremental: only new items on top (existing cards stay put).
        if (!force &&
            _renderedKeys.Count > 0 &&
            keys.Count > _renderedKeys.Count)
        {
            var suffixStart = keys.Count - _renderedKeys.Count;
            if (keys.Skip(suffixStart).SequenceEqual(_renderedKeys))
            {
                // Remove empty-state label if present.
                foreach (Control c in _cards.Controls.Cast<Control>().ToList())
                {
                    if (c is Label)
                    {
                        _cards.Controls.Remove(c);
                        c.Dispose();
                    }
                }

                var newCards = new List<Control>();
                for (var i = suffixStart - 1; i >= 0; i--)
                {
                    var card = BuildCard(newestFirst[i]);
                    _cards.Controls.Add(card);
                    _cards.Controls.SetChildIndex(card, 0);
                    newCards.Add(card);
                }

                _renderedKeys.Clear();
                _renderedKeys.AddRange(keys);
                var gen = Interlocked.Increment(ref _enrichGeneration);
                _ = FillThumbsInPlaceAsync(gen, newCards);
                return;
            }
        }

        // Full rebuild (first open / Reload / history changed).
        _layoutGuard = true;
        _cards.SuspendLayout();
        try
        {
            _cards.Controls.Clear();
            if (newestFirst.Count == 0)
            {
                _cards.Controls.Add(DpiUtil.AutoLabel(
                    "Nothing yet. Enter a protected world and Status Guard will show what it did here.",
                    VrcTheme.BodyFont,
                    VrcTheme.TextMuted,
                    maxWidth: 640));
            }
            else
            {
                foreach (var entry in newestFirst)
                {
                    _cards.Controls.Add(BuildCard(entry));
                }
            }
        }
        finally
        {
            _cards.ResumeLayout(true);
            _layoutGuard = false;
        }

        _renderedKeys.Clear();
        _renderedKeys.AddRange(keys);
        var g2 = Interlocked.Increment(ref _enrichGeneration);
        _ = FillThumbsInPlaceAsync(g2, _cards.Controls.OfType<Control>().ToList());
    }

    private static string EntryKey(ActivityEntry e) =>
        $"{e.At.UtcTicks}|{e.Kind}|{e.WorldId}|{e.GroupId}|{e.Title}";

    private int CardWidth() => Math.Max(280, _basicHost.ClientSize.Width - 24);

    private Control BuildCard(ActivityEntry entry)
    {
        var cachedWorld = !string.IsNullOrWhiteSpace(entry.WorldId)
            ? _catalog?.Worlds.TryGetCached(entry.WorldId!)
            : null;
        var group = !string.IsNullOrWhiteSpace(entry.GroupId)
            ? _catalog?.FindGroup(entry.GroupId)
            : null;

        var worldName = FirstNonEmpty(entry.WorldName, cachedWorld?.Name);
        var groupName = FirstNonEmpty(entry.GroupName, group?.Name, group?.HeaderLabel);
        var isGroup = entry.Kind == FeedKind.GroupApplied;
        var imageUrl = isGroup
            ? FirstNonEmpty(entry.GroupIconUrl, group?.IconUrl, group?.BannerUrl)
            : FirstNonEmpty(entry.ThumbnailImageUrl, cachedWorld?.ThumbnailImageUrl);

        var titleText = entry.Kind switch
        {
            FeedKind.ProtectedWorldEntered when worldName is not null => $"Entered {worldName}",
            FeedKind.StatusApplied => "Status updated",
            FeedKind.GroupApplied when groupName is not null => $"Repping {groupName}",
            FeedKind.GroupApplied => entry.Title,
            _ => entry.Title,
        };
        var detailText = entry.Kind switch
        {
            FeedKind.ProtectedWorldEntered when entry.Status is not null =>
                "Target status queued for this world.",
            FeedKind.StatusApplied when worldName is not null => $"In {worldName}",
            FeedKind.GroupApplied when worldName is not null => $"In {worldName}",
            _ => entry.Detail,
        };

        var imgW = isGroup ? GroupIconSize : WorldThumbW;
        var imgH = isGroup ? GroupIconSize : WorldThumbH;
        var width = CardWidth();
        var card = new Panel
        {
            Width = width,
            Height = DpiUtil.Scale(this, 96),
            Margin = new Padding(0, 0, 0, 10),
            BackColor = VrcTheme.BgPanel,
            Tag = entry,
        };
        card.Paint += (_, e) =>
        {
            var accent = entry.Status is { } s ? VrcTheme.ForStatus(s) : VrcTheme.Accent;
            using var brush = new SolidBrush(accent);
            e.Graphics.FillRectangle(brush, 0, 0, DpiUtil.Scale(this, 4), card.Height);
            using var pen = new Pen(VrcTheme.Border);
            e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
        };

        var showThumb = entry.Kind is not (FeedKind.Paused or FeedKind.Resumed);
        var pad = DpiUtil.Scale(this, 14);
        var pic = new PictureBox
        {
            Name = "thumb",
            Size = new Size(imgW, imgH),
            Location = new Point(pad, DpiUtil.Scale(this, 12)),
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = VrcTheme.BgElevated,
            Visible = showThumb,
            Tag = new ThumbTag(entry.Kind, entry.WorldId, entry.GroupId, imageUrl, imgW, imgH),
        };
        if (showThumb && !string.IsNullOrWhiteSpace(imageUrl) && _catalog is not null)
        {
            var cached = _catalog.TryGetCachedImage(imageUrl, imgW, imgH);
            if (cached is not null)
            {
                pic.Image = new Bitmap(cached);
            }
        }

        var textLeft = showThumb ? pic.Right + DpiUtil.Scale(this, 12) : pad;
        var textWidth = Math.Max(120, width - textLeft - pad);
        var y = DpiUtil.Scale(this, 10);

        var title = new Label
        {
            Name = "title",
            AutoSize = false,
            Text = titleText,
            Font = VrcTheme.HeadingFont,
            ForeColor = VrcTheme.TextPrimary,
            Location = new Point(textLeft, y),
            Size = new Size(textWidth, DpiUtil.Scale(this, 22)),
        };
        y += DpiUtil.Scale(this, 24);

        // Status chip for status-related rows (not just the word "Blue").
        Panel? chip = null;
        if (entry.Status is { } status &&
            entry.Kind is FeedKind.StatusApplied or FeedKind.ProtectedWorldEntered)
        {
            chip = BuildStatusChip(status);
            chip.Location = new Point(textLeft, y);
            y += chip.Height + DpiUtil.Scale(this, 6);
        }

        var detail = new Label
        {
            Name = "detail",
            AutoSize = false,
            Text = detailText,
            Font = VrcTheme.BodyFont,
            ForeColor = VrcTheme.TextMuted,
            Location = new Point(textLeft, y),
            Size = new Size(textWidth, DpiUtil.Scale(this, 20)),
        };
        y += DpiUtil.Scale(this, 22);

        var time = new Label
        {
            AutoSize = false,
            Text = entry.At.ToLocalTime().ToString("ddd HH:mm:ss"),
            Font = VrcTheme.SmallFont,
            ForeColor = VrcTheme.TextMuted,
            Location = new Point(textLeft, y),
            Size = new Size(textWidth, DpiUtil.Scale(this, 18)),
        };

        card.Controls.Add(pic);
        card.Controls.Add(title);
        if (chip is not null)
        {
            card.Controls.Add(chip);
        }

        card.Controls.Add(detail);
        card.Controls.Add(time);

        var needed = Math.Max(pic.Visible ? pic.Bottom + DpiUtil.Scale(this, 12) : 0, time.Bottom + DpiUtil.Scale(this, 10));
        card.Height = Math.Max(DpiUtil.Scale(this, 88), needed);
        return card;
    }

    private Panel BuildStatusChip(ProtectionStatus status)
    {
        var color = VrcTheme.ForStatus(status);
        var label = StatusUx.ShortName(status);
        var h = DpiUtil.Scale(this, 26);
        var textSize = TextRenderer.MeasureText(label, VrcTheme.SmallFont);
        var width = DpiUtil.Scale(this, 28) + textSize.Width;

        var chip = new Panel
        {
            Name = "statusChip",
            AutoSize = false,
            Size = new Size(width, h),
            BackColor = Color.Transparent,
        };
        chip.Paint += (_, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
            var fill = Color.FromArgb(55, color);
            using var path = Capsule(new Rectangle(0, 0, chip.Width - 1, chip.Height - 1));
            using (var brush = new SolidBrush(fill))
            {
                g.FillPath(brush, path);
            }

            using (var pen = new Pen(color, 1.5f))
            {
                g.DrawPath(pen, path);
            }

            var dotR = DpiUtil.Scale(this, 5);
            var dotX = DpiUtil.Scale(this, 10);
            var dotY = (chip.Height - dotR * 2) / 2;
            using (var brush = new SolidBrush(color))
            {
                g.FillEllipse(brush, dotX, dotY, dotR * 2, dotR * 2);
            }

            var textX = DpiUtil.Scale(this, 24);
            var textY = (chip.Height - textSize.Height) / 2;
            TextRenderer.DrawText(
                g,
                label,
                VrcTheme.SmallFont,
                new Point(textX, textY),
                VrcTheme.TextPrimary,
                TextFormatFlags.NoPadding);
        };

        return chip;
    }

    private static System.Drawing.Drawing2D.GraphicsPath Capsule(Rectangle bounds)
    {
        var path = new System.Drawing.Drawing2D.GraphicsPath();
        var d = Math.Min(bounds.Width, bounds.Height);
        if (d <= 0)
        {
            path.AddRectangle(bounds);
            return path;
        }

        path.AddArc(bounds.X, bounds.Y, d, d, 90, 180);
        path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 180);
        path.CloseFigure();
        return path;
    }

    private async Task FillThumbsInPlaceAsync(int generation, IReadOnlyList<Control> cards)
    {
        if (_catalog is null)
        {
            return;
        }

        foreach (var control in cards)
        {
            if (generation != _enrichGeneration || IsDisposed)
            {
                return;
            }

            if (control is not Panel card)
            {
                continue;
            }

            var pic = card.Controls.Find("thumb", false).OfType<PictureBox>().FirstOrDefault();
            var title = card.Controls.Find("title", false).OfType<Label>().FirstOrDefault();
            var detail = card.Controls.Find("detail", false).OfType<Label>().FirstOrDefault();
            if (pic is null || !pic.Visible || pic.Tag is not ThumbTag tag)
            {
                continue;
            }

            var url = tag.Url;
            if (tag.Kind == FeedKind.GroupApplied)
            {
                if (string.IsNullOrWhiteSpace(url) && !string.IsNullOrWhiteSpace(tag.GroupId))
                {
                    var g = _catalog.FindGroup(tag.GroupId);
                    if (g is null)
                    {
                        try
                        {
                            await _catalog.WarmupAsync(force: false).ConfigureAwait(true);
                        }
                        catch
                        {
                            // ignore
                        }

                        g = _catalog.FindGroup(tag.GroupId);
                    }

                    if (g is not null)
                    {
                        url = g.IconUrl ?? g.BannerUrl;
                        pic.Tag = tag with { Url = url };
                        if (title is not null && !string.IsNullOrWhiteSpace(g.Name))
                        {
                            title.Text = $"Repping {g.Name}";
                        }
                    }
                }
            }
            else if (string.IsNullOrWhiteSpace(url) && !string.IsNullOrWhiteSpace(tag.WorldId))
            {
                try
                {
                    var info = await _catalog.Worlds.GetAsync(tag.WorldId).ConfigureAwait(true);
                    if (generation != _enrichGeneration || IsDisposed)
                    {
                        return;
                    }

                    url = info.ThumbnailImageUrl;
                    pic.Tag = tag with { Url = url };
                    if (card.Tag is ActivityEntry entry)
                    {
                        if (title is not null && entry.Kind == FeedKind.ProtectedWorldEntered)
                        {
                            title.Text = $"Entered {info.Name}";
                        }

                        if (detail is not null && entry.Kind == FeedKind.StatusApplied)
                        {
                            detail.Text = $"In {info.Name}";
                        }
                    }
                }
                catch
                {
                    continue;
                }
            }

            if (string.IsNullOrWhiteSpace(url) || pic.Image is not null)
            {
                continue;
            }

            try
            {
                var img = await _catalog.GetImageAsync(url, tag.Width, tag.Height).ConfigureAwait(true);
                if (generation != _enrichGeneration || IsDisposed || pic.IsDisposed)
                {
                    return;
                }

                pic.Image = new Bitmap(img);
            }
            catch (Exception ex)
            {
                AppLog.Warn($"Activity thumb failed: {ex.Message}");
            }
        }
    }

    private void OpenLogFolder()
    {
        try
        {
            var path = _basicRadio.Checked ? _journal.FilePath : AppLog.FilePath;
            var dir = Path.GetDirectoryName(path);
            if (string.IsNullOrWhiteSpace(dir))
            {
                return;
            }

            DiagProcess.Start(new DiagProcessStartInfo { FileName = dir, UseShellExecute = true });
        }
        catch
        {
            // shell can refuse
        }
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var v in values)
        {
            if (!string.IsNullOrWhiteSpace(v))
            {
                return v;
            }
        }

        return null;
    }

    public static void ShowLog(IWin32Window? owner, ActivityJournal journal, HubCatalog? catalog = null)
    {
        using var form = new ActivityLogForm(journal, catalog);
        if (owner is not null)
        {
            form.ShowDialog(owner);
        }
        else
        {
            form.ShowDialog();
        }
    }

    private sealed record ThumbTag(
        FeedKind Kind,
        string? WorldId,
        string? GroupId,
        string? Url,
        int Width,
        int Height);
}
