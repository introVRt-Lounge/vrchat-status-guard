using VrchatPrivacy.Core.Groups;
using VrchatPrivacy.Core.Protection;
using VrchatPrivacy.Core.Worlds;

namespace VrchatPrivacy.App.Ui;

/// <summary>
/// Progressive add/edit flow: world → status → group → save.
/// Selections stay visible in a summary strip. Paste mode never shows recent worlds.
/// </summary>
public sealed class AddWorldFlowControl : UserControl
{
    public sealed record Result(WorldInfo World, ProtectionStatus Status, GroupInfo? Group);

    public enum Mode
    {
        FromRecent,
        FromPaste,
        Edit,
    }

    private enum Step
    {
        PickWorld,
        Status,
        Group,
        Confirm,
    }

    private readonly HubCatalog _catalog;
    private readonly Mode _mode;
    private readonly FlowLayoutPanel _body = new()
    {
        Dock = DockStyle.Fill,
        AutoScroll = true,
        FlowDirection = FlowDirection.TopDown,
        WrapContents = false,
        Padding = new Padding(8),
    };
    private readonly Panel _summaryHost = new()
    {
        Dock = DockStyle.Top,
        AutoSize = true,
        BackColor = VrcTheme.BgPanel,
        Padding = new Padding(12, 8, 12, 8),
        Visible = false,
    };
    private readonly Label _stepTitle = VrcTheme.Heading("Step");
    private readonly Label _stepHint = VrcTheme.MutedLabel("");
    private readonly Label _loading = VrcTheme.MutedLabel("");
    private readonly Panel _nav = new() { Dock = DockStyle.Bottom, BackColor = VrcTheme.BgPanel };
    private readonly Button _back = new() { Text = "Back" };
    private readonly Button _cancel = new() { Text = "Cancel" };
    private readonly Button _next = new() { Text = "Next" };
    private readonly TextBox _idBox = new()
    {
        Width = 420,
        BackColor = VrcTheme.BgElevated,
        ForeColor = VrcTheme.TextPrimary,
        BorderStyle = BorderStyle.FixedSingle,
    };

    private Step _step;
    private WorldInfo? _world;
    private ProtectionStatus? _status;
    private GroupInfo? _group;
    private bool _skipGroup;

    public AddWorldFlowControl(HubCatalog catalog, Mode mode)
    {
        _catalog = catalog;
        _mode = mode;
        _step = mode == Mode.Edit ? Step.Status : Step.PickWorld;
        Dock = DockStyle.Fill;
        BackColor = VrcTheme.BgDeep;

        VrcTheme.StyleGhostButton(_back);
        VrcTheme.StyleGhostButton(_cancel);
        VrcTheme.StylePrimaryButton(_next);
        _back.Click += (_, _) => GoBack();
        _cancel.Click += (_, _) => Cancelled?.Invoke(this, EventArgs.Empty);
        _next.Click += async (_, _) => await GoNextAsync();

        _nav.Height = DpiUtil.Scale(this, 64);
        _nav.Padding = DpiUtil.ScalePadding(this, 12);
        _nav.Controls.Add(_back);
        _nav.Controls.Add(_cancel);
        _nav.Controls.Add(_next);
        _nav.Resize += (_, _) => LayoutNav();

        var header = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = DpiUtil.Scale(this, 8, 8, 8, 8),
            BackColor = VrcTheme.BgDeep,
        };
        _stepTitle.AutoSize = true;
        _stepHint.AutoSize = true;
        _loading.AutoSize = true;
        _loading.ForeColor = VrcTheme.Accent;
        _stepHint.MaximumSize = new Size(DpiUtil.Scale(this, 700), 0);
        header.Controls.Add(_stepTitle);
        header.Controls.Add(_stepHint);
        header.Controls.Add(_loading);

        Controls.Add(_body);
        Controls.Add(_nav);
        Controls.Add(_summaryHost);
        Controls.Add(header);

