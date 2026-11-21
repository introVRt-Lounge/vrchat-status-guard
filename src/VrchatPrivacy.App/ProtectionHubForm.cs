using VrchatPrivacy.Core.Activity;
using VrchatPrivacy.Core.Groups;
using VrchatPrivacy.Core.Protection;
using VrchatPrivacy.Core.Settings;
using VrchatPrivacy.Core.Worlds;
using VrchatPrivacy.App.Ui;

namespace VrchatPrivacy.App;

/// <summary>Double-click hub: protected worlds first; progressive add flows.</summary>
public sealed class ProtectionHubForm : Form
{
    private readonly AppSettings _settings;
    private readonly HubCatalog _catalog;
    private readonly ActivityJournal _activity;
    private readonly FlowLayoutPanel _protectedGrid = new()
    {
        Dock = DockStyle.Fill,
        AutoScroll = true,
        WrapContents = true,
        FlowDirection = FlowDirection.LeftToRight,
        Padding = new Padding(12),
        BackColor = VrcTheme.BgDeep,
    };
    private readonly Panel _addHost = new() { Dock = DockStyle.Fill, Visible = false, BackColor = VrcTheme.BgDeep };
    private readonly Button _toggleRecent = new() { Text = "＋ Add from recent worlds" };
    private readonly Button _togglePaste = new() { Text = "＋ Add by world ID or URL" };
    private readonly Button _done = new() { Text = "Done" };
    private readonly Label _busy = VrcTheme.MutedLabel("");
    private readonly FlowLayoutPanel _headerStack;
    private readonly Panel _footer;
    private readonly Panel _addChrome;
    private readonly LinkLabel _aboutLink;
    private readonly LinkLabel _logLink;
    private AddWorldFlowControl? _flow;
    private CancellationTokenSource? _cts;

    public ProtectionHubForm(AppSettings settings, HubCatalog catalog, ActivityJournal? activity = null)
    {
        _settings = settings;
        _catalog = catalog;
        _activity = activity ?? new ActivityJournal();

        Text = "Status Guard";
        FormBorderStyle = FormBorderStyle.Sizable;
        StartPosition = FormStartPosition.CenterScreen;
        MinimizeBox = false;
        MaximizeBox = true;
        ShowInTaskbar = true;
        ClientSize = new Size(980, 720);
        MinimumSize = new Size(820, 560);
        VrcTheme.StyleForm(this);

        VrcTheme.StylePrimaryButton(_done);
        VrcTheme.StyleGhostButton(_toggleRecent);
        VrcTheme.StyleGhostButton(_togglePaste);
        _toggleRecent.TextAlign = ContentAlignment.MiddleLeft;
        _togglePaste.TextAlign = ContentAlignment.MiddleLeft;
        _toggleRecent.Padding = DpiUtil.Scale(this, 16, 10, 16, 10);
        _togglePaste.Padding = DpiUtil.Scale(this, 16, 10, 16, 10);

        _aboutLink = new LinkLabel
        {
            Text = "About",
            AutoSize = true,
            Font = VrcTheme.SmallFont,
            LinkColor = VrcTheme.Accent,
            ActiveLinkColor = VrcTheme.TextPrimary,
            VisitedLinkColor = VrcTheme.Accent,
            BackColor = Color.Transparent,
            TabStop = true,
        };
        _aboutLink.LinkClicked += (_, _) => AboutForm.ShowAbout(this, _activity, _catalog);

        _logLink = new LinkLabel
        {
            Text = "Activity log",
            AutoSize = true,
            Font = VrcTheme.SmallFont,
            LinkColor = VrcTheme.Accent,
            ActiveLinkColor = VrcTheme.TextPrimary,
            VisitedLinkColor = VrcTheme.Accent,
            BackColor = Color.Transparent,
            TabStop = true,
        };
        _logLink.LinkClicked += (_, _) => ActivityLogForm.ShowLog(this, _activity, _catalog);

        _headerStack = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = VrcTheme.BgPanel,
            Padding = DpiUtil.Scale(this, 20, 14, 20, 14),
        };
        _headerStack.Paint += (_, e) =>
        {
            using var accent = new SolidBrush(VrcTheme.Accent);
            e.Graphics.FillRectangle(accent, 0, _headerStack.Height - DpiUtil.Scale(this, 3), _headerStack.Width, DpiUtil.Scale(this, 3));
        };
        _headerStack.Controls.Add(DpiUtil.AutoLabel("Protected worlds", VrcTheme.TitleFont, VrcTheme.TextPrimary));
        _headerStack.Controls.Add(DpiUtil.AutoLabel(
            "These worlds get an automatic status (and optional group) when you join.",
            VrcTheme.SmallFont,
            VrcTheme.TextMuted,
            maxWidth: DpiUtil.Scale(this, 700)));
        _headerStack.Controls.Add(_busy);

