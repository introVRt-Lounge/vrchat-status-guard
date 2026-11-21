using VrchatPrivacy.App.Ui;
using VrchatPrivacy.Core.Activity;
using DiagProcess = System.Diagnostics.Process;
using DiagProcessStartInfo = System.Diagnostics.ProcessStartInfo;
using VrchatPrivacy.Core.Logging;
using VrchatPrivacy.Core.Protection;
using VrchatPrivacy.Core.Worlds;

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
    private string _lastDebug = string.Empty;
    private int _lastBasicCount = -1;

    public ActivityLogForm(ActivityJournal journal, HubCatalog? catalog = null)
    {
        _journal = journal;
        _catalog = catalog;

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
        _basicRadio.CheckedChanged += (_, _) => ApplyMode();
        _debugRadio.CheckedChanged += (_, _) => ApplyMode();
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
            foreach (Control c in _cards.Controls)
            {
                c.Width = Math.Max(280, _basicHost.ClientSize.Width - 24);
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
        reload.Click += (_, _) => RefreshAll(forceScroll: false);

        _follow = new CheckBox
        {
            Text = "Auto-refresh",
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

        _timer = new System.Windows.Forms.Timer { Interval = 2000 };
        _timer.Tick += (_, _) =>
        {
            if (_follow.Checked)
            {
                RefreshAll(forceScroll: true);
            }
        };

        _journal.Changed += OnJournalChanged;

        Load += (_, _) =>
        {
            ApplyMode();
            RefreshAll(forceScroll: true);
            _timer.Start();
        };
        FormClosed += (_, _) =>
        {
            _timer.Stop();
            _journal.Changed -= OnJournalChanged;
        };
    }

    private void OnJournalChanged(object? sender, EventArgs e)
    {
        if (IsDisposed)
        {
            return;
        }

        BeginInvoke(() =>
        {
            if (_follow.Checked && _basicRadio.Checked)
            {
                RefreshBasic(forceScroll: true);
            }
        });
    }

    private void ApplyMode()
    {
        var basic = _basicRadio.Checked;
        _basicHost.Visible = basic;
        _debugBody.Visible = !basic;
        _basicRadio.ForeColor = basic ? VrcTheme.TextPrimary : VrcTheme.TextMuted;
        _debugRadio.ForeColor = basic ? VrcTheme.TextMuted : VrcTheme.TextPrimary;
        _pathLabel.Text = basic ? _journal.FilePath : AppLog.FilePath;
        RefreshAll(forceScroll: true);
    }

    private void RefreshAll(bool forceScroll)
    {
        if (_basicRadio.Checked)
        {
            RefreshBasic(forceScroll);
        }
        else
        {
            RefreshDebug(forceScroll);
        }

        _pathLabel.Text = _basicRadio.Checked ? _journal.FilePath : AppLog.FilePath;
    }

    private void RefreshDebug(bool forceScroll)
    {
        var text = AppLog.ReadRecent(500);
        if (string.Equals(text, _lastDebug, StringComparison.Ordinal))
        {
            return;
        }

        var stick = forceScroll || _debugBody.SelectionStart >= Math.Max(0, _debugBody.TextLength - 4);
        _lastDebug = text;
        _debugBody.Text = text;
        if (stick)
        {
            _debugBody.SelectionStart = _debugBody.TextLength;
            _debugBody.ScrollToCaret();
        }
    }

    private void RefreshBasic(bool forceScroll)
    {
        var entries = _journal.ReadRecent(60);
        if (entries.Count == _lastBasicCount && !forceScroll)
        {
            // still rebuild when names/thumbs may resolve
        }

        var atBottom = forceScroll ||
            (_basicHost.VerticalScroll.Value >= Math.Max(0, _basicHost.VerticalScroll.Maximum - 40));

        _cards.SuspendLayout();
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
            // Newest first for casual reading.
            foreach (var entry in entries.Reverse())
            {
                _cards.Controls.Add(BuildCard(entry));
            }
        }

        _cards.ResumeLayout(true);
        _lastBasicCount = entries.Count;

        if (atBottom && _basicHost.VerticalScroll.Visible)
        {
            _basicHost.AutoScrollPosition = new Point(0, 0); // newest-first: top is newest
        }

        _ = EnrichThumbsAsync(entries);
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
            Tag = entry.WorldId,
        };
        card.Paint += (_, e) =>
        {
            var accent = entry.Status is { } s ? VrcTheme.ForStatus(s) : VrcTheme.Accent;
            using var brush = new SolidBrush(accent);
            e.Graphics.FillRectangle(brush, 0, 0, DpiUtil.Scale(this, 4), card.Height);
            using var pen = new Pen(VrcTheme.Border);
            e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
        };

        var thumbW = DpiUtil.Scale(this, 112);
        var thumbH = DpiUtil.Scale(this, 63);
        var pic = new PictureBox
        {
            Size = new Size(thumbW, thumbH),
            Location = new Point(DpiUtil.Scale(this, 14), DpiUtil.Scale(this, 12)),
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = VrcTheme.BgElevated,
            Tag = thumbUrl,
        };
        if (entry.Kind is FeedKind.Paused or FeedKind.Resumed)
        {
            pic.Visible = false;
        }
        else if (!string.IsNullOrWhiteSpace(thumbUrl) && _catalog is not null)
        {
            var cached = _catalog.TryGetCachedImage(thumbUrl, thumbW, thumbH);
            if (cached is not null)
            {
                pic.Image = cached;
            }
        }

        var textLeft = pic.Visible ? pic.Right + DpiUtil.Scale(this, 12) : DpiUtil.Scale(this, 16);
        var textWidth = Math.Max(120, width - textLeft - DpiUtil.Scale(this, 12));
        var when = entry.At.ToLocalTime().ToString("ddd HH:mm:ss");
        var title = new Label
        {
            AutoSize = false,
            Text = titleText,
            Font = VrcTheme.HeadingFont,
            ForeColor = VrcTheme.TextPrimary,
            Location = new Point(textLeft, DpiUtil.Scale(this, 10)),
            Size = new Size(textWidth, DpiUtil.Scale(this, 24)),
        };
        var detail = new Label
        {
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

    private async Task EnrichThumbsAsync(IReadOnlyList<ActivityEntry> entries)
    {
        if (_catalog is null)
        {
            return;
        }

        foreach (var entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.WorldId))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(entry.WorldName) || string.IsNullOrWhiteSpace(entry.ThumbnailImageUrl))
            {
                try
                {
                    _ = await _catalog.Worlds.GetAsync(entry.WorldId).ConfigureAwait(true);
                    if (IsHandleCreated && !IsDisposed)
                    {
                        RefreshBasic(forceScroll: false);
                        break;
                    }
                }
                catch
                {
                    // offline / rate - ignore
                }
            }
            else if (!string.IsNullOrWhiteSpace(entry.ThumbnailImageUrl))
            {
                try
                {
                    _ = await _catalog.GetImageAsync(entry.ThumbnailImageUrl, 112, 63).ConfigureAwait(true);
                }
                catch
                {
                    // ignore
                }
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
}