        _catalog.Changed += OnCatalogChanged;
        Disposed += (_, _) => _catalog.Changed -= OnCatalogChanged;
    }

    public event EventHandler<Result>? Completed;
    public event EventHandler? Cancelled;

    /// <summary>Prefill for Edit mode (world required; status/group optional).</summary>
    public void Prefill(WorldInfo world, ProtectionStatus? status, GroupInfo? group)
    {
        _world = world;
        _status = status;
        _group = group;
        _skipGroup = group is null;
        _step = Step.Status;
        RenderStep();
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        RenderStep();
        if (_mode == Mode.FromRecent && (!_catalog.HasRecent || _catalog.IsWarming))
        {
            _loading.Text = "Loading recent worlds…";
            _ = EnsureCatalogAsync();
        }
    }

    private async Task EnsureCatalogAsync()
    {
        _loading.Text = "Loading…";
        try
        {
            await _catalog.EnsureFreshForHubAsync().ConfigureAwait(true);
        }
        finally
        {
            if (!IsDisposed)
            {
                _loading.Text = "";
                if (_step == Step.PickWorld)
                {
                    RenderStep();
                }
            }
        }
    }

    private void OnCatalogChanged(object? sender, EventArgs e)
    {
        if (IsDisposed || !IsHandleCreated)
        {
            return;
        }

        BeginInvoke(() =>
        {
            if (_step is Step.PickWorld or Step.Group)
            {
                _loading.Text = _catalog.IsWarming ? "Loading…" : "";
                if (!_catalog.IsWarming)
                {
                    RenderStep();
                }
            }
        });
    }

    private void RenderStep()
    {
        _body.SuspendLayout();
        _body.Controls.Clear();
        RefreshSummaryStrip();

        _back.Enabled = _step != Step.PickWorld && !(_mode == Mode.Edit && _step == Step.Status);
        _next.Visible = true;
        _next.Enabled = true;

        switch (_step)
        {
            case Step.PickWorld:
                if (_mode == Mode.FromPaste)
                {
                    _stepTitle.Text = "Add by world ID or URL";
                    _stepHint.Text = "Paste a world ID or VRChat URL, then Next.";
                    RenderPasteOnly();
                    _next.Text = "Next";
                    _next.Visible = true;
                    _next.Enabled = !string.IsNullOrWhiteSpace(_idBox.Text);
                }
                else
                {
                    _stepTitle.Text = "Pick a recent world";
                    _stepHint.Text = "Click a world to continue.";
                    RenderRecentPick();
                    _next.Visible = false;
                }

                break;
            case Step.Status:
                _stepTitle.Text = "Choose a status";
                _stepHint.Text = "Click a colour to continue.";
                RenderStatusPick();
                _next.Visible = false;
                break;
            case Step.Group:
            {
                var hasGroup = _group is not null && !_skipGroup;
                _stepTitle.Text = "Group representation (optional)";
                _stepHint.Text = hasGroup
                    ? "Selected group is highlighted. Click another, or Next."
                    : "Click a group, or Skip if you do not want one.";
                RenderGroupPick();
                _next.Text = hasGroup ? "Next" : "Skip";
                _next.Visible = true;
                break;
            }
            case Step.Confirm:
                _stepTitle.Text = "Save protection";
                _stepHint.Text = "Confirm world, status, and group.";
                RenderConfirm();
                _next.Text = "Save protection";
                _next.Visible = true;
                _next.Enabled = _world is not null && _status is not null;
                break;
        }

        _body.ResumeLayout(true);
        LayoutNav();
    }

    private void RefreshSummaryStrip()
    {
        _summaryHost.Controls.Clear();
        if (_world is null)
        {
            _summaryHost.Visible = false;
            return;
        }

        _summaryHost.Visible = true;
        var row = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = VrcTheme.BgPanel,
        };

        row.Controls.Add(WorldChip(_world));

        if (_status is { } status)
        {
            row.Controls.Add(Chip("Status", StatusUx.ShortName(status), VrcTheme.ForStatus(status)));
        }
        else if (_step == Step.Status)
        {
            row.Controls.Add(Chip("Status", "choose…", VrcTheme.TextMuted));
        }

        if (_step is Step.Group or Step.Confirm)
        {
            if (_group is not null)
            {
                row.Controls.Add(Chip("Repping", _group.Name, VrcTheme.Accent));
            }
            else if (_step == Step.Confirm && _skipGroup)
            {
                row.Controls.Add(Chip("Group", "no change", VrcTheme.TextMuted));
            }
            else
            {
                row.Controls.Add(Chip("Group", "pick or skip…", VrcTheme.TextMuted));
            }
        }

        _summaryHost.Controls.Add(row);
    }

    /// <summary>WORLD tile with thumbnail and name in one chip (not a separate image).</summary>
    private Panel WorldChip(WorldInfo world)
    {
        var thumbW = DpiUtil.Scale(this, 72);
        var thumbH = DpiUtil.Scale(this, 40);
        var pad = DpiUtil.Scale(this, 8);
        var name = string.IsNullOrWhiteSpace(world.Name) ? world.Id : world.Name;
        var nameSize = TextRenderer.MeasureText(name, VrcTheme.BodyFont, new Size(220, 0), TextFormatFlags.WordBreak);
        var labelH = TextRenderer.MeasureText("WORLD", VrcTheme.SmallFont).Height;
        var textW = Math.Max(DpiUtil.Scale(this, 120), Math.Min(220, nameSize.Width + 4));
        var contentH = Math.Max(thumbH, labelH + 2 + nameSize.Height);
        var panel = new Panel
        {
            Width = pad + thumbW + pad + textW + pad,
            Height = pad + contentH + pad,
            Margin = new Padding(0, 0, 16, 0),
            BackColor = VrcTheme.BgElevated,
        };

        var pic = new PictureBox
        {
            Location = new Point(pad, pad + (contentH - thumbH) / 2),
            Size = new Size(thumbW, thumbH),
            SizeMode = PictureBoxSizeMode.Zoom,
            Image = _catalog.TryGetCachedImage(world.ThumbnailImageUrl, 160, 90)
                    ?? VrcTheme.SolidBitmap(VrcTheme.BgDeep, thumbW, thumbH),
        };
        if (_catalog.TryGetCachedImage(world.ThumbnailImageUrl, 160, 90) is null)
        {
            _ = FillImageLaterAsync(pic, world.ThumbnailImageUrl, 160, 90);
        }

        var textX = pad + thumbW + pad;
        var label = new Label
        {
            Text = "WORLD",
            Font = VrcTheme.SmallFont,
            ForeColor = VrcTheme.TextMuted,
            Location = new Point(textX, pad),
            AutoSize = true,
        };
        var value = new Label
        {
            Text = name,
            Font = VrcTheme.BodyFont,
            ForeColor = VrcTheme.TextPrimary,
            Location = new Point(textX, pad + labelH + 2),
            Size = new Size(textW, nameSize.Height + 2),
        };
        panel.Controls.Add(pic);
        panel.Controls.Add(label);
        panel.Controls.Add(value);
        return panel;
    }

    private static Panel Chip(string label, string value, Color valueColor)
    {
        var panel = new Panel
        {
            AutoSize = true,
            Margin = new Padding(0, 0, 16, 0),
            BackColor = VrcTheme.BgElevated,
            Padding = new Padding(10, 6, 10, 6),
        };
        var stack = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = VrcTheme.BgElevated,
        };
        stack.Controls.Add(new Label
        {
            Text = label.ToUpperInvariant(),
            Font = VrcTheme.SmallFont,
            ForeColor = VrcTheme.TextMuted,
            AutoSize = true,
        });
        stack.Controls.Add(new Label
        {
            Text = value,
            Font = VrcTheme.BodyFont,
            ForeColor = valueColor,
            AutoSize = true,
            MaximumSize = new Size(220, 0),
        });
        panel.Controls.Add(stack);
        return panel;
    }

    private void LayoutNav()
    {
        var yBack = Math.Max(_nav.Padding.Top, (_nav.ClientSize.Height - _back.Height) / 2);
        _back.Location = new Point(_nav.Padding.Left, yBack);
        _cancel.Location = new Point(_back.Right + 8, Math.Max(_nav.Padding.Top, (_nav.ClientSize.Height - _cancel.Height) / 2));
        if (_next.Visible)
        {
            _next.Location = new Point(
                _nav.ClientSize.Width - _next.Width - _nav.Padding.Right,
                Math.Max(_nav.Padding.Top, (_nav.ClientSize.Height - _next.Height) / 2));
        }
    }

    private void RenderPasteOnly()
    {
        var pasteCard = VrcTheme.Card();
        pasteCard.Width = Math.Max(DpiUtil.Scale(this, 520), Width - 40);
        pasteCard.Height = DpiUtil.Scale(this, 100);
        var lbl = VrcTheme.MutedLabel("World ID or URL");
        lbl.Location = new Point(12, 12);
        _idBox.Location = new Point(12, 40);
        _idBox.Width = pasteCard.Width - 24;
        _idBox.TextChanged += (_, _) => _next.Enabled = !string.IsNullOrWhiteSpace(_idBox.Text);
        pasteCard.Controls.Add(lbl);
        pasteCard.Controls.Add(_idBox);
        _body.Controls.Add(pasteCard);
        _body.Controls.Add(VrcTheme.MutedLabel("Example: wrld_… or https://vrchat.com/home/world/wrld_…"));
    }

    private void RenderRecentPick()
    {
        var recent = _catalog.RecentWorlds;
        if (recent.Count == 0)
        {
            _body.Controls.Add(VrcTheme.MutedLabel(
                _catalog.IsWarming ? "Loading recent worlds…" : "No recent worlds yet."));
            return;
        }

        var thumbW = DpiUtil.Scale(this, 160);
        var thumbH = DpiUtil.Scale(this, 90);
        var cardW = DpiUtil.Scale(this, 176);
        var nameH = DpiUtil.Scale(this, 40);
        var pad = DpiUtil.Scale(this, 8);
        var cardH = pad + thumbH + DpiUtil.Scale(this, 6) + nameH + pad;

        var grid = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Width = Math.Max(DpiUtil.Scale(this, 520), Width - 32),
            BackColor = VrcTheme.BgDeep,
        };

        foreach (var world in recent.Take(16))
        {
            var local = world;
            var card = new Panel
            {
                Width = cardW,
                Height = cardH,
                Margin = DpiUtil.ScalePadding(this, 6),
                BackColor = VrcTheme.BgPanel,
                Cursor = Cursors.Hand,
            };
            card.Paint += (_, e) =>
            {
                using var pen = new Pen(VrcTheme.Border);
                e.Graphics.DrawRectangle(pen, 1, 1, card.Width - 3, card.Height - 3);
            };

            var pic = new PictureBox
            {
                Location = new Point(pad, pad),
                Size = new Size(thumbW, thumbH),
                SizeMode = PictureBoxSizeMode.Zoom,
                Image = _catalog.TryGetCachedImage(local.ThumbnailImageUrl, 160, 90)
                        ?? VrcTheme.SolidBitmap(VrcTheme.BgElevated, thumbW, thumbH),
            };
            if (_catalog.TryGetCachedImage(local.ThumbnailImageUrl, 160, 90) is null)
            {
                _ = FillImageLaterAsync(pic, local.ThumbnailImageUrl, 160, 90);
            }

            var name = new Label
            {
                Text = local.Name,
                Font = VrcTheme.SmallFont,
                ForeColor = VrcTheme.TextPrimary,
                Location = new Point(pad, pad + thumbH + DpiUtil.Scale(this, 6)),
                Size = new Size(thumbW, nameH),
            };

            void pick(object? s, EventArgs e)
            {
                _world = local;
                _status = null;
                _group = null;
                _skipGroup = false;
                _step = Step.Status;
                RenderStep();
            }

            card.Click += pick;
            pic.Click += pick;
            name.Click += pick;
            card.Controls.Add(pic);
            card.Controls.Add(name);
            grid.Controls.Add(card);
        }

        _body.Controls.Add(grid);
    }

    private void RenderStatusPick()
    {
        foreach (var status in new[] { ProtectionStatus.Orange, ProtectionStatus.Red, ProtectionStatus.Blue })
        {
            var local = status;
            var selected = _status == local;
            var color = VrcTheme.ForStatus(local);
            var cardW = Math.Max(DpiUtil.Scale(this, 480), Width - DpiUtil.Scale(this, 48));
            var pad = DpiUtil.Scale(this, 16);
            var titleSize = TextRenderer.MeasureText(StatusUx.ShortName(local), VrcTheme.HeadingFont);
            var meaningSize = TextRenderer.MeasureText(
                StatusUx.Meaning(local),
                VrcTheme.SmallFont,
                new Size(cardW - DpiUtil.Scale(this, 48), 0),
                TextFormatFlags.WordBreak);
            var cardH = pad + titleSize.Height + DpiUtil.Scale(this, 8) + meaningSize.Height + pad;

            var card = new Panel
            {
                Width = cardW,
                Height = cardH,
                Margin = DpiUtil.ScalePadding(this, 4),
                BackColor = selected ? VrcTheme.BgHover : VrcTheme.BgPanel,
                Cursor = Cursors.Hand,
            };
            card.Paint += (_, e) =>
            {
                using var brush = new SolidBrush(color);
                e.Graphics.FillRectangle(brush, 0, 0, DpiUtil.Scale(this, 8), card.Height);
                using var pen = new Pen(selected ? color : VrcTheme.Border, selected ? 2 : 1);
                e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
            };

            var title = new Label
            {
                Text = StatusUx.ShortName(local),
                Font = VrcTheme.HeadingFont,
                ForeColor = VrcTheme.TextPrimary,
                Location = new Point(DpiUtil.Scale(this, 24), pad),
                AutoSize = true,
            };
            var meaning = new Label
            {
                Text = StatusUx.Meaning(local),
                Font = VrcTheme.SmallFont,
                ForeColor = VrcTheme.TextMuted,
                Location = new Point(DpiUtil.Scale(this, 24), pad + titleSize.Height + DpiUtil.Scale(this, 8)),
                Size = new Size(cardW - DpiUtil.Scale(this, 48), meaningSize.Height + 4),
            };

            void pick(object? s, EventArgs e)
            {
                _status = local;
                _step = Step.Group;
                RenderStep();
            }

            card.Click += pick;
            title.Click += pick;
            meaning.Click += pick;
            card.Controls.Add(title);
            card.Controls.Add(meaning);
            _body.Controls.Add(card);
        }
    }

    private void RenderGroupPick()
    {
        var groups = BuildGroupListForPick();
        if (groups.Count == 0)
        {
            _body.Controls.Add(VrcTheme.MutedLabel(
                _catalog.IsWarming ? "Loading groups…" : "No groups found — Skip is fine."));
            return;
        }

        var icon = DpiUtil.Scale(this, 56);
        var cardW = DpiUtil.Scale(this, 220);
        var cardH = DpiUtil.Scale(this, 88);
        var accentBar = DpiUtil.Scale(this, 6);
        var grid = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Width = Math.Max(DpiUtil.Scale(this, 520), Width - 32),
            BackColor = VrcTheme.BgDeep,
        };

        Panel? selectedCard = null;
        foreach (var g in groups)
        {
            var local = g;
            var selected = !_skipGroup && _group is not null &&
                           string.Equals(_group.Id, local.Id, StringComparison.OrdinalIgnoreCase);
            var card = new Panel
            {
                Width = cardW,
                Height = cardH,
                Margin = DpiUtil.ScalePadding(this, 6),
                BackColor = selected ? VrcTheme.BgHover : VrcTheme.BgPanel,
                Cursor = Cursors.Hand,
                Tag = local.Id,
            };
            card.Paint += (_, e) =>
            {
                if (selected)
                {
                    using var brush = new SolidBrush(VrcTheme.Accent);
                    e.Graphics.FillRectangle(brush, 0, 0, accentBar, card.Height);
                }

                using var pen = new Pen(selected ? VrcTheme.Accent : VrcTheme.Border, selected ? 2 : 1);
                e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
            };

            var textLeft = DpiUtil.Scale(this, 10) + icon + 8 + (selected ? accentBar / 2 : 0);
            var pic = new PictureBox
            {
                Location = new Point(DpiUtil.Scale(this, 10) + (selected ? accentBar : 0), (cardH - icon) / 2),
                Size = new Size(icon, icon),
                SizeMode = PictureBoxSizeMode.Zoom,
                Image = _catalog.TryGetCachedImage(local.IconUrl ?? local.BannerUrl, 72, 72),
            };
            if (pic.Image is null && !string.IsNullOrWhiteSpace(local.IconUrl ?? local.BannerUrl))
            {
                _ = FillImageLaterAsync(pic, local.IconUrl ?? local.BannerUrl, 72, 72);
            }

            var name = new Label
            {
                Text = local.Name,
                Font = VrcTheme.SmallFont,
                ForeColor = VrcTheme.TextPrimary,
                Location = new Point(textLeft, DpiUtil.Scale(this, 16)),
                Size = new Size(cardW - textLeft - DpiUtil.Scale(this, 12), DpiUtil.Scale(this, 28)),
            };
            var header = new Label
            {
                Text = local.HeaderLabel,
                Font = VrcTheme.SmallFont,
                ForeColor = VrcTheme.Accent,
                Location = new Point(name.Left, name.Bottom + 2),
                Size = new Size(name.Width, DpiUtil.Scale(this, 24)),
            };

            void pick(object? s, EventArgs e)
            {
                _group = local;
                _skipGroup = false;
                _step = Step.Confirm;
                RenderStep();
            }

            card.Click += pick;
            pic.Click += pick;
            name.Click += pick;
            header.Click += pick;
            card.Controls.Add(pic);
            card.Controls.Add(name);
            card.Controls.Add(header);
            grid.Controls.Add(card);
            if (selected)
            {
                selectedCard = card;
            }
        }

        _body.Controls.Add(grid);
        if (selectedCard is not null)
        {
            var target = selectedCard;
            BeginInvoke(() =>
            {
                if (IsDisposed || target.IsDisposed)
                {
                    return;
                }

                _body.ScrollControlIntoView(target);
                target.Focus();
            });
        }
    }

    /// <summary>
    /// Full catalog for pick (scroll the panel). Selected group is listed first.
    /// </summary>
    private List<GroupInfo> BuildGroupListForPick()
    {
        var selected = !_skipGroup ? _group : null;
        return GroupPickList.Build(_catalog.Groups, selected);
    }

    private void RenderConfirm()
    {
        _body.Controls.Add(VrcTheme.MutedLabel("Ready to save. Review the strip above, then Save protection."));
    }

    private async Task FillImageLaterAsync(PictureBox box, string? url, int w, int h)
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

    private void GoBack()
    {
        if (_mode == Mode.Edit && _step == Step.Status)
        {
            return;
        }

        _step = _step switch
        {
            Step.Status => Step.PickWorld,
            Step.Group => Step.Status,
            Step.Confirm => Step.Group,
            _ => Step.PickWorld,
        };
        if (_step == Step.PickWorld)
        {
            _status = null;
            _group = null;
            _skipGroup = false;
            if (_mode != Mode.Edit)
            {
                _world = null;
            }
        }

        RenderStep();
    }

    private async Task GoNextAsync()
    {
        switch (_step)
        {
            case Step.PickWorld when _mode == Mode.FromPaste:
                if (!WorldId.TryParseFromUserInput(_idBox.Text.Trim(), out var parsed))
                {
                    MessageBox.Show(FindForm(), "Paste a valid world ID or VRChat URL.", "Add world",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                _loading.Text = "Looking up world…";
                _next.Enabled = false;
                try
                {
                    _world = await _catalog.Worlds.GetAsync(parsed.Value).ConfigureAwait(true);
                    _catalog.Worlds.Upsert(_world);
                    _status = null;
                    _group = null;
                    _skipGroup = false;
                    _step = Step.Status;
                    RenderStep();
                }
                finally
                {
                    _loading.Text = "";
                    _next.Enabled = true;
                }

                break;
            case Step.Group:
                // Next keeps the current group; Skip only when none was selected.
                if (_group is null)
                {
                    _skipGroup = true;
                }
                else
                {
                    _skipGroup = false;
                }

                _step = Step.Confirm;
                RenderStep();
                break;
            case Step.Confirm:
                if (_world is null || _status is null)
                {
                    return;
                }

                Completed?.Invoke(this, new Result(_world, _status.Value, _skipGroup ? null : _group));
                break;
        }
    }
}