        _footer = new Panel
        {
            Dock = DockStyle.Bottom,
            BackColor = VrcTheme.BgPanel,
            Padding = DpiUtil.ScalePadding(this, 12),
            Height = DpiUtil.Scale(this, 64),
        };
        _footer.Controls.Add(_done);
        _footer.Resize += (_, _) =>
        {
            _done.Location = new Point(
                _footer.ClientSize.Width - _done.Width - _footer.Padding.Right,
                Math.Max(_footer.Padding.Top, (_footer.ClientSize.Height - _done.Height) / 2));
        };

        _addChrome = new Panel { Dock = DockStyle.Bottom, AutoSize = true, BackColor = VrcTheme.BgPanel };
        var addStack = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = VrcTheme.BgPanel,
        };
        addStack.Controls.Add(_toggleRecent);
        addStack.Controls.Add(_togglePaste);
        _addChrome.Controls.Add(addStack);
        _addChrome.Resize += (_, _) =>
        {
            _toggleRecent.Width = Math.Max(200, _addChrome.ClientSize.Width);
            _togglePaste.Width = Math.Max(200, _addChrome.ClientSize.Width);
        };

        // Fill host last so it sits under chrome when hub is idle; sole-focus shows it alone.
        Controls.Add(_addHost);
        Controls.Add(_protectedGrid);
        Controls.Add(_addChrome);
        Controls.Add(_footer);
        Controls.Add(_headerStack);
        // Top-right links stay visible even in sole-focus (header hidden).
        Controls.Add(_aboutLink);
        Controls.Add(_logLink);
        _aboutLink.BringToFront();
        _logLink.BringToFront();
        Resize += (_, _) => LayoutAboutLink();
        LayoutAboutLink();

        _done.Click += (_, _) =>
        {
            DialogResult = DialogResult.OK;
            Close();
        };
        _toggleRecent.Click += (_, _) => ToggleFlow(allowPaste: false);
        _togglePaste.Click += (_, _) => ToggleFlow(allowPaste: true);

        Shown += async (_, _) =>
        {
            LayoutAboutLink();
            _toggleRecent.Width = Math.Max(200, _addChrome.ClientSize.Width);
            _togglePaste.Width = Math.Max(200, _addChrome.ClientSize.Width);
            _busy.Text = _catalog.IsWarming ? "Refreshing catalog…" : "";
            await _catalog.EnsureFreshForHubAsync().ConfigureAwait(true);
            await ReloadProtectedAsync().ConfigureAwait(true);
        };
        FormClosed += (_, _) =>
        {
            _cts?.Cancel();
            _cts?.Dispose();
        };
    }

    public AppSettings Result => _settings;

    private void LayoutAboutLink()
    {
        var pad = DpiUtil.Scale(this, 16);
        var gap = DpiUtil.Scale(this, 12);
        _aboutLink.Location = new Point(
            Math.Max(pad, ClientSize.Width - _aboutLink.Width - pad),
            pad);
        _logLink.Location = new Point(
            Math.Max(pad, _aboutLink.Left - _logLink.Width - gap),
            pad);
        _aboutLink.BringToFront();
        _logLink.BringToFront();
    }

    private void ToggleFlow(bool allowPaste)
    {
        if (_addHost.Visible && _flow is not null)
        {
            // Prefer Cancel inside the flow; toggles stay hidden while open.
            return;
        }

        ShowFlow(allowPaste ? AddWorldFlowControl.Mode.FromPaste : AddWorldFlowControl.Mode.FromRecent);
    }

    private void ShowFlow(AddWorldFlowControl.Mode mode, WorldInfo? editWorld = null)
    {
        HideFlow();
        _flow = new AddWorldFlowControl(_catalog, mode) { Dock = DockStyle.Fill };
        _flow.Completed += (_, result) =>
        {
            ApplyAdd(result);
            HideFlow();
            _ = AppendOrRefreshCardAsync(result);
        };
        _flow.Cancelled += (_, _) => HideFlow();
        _addHost.Controls.Add(_flow);

        // Sole activity: hide hub chrome so only the flow is on screen.
        _headerStack.Visible = false;
        _protectedGrid.Visible = false;
        _addChrome.Visible = false;
        _footer.Visible = false;
        _addHost.Visible = true;
        _addHost.BringToFront();
        _aboutLink.BringToFront();
        Text = mode == AddWorldFlowControl.Mode.Edit ? "Status Guard · Edit world" :
            mode == AddWorldFlowControl.Mode.FromPaste ? "Status Guard · Add by ID" :
            "Status Guard · Add world";

        if (mode == AddWorldFlowControl.Mode.Edit && editWorld is not null)
        {
            var status = _settings.StatusForWorldId(editWorld.Id);
            var group = _catalog.FindGroup(_settings.TryGetRepresentGroupId(editWorld.Id));
            _flow.Prefill(editWorld, status, group);
        }
    }

    private void HideFlow()
    {
        _addHost.Controls.Clear();
        _flow?.Dispose();
        _flow = null;
        _addHost.Visible = false;
        _headerStack.Visible = true;
        _protectedGrid.Visible = true;
        _addChrome.Visible = true;
        _footer.Visible = true;
        _toggleRecent.Visible = true;
        _togglePaste.Visible = true;
        _toggleRecent.Text = "＋ Add from recent worlds";
        _togglePaste.Text = "＋ Add by world ID or URL";
        Text = "Status Guard";
    }

    private void ApplyAdd(AddWorldFlowControl.Result result)
    {
        if (!_settings.ProtectedWorldIds.Any(id =>
                string.Equals(id, result.World.Id, StringComparison.OrdinalIgnoreCase)))
        {
            _settings.ProtectedWorldIds.Add(result.World.Id);
        }

        _settings.SetStatusForWorld(result.World.Id, result.Status);
        _settings.SetRepresentGroupId(result.World.Id, result.Group?.Id);
        _settings.EnsureDefaultWorldPresent();
    }

    private async Task AppendOrRefreshCardAsync(AddWorldFlowControl.Result result)
    {
        // Remove empty-state label if present.
        foreach (Control c in _protectedGrid.Controls.Cast<Control>().ToList())
        {
            if (c is Label)
            {
                _protectedGrid.Controls.Remove(c);
                c.Dispose();
            }
        }

        // Replace existing card for same world, else append.
        foreach (Control c in _protectedGrid.Controls.Cast<Control>().ToList())
        {
            if (c.Tag is string id && string.Equals(id, result.World.Id, StringComparison.OrdinalIgnoreCase))
            {
                _protectedGrid.Controls.Remove(c);
                c.Dispose();
                break;
            }
        }

        var group = result.Group ?? _catalog.FindGroup(_settings.TryGetRepresentGroupId(result.World.Id));
        var card = await BuildProtectedCardAsync(result.World, result.Status, group).ConfigureAwait(true);
        _protectedGrid.Controls.Add(card);
        card.BringToFront();
    }

    private async Task ReloadProtectedAsync()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        _busy.Text = "Loading protected…";
        _protectedGrid.SuspendLayout();
        _protectedGrid.Controls.Clear();

        try
        {
            var ids = _settings.ProtectedWorldIds
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (ids.Count == 0)
            {
                _protectedGrid.Controls.Add(new Label
                {
                    Text = "No protected worlds yet. Add one from recent or by ID below.",
                    ForeColor = VrcTheme.TextMuted,
                    Font = VrcTheme.BodyFont,
                    AutoSize = true,
                    Margin = new Padding(12),
                });
                return;
            }

            // Resolve worlds from disk cache / API once each; groups from catalog (no per-card refetch).
            foreach (var id in ids)
            {
                ct.ThrowIfCancellationRequested();
                var info = _catalog.Worlds.TryGetCached(id) ?? await _catalog.Worlds.GetAsync(id, ct).ConfigureAwait(true);
                var status = _settings.StatusForWorldId(id);
                var group = _catalog.FindGroup(_settings.TryGetRepresentGroupId(id));
                _protectedGrid.Controls.Add(await BuildProtectedCardAsync(info, status, group).ConfigureAwait(true));
            }
        }
        catch (OperationCanceledException)
        {
            // closed
        }
        finally
        {
            _protectedGrid.ResumeLayout(true);
            if (!IsDisposed)
            {
                _busy.Text = "";
            }
        }
    }

    private async Task<Panel> BuildProtectedCardAsync(
        WorldInfo world,
        ProtectionStatus status,
        GroupInfo? group)
    {
        var accent = VrcTheme.ForStatus(status);
        var cardW = DpiUtil.Scale(this, 300);
        var pad = DpiUtil.Scale(this, 14);
        var thumbW = DpiUtil.Scale(this, 272);
        var thumbH = DpiUtil.Scale(this, 110);
        var nameH = DpiUtil.Scale(this, 28);
        var lineH = DpiUtil.Scale(this, 22);
        var groupIcon = DpiUtil.Scale(this, 40);
        var showGroupCode = group is not null &&
                            !string.Equals(group.HeaderLabel, group.Name, StringComparison.Ordinal);
        var groupRowH = group is null
            ? 0
            : Math.Max(groupIcon, lineH + (showGroupCode ? DpiUtil.Scale(this, 16) : 0)) + DpiUtil.Scale(this, 4);
        var btnH = DpiUtil.Scale(this, 36);
        var gap = DpiUtil.Scale(this, 6);

        var lines = 2; // status + meaning
        var cardH = pad + thumbH + gap + nameH + (lineH * lines) + groupRowH + gap + btnH + pad;

        var card = new Panel
        {
            Width = cardW,
            Height = cardH,
            Margin = DpiUtil.ScalePadding(this, 10),
            BackColor = VrcTheme.BgPanel,
            Tag = world.Id,
        };
        card.Paint += (_, e) =>
        {
            // Full status-colored outline so protected state reads around the whole card.
            var thickness = Math.Max(2, DpiUtil.Scale(this, 3));
            using var pen = new Pen(accent, thickness);
            var inset = thickness / 2f;
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            e.Graphics.DrawRectangle(pen, inset, inset, card.Width - thickness, card.Height - thickness);
        };

        var y = pad;
        var pic = new PictureBox
        {
            Location = new Point(pad, y),
            Size = new Size(thumbW, thumbH),
            SizeMode = PictureBoxSizeMode.Zoom,
            Image = _catalog.TryGetCachedImage(world.ThumbnailImageUrl, 160, 90)
                    ?? VrcTheme.SolidBitmap(VrcTheme.BgElevated, thumbW, thumbH),
        };
        if (_catalog.TryGetCachedImage(world.ThumbnailImageUrl, 160, 90) is null &&
            !string.IsNullOrWhiteSpace(world.ThumbnailImageUrl))
        {
            _ = FillThumbAsync(pic, world.ThumbnailImageUrl);
        }

        y += thumbH + gap;
        card.Controls.Add(pic);
        card.Controls.Add(new Label
        {
            Text = string.IsNullOrWhiteSpace(world.Name) ? world.Id : world.Name,
            Font = VrcTheme.HeadingFont,
            ForeColor = VrcTheme.TextPrimary,
            Location = new Point(pad, y),
            Size = new Size(thumbW, nameH),
        });
        y += nameH;
        card.Controls.Add(new Label
        {
            Text = StatusUx.ShortName(status),
            Font = VrcTheme.SmallFont,
            ForeColor = accent,
            Location = new Point(pad, y),
            Size = new Size(thumbW, lineH),
        });
        y += lineH;
        card.Controls.Add(new Label
        {
            Text = StatusUx.Meaning(status),
            Font = VrcTheme.SmallFont,
            ForeColor = VrcTheme.TextMuted,
            Location = new Point(pad, y),
            Size = new Size(thumbW, lineH),
        });
        y += lineH;
        if (group is not null)
        {
            var iconUrl = group.IconUrl ?? group.BannerUrl;
            var iconBox = new PictureBox
            {
                Location = new Point(pad, y),
                Size = new Size(groupIcon, groupIcon),
                SizeMode = PictureBoxSizeMode.Zoom,
                Image = _catalog.TryGetCachedImage(iconUrl, 72, 72)
                        ?? VrcTheme.SolidBitmap(VrcTheme.BgElevated, groupIcon, groupIcon),
            };
            // Round logo like VRChat group cards.
            using (var path = new System.Drawing.Drawing2D.GraphicsPath())
            {
                path.AddEllipse(0, 0, groupIcon - 1, groupIcon - 1);
                iconBox.Region = new Region(path);
            }

            if (_catalog.TryGetCachedImage(iconUrl, 72, 72) is null &&
                !string.IsNullOrWhiteSpace(iconUrl))
            {
                _ = FillThumbAsync(iconBox, iconUrl, w: 72, h: 72);
            }

            var textLeft = pad + groupIcon + DpiUtil.Scale(this, 8);
            var textTop = showGroupCode
                ? y + Math.Max(0, (groupIcon - (lineH + DpiUtil.Scale(this, 16))) / 2)
                : y + Math.Max(0, (groupIcon - lineH) / 2);
            var groupLabel = new Label
            {
                Text = $"Repping · {group.Name}",
                Font = VrcTheme.SmallFont,
                ForeColor = VrcTheme.Accent,
                Location = new Point(textLeft, textTop),
                Size = new Size(thumbW - groupIcon - DpiUtil.Scale(this, 8), lineH),
            };
            card.Controls.Add(iconBox);
            card.Controls.Add(groupLabel);
            if (showGroupCode)
            {
                card.Controls.Add(new Label
                {
                    Text = group.HeaderLabel,
                    Font = VrcTheme.SmallFont,
                    ForeColor = VrcTheme.TextMuted,
                    Location = new Point(textLeft, groupLabel.Bottom),
                    Size = new Size(groupLabel.Width, DpiUtil.Scale(this, 16)),
                });
            }

            y += groupRowH;
        }

        var btnY = y + gap;
        var edit = new Button { Text = "Edit", Location = new Point(pad, btnY) };
        VrcTheme.StyleGhostButton(edit);
        edit.Click += (_, _) => ShowFlow(AddWorldFlowControl.Mode.Edit, world);

        var remove = new Button
        {
            Text = "Remove",
            Location = new Point(pad + DpiUtil.Scale(this, 96), btnY),
        };
        VrcTheme.StyleGhostButton(remove);
        remove.Click += (_, _) =>
        {
            _settings.ProtectedWorldIds.RemoveAll(x =>
                string.Equals(x, world.Id, StringComparison.OrdinalIgnoreCase));
            _settings.SetStatusForWorld(world.Id, null);
            _settings.SetRepresentGroupId(world.Id, null);
            _settings.EnsureDefaultWorldPresent();
            _protectedGrid.Controls.Remove(card);
            card.Dispose();
        };
        card.Controls.Add(edit);
        card.Controls.Add(remove);

        await Task.CompletedTask.ConfigureAwait(true);
        return card;
    }

    private async Task FillThumbAsync(PictureBox box, string? url, int w = 160, int h = 90)
    {
        try
        {
            var img = await _catalog.GetImageAsync(url, w, h).ConfigureAwait(true);
            if (!box.IsDisposed)
            {
                box.Image = img;
            }
        }
        catch
        {
            // ignore
        }
    }
}
