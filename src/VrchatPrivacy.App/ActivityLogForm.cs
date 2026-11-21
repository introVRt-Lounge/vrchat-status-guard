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
    private readonly int _thumbW;
    private readonly int _thumbH;
    private string _lastDebug = string.Empty;
    private string _lastBasicFingerprint = string.Empty;
    private int _enrichGeneration;
    private bool _rebuildingBasic;

    public ActivityLogForm(ActivityJournal journal, HubCatalog? catalog = null)
    {
        _journal = journal;
        _catalog = catalog;
        _thumbW = DpiUtil.Scale(this, 112);
        _thumbH = DpiUtil.Scale(this, 63);

        Text = "Activity - VRChat Status Guard";
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
            if (_rebuildingBasic)
            {
                return;
            }

            var w = Math.Max(280, _basicHost.ClientSize.Width - 24);
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
            _lastBasicFingerprint = string.Empty;
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

        // Debug may grow without events; Basic is event-driven (no destructive poll).
        _timer = new System.Windows.Forms.Timer { Interval = 2500 };
        _timer.Tick += (_, _) =>
        {
            if (!_follow.Checked || !_debugRadio.Checked)
            {
                return;
            }

            RefreshDebug();
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
        var fingerprint = Fingerprint(entries);
        if (!force && fingerprint == _lastBasicFingerprint)
        {
            return;
        }

        _lastBasicFingerprint = fingerprint;
        _rebuildingBasic = true;
        _cards.SuspendLayout();
        try
        {
            _cards.Controls.Clear();
            if (entries.Count == 0)
            {
                _cards.Controls.Add(DpiUtil.AutoLabel(
                    "Nothing yet. Enter a protected world and Status Guard will show what it did here.",
                    VrcTheme.BodyFont,
                    VrcTheme.TextMuted,
                    maxWidth: 640));
            }
            else
            {
                foreach (var entry in entries.Reverse())
                {
                    _cards.Controls.Add(BuildCard(entry));
                }
            }
        }
        finally
        {
            _cards.ResumeLayout(true);
            _rebuildingBasic = false;
        }

        var gen = Interlocked.Increment(ref _enrichGeneration);
        _ = FillThumbsInPlaceAsync(gen);
    }

    private static string Fingerprint(IReadOnlyList<ActivityEntry> entries)
    {
        if (entries.Count == 0)
        {
            return "0";
        }

        var last = entries[^1];
        return $"{entries.Count}|{last.At.UtcTicks}|{last.Kind}|{last.WorldId}|{last.Title}";
    }

    private Control BuildCard(ActivityEntry entry)
    {
        var cachedWorld = !string.IsNullOrWhiteSpace(entry.WorldId)
            ? _catalog?.Worlds.TryGetCached(entry.WorldId!)
            : null;
        var worldName = !string.IsNullOrWhiteSpace(entry.WorldName)
            ? entry.WorldName!
            : cachedWorld?.Name;
        var thumbUrl = entry.ThumbnailImageUrl ?? cachedWorld?.ThumbnailImageUrl;
        var titleText = entry.Kind switch
        {
            FeedKind.ProtectedWorldEntered when !string.IsNullOrWhiteSpace(worldName)
                => $"Entered {worldName}",
            FeedKind.StatusApplied when entry.Status is { } st
                => $"Status → {ProtectionStatusMapping.ToUserLabel(st)}",
            _ => entry.Title,
        };
        var detailText = entry.Kind switch
        {
            FeedKind.StatusApplied when !string.IsNullOrWhiteSpace(worldName)
                => $"Protected world: {worldName}",
            FeedKind.GroupApplied when !string.IsNullOrWhiteSpace(worldName)
                => $"{worldName} · group updated",
            _ => entry.Detail,
        };

        var width = Math.Max(280, _basicHost.ClientSize.Width - 24);
        var card = new Panel
        {
            Width = width,
            Height = DpiUtil.Scale(this, 88),
            Margin = new Padding(0, 0, 0, 10),
            BackColor = VrcTheme.BgPanel,
            Padding = DpiUtil.Scale(this, 10, 8, 10, 8),
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
        var pic = new PictureBox
        {
            Name = "thumb",
            Size = new Size(_thumbW, _thumbH),
            Location = new Point(DpiUtil.Scale(this, 14), DpiUtil.Scale(this, 12)),
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = VrcTheme.BgElevated,
            Visible = showThumb,
            Tag = new ThumbTag(entry.WorldId, thumbUrl),
        };
        if (showThumb && !string.IsNullOrWhiteSpace(thumbUrl) && _catalog is not null)
        {
            var cached = _catalog.TryGetCachedImage(thumbUrl, _thumbW, _thumbH);
            if (cached is not null)
            {
                pic.Image = cached;
            }
        }

        var textLeft = showThumb ? pic.Right + DpiUtil.Scale(this, 12) : DpiUtil.Scale(this, 16);
        var textWidth = Math.Max(120, width - textLeft - DpiUtil.Scale(this, 12));
        var when = entry.At.ToLocalTime().ToString("ddd HH:mm:ss");
        var title = new Label
        {
            Name = "title",
            AutoSize = false,
            Text = titleText,
            Font = VrcTheme.HeadingFont,
            ForeColor = VrcTheme.TextPrimary,
            Location = new Point(textLeft, DpiUtil.Scale(this, 10)),
            Size = new Size(textWidth, DpiUtil.Scale(this, 24)),
        };
        var detail = new Label
        {
            Name = "detail",
            AutoSize = false,
            Text = detailText,
            Font = VrcTheme.BodyFont,
            ForeColor = VrcTheme.TextMuted,
            Location = new Point(textLeft, DpiUtil.Scale(this, 36)),
            Size = new Size(textWidth, DpiUtil.Scale(this, 22)),
        };
        var time = new Label
        {
            AutoSize = false,
            Text = when,
            Font = VrcTheme.SmallFont,
            ForeColor = VrcTheme.TextMuted,
            Location = new Point(textLeft, DpiUtil.Scale(this, 60)),
            Size = new Size(textWidth, DpiUtil.Scale(this, 18)),
        };

        card.Controls.Add(pic);
        card.Controls.Add(title);
        card.Controls.Add(detail);
        card.Controls.Add(time);
        return card;
    }

    private async Task FillThumbsInPlaceAsync(int generation)
    {
        if (_catalog is null)
        {
            return;
        }

        // Snapshot cards so we do not rebuild while filling.
        var cards = _cards.Controls.OfType<Panel>().ToList();
        foreach (var card in cards)
        {
            if (generation != _enrichGeneration || IsDisposed)
            {
                return;
            }

            var pic = card.Controls.Find("thumb", false).OfType<PictureBox>().FirstOrDefault();
            var title = card.Controls.Find("title", false).OfType<Label>().FirstOrDefault();
            var detail = card.Controls.Find("detail", false).OfType<Label>().FirstOrDefault();
            if (pic is null || !pic.Visible || pic.Tag is not ThumbTag thumbTag)
            {
                continue;
            }

            var worldId = thumbTag.WorldId;
            var url = thumbTag.Url;

            if (string.IsNullOrWhiteSpace(url) && !string.IsNullOrWhiteSpace(worldId))
            {
                try
                {
                    var info = await _catalog.Worlds.GetAsync(worldId).ConfigureAwait(true);
                    if (generation != _enrichGeneration || IsDisposed)
                    {
                        return;
                    }

                    url = info.ThumbnailImageUrl;
                    pic.Tag = new ThumbTag(worldId, url);
                    if (card.Tag is ActivityEntry entry)
                    {
                        PatchCardText(title, detail, entry, info.Name);
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
                var img = await _catalog.GetImageAsync(url, _thumbW, _thumbH).ConfigureAwait(true);
                if (generation != _enrichGeneration || IsDisposed || pic.IsDisposed)
                {
                    return;
                }

                pic.Image = img;
            }
            catch
            {
                // leave placeholder
            }
        }
    }

    private static void PatchCardText(Label? title, Label? detail, ActivityEntry entry, string? worldName)
    {
        if (string.IsNullOrWhiteSpace(worldName))
        {
            return;
        }

        if (title is not null)
        {
            title.Text = entry.Kind switch
            {
                FeedKind.ProtectedWorldEntered => $"Entered {worldName}",
                FeedKind.StatusApplied when entry.Status is { } st
                    => $"Status → {ProtectionStatusMapping.ToUserLabel(st)}",
                FeedKind.GroupApplied => "Group representation updated",
                _ => title.Text,
            };
        }

        if (detail is not null)
        {
            detail.Text = entry.Kind switch
            {
                FeedKind.StatusApplied => $"Protected world: {worldName}",
                FeedKind.GroupApplied => $"{worldName} · group updated",
                FeedKind.ProtectedWorldEntered when entry.Status is { } st
                    => $"Will set status to {ProtectionStatusMapping.ToUserLabel(st)}.",
                _ => detail.Text,
            };
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

    private sealed record ThumbTag(string? WorldId, string? Url);
}
