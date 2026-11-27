using StatusGuard.Core.Auth;
using StatusGuard.Core.Groups;
using StatusGuard.Core.Logs;
using StatusGuard.Core.Protection;
using StatusGuard.Core.Settings;
using StatusGuard.Core.Worlds;

namespace StatusGuard.App;

public sealed class SettingsForm : Form
{
    private const int ThumbW = 128;
    private const int ThumbH = 72;
    private const int GroupIconSize = 48;

    private readonly ListView _recentList = CreateWorldListView();
    private readonly ListView _worldList = CreateWorldListView();
    private readonly ListView _groupList = CreateGroupListView();
    private readonly ImageList _worldThumbs = new()
    {
        ImageSize = new Size(ThumbW, ThumbH),
        ColorDepth = ColorDepth.Depth32Bit,
    };
    private readonly ImageList _groupIcons = new()
    {
        ImageSize = new Size(GroupIconSize, GroupIconSize),
        ColorDepth = ColorDepth.Depth32Bit,
    };
    private readonly TextBox _worldInput = new();
    private readonly Button _add = new() { Text = "Add", Size = new Size(88, 28) };
    private readonly Button _toProtected = new() { Text = "→", Size = new Size(44, 36), Enabled = false };
    private readonly Button _toRecent = new() { Text = "←", Size = new Size(44, 36), Enabled = false };
    private readonly ComboBox _status = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _worldStatus = new() { DropDownStyle = ComboBoxStyle.DropDownList, Enabled = false };
    private readonly Label _worldStatusLabel = new()
    {
        Text = "Status for selected world:",
        AutoSize = true,
        TextAlign = ContentAlignment.MiddleLeft,
    };
    private readonly Button _save = new() { Text = "Save", Size = new Size(96, 30) };
    private readonly Button _cancel = new() { Text = "Cancel", Size = new Size(96, 30), DialogResult = DialogResult.Cancel };
    private readonly Label _busy = new() { Text = "", AutoSize = true, ForeColor = SystemColors.GrayText };
    private readonly Label _recentHint = new()
    {
        AutoSize = false,
        Dock = DockStyle.Top,
        Height = 22,
        TextAlign = ContentAlignment.MiddleLeft,
        ForeColor = SystemColors.GrayText,
        Text = "Recent — pick and move across →",
    };
    private readonly Label _groupHint = new()
    {
        AutoSize = false,
        Dock = DockStyle.Top,
        Height = 22,
        TextAlign = ContentAlignment.MiddleLeft,
        ForeColor = SystemColors.GrayText,
        Text = "Select a protected world, then choose which group to represent there.",
    };
    private readonly PictureBox _groupBanner = new()
    {
        Dock = DockStyle.Right,
        Width = 220,
        SizeMode = PictureBoxSizeMode.Zoom,
        BorderStyle = BorderStyle.FixedSingle,
        BackColor = Color.FromArgb(40, 40, 40),
    };
    private readonly Label _groupBannerCaption = new()
    {
        Dock = DockStyle.Bottom,
        Height = 36,
        TextAlign = ContentAlignment.MiddleLeft,
        ForeColor = SystemColors.GrayText,
        Text = "",
    };

    private readonly AppSettings _settings;
    private readonly IVrchatSession? _session;
    private readonly WorldInfoLookup _worldLookup;
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(20) };
    private readonly Image _worldPlaceholder;
    private readonly Image _groupPlaceholder;
    private readonly Dictionary<string, string> _groupByWorld;
    private readonly Dictionary<string, ProtectionStatus> _statusByWorld;
    private readonly List<GroupInfo> _groups = new();
    private CancellationTokenSource? _loadCts;
    private bool _suppressGroupPersist;
    private bool _suppressWorldStatusPersist;

    public SettingsForm(AppSettings settings, IVrchatSession? session = null)
    {
        _settings = settings;
        _session = session;
        _worldLookup = new WorldInfoLookup();
        _worldPlaceholder = CreatePlaceholder(ThumbW, ThumbH, "?");
        _groupPlaceholder = CreatePlaceholder(GroupIconSize, GroupIconSize, "G");
        _groupByWorld = new Dictionary<string, string>(settings.RepresentGroupByWorld, StringComparer.OrdinalIgnoreCase);
        _statusByWorld = new Dictionary<string, ProtectionStatus>(settings.StatusByWorld, StringComparer.OrdinalIgnoreCase);
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("StatusGuard/0.1.0 (status-guard@introvrtlounge.com)");

        Text = "Protected worlds";
        FormBorderStyle = FormBorderStyle.Sizable;
        StartPosition = FormStartPosition.CenterScreen;
        MinimizeBox = false;
        MaximizeBox = true;
        ShowInTaskbar = true;
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(980, 720);
        MinimumSize = new Size(820, 560);

        _recentList.LargeImageList = _worldThumbs;
        _worldList.LargeImageList = _worldThumbs;
        _groupList.LargeImageList = _groupIcons;
        _groupList.SmallImageList = _groupIcons;

        _status.Items.AddRange(new object[]
        {
            ProtectionStatusMapping.ToUserLabel(ProtectionStatus.Orange),
            ProtectionStatusMapping.ToUserLabel(ProtectionStatus.Red),
            ProtectionStatusMapping.ToUserLabel(ProtectionStatus.Blue),
        });
        _status.SelectedIndex = settings.ProtectionStatus switch
        {
            ProtectionStatus.Red => 1,
            ProtectionStatus.Blue => 2,
            _ => 0,
        };
        PopulateWorldStatusCombo();

        var dialogButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 52,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(12, 10, 12, 12),
            WrapContents = false,
        };
        dialogButtons.Controls.Add(_cancel);
        _save.Margin = new Padding(0, 0, 8, 0);
        dialogButtons.Controls.Add(_save);

        var statusBar = new Panel { Dock = DockStyle.Bottom, Height = 40, Padding = new Padding(16, 4, 16, 4) };
        var statusLabel = new Label { Text = "Default status:", AutoSize = true, Location = new Point(16, 10) };
        statusBar.Controls.Add(statusLabel);
        statusBar.Controls.Add(_status);
        statusBar.Controls.Add(_busy);
        statusBar.Resize += (_, _) =>
        {
            _status.Location = new Point(statusLabel.Right + 8, 6);
            _status.Size = new Size(Math.Max(140, statusBar.ClientSize.Width - _status.Left - 160), 26);
            _busy.Location = new Point(_status.Right + 10, 10);
        };

        var addBar = new Panel { Dock = DockStyle.Bottom, Height = 56, Padding = new Padding(16, 2, 16, 6) };
        var addLabel = new Label
        {
            Text = "Or paste a world ID / VRChat URL:",
            AutoSize = true,
            Location = new Point(16, 2),
        };
        addBar.Controls.Add(addLabel);
        addBar.Controls.Add(_worldInput);
        addBar.Controls.Add(_add);
        addBar.Resize += (_, _) =>
        {
            _add.Location = new Point(addBar.ClientSize.Width - 16 - _add.Width, 22);
            _worldInput.Location = new Point(16, 24);
            _worldInput.Size = new Size(Math.Max(80, _add.Left - 28), 24);
        };

        // Group representation + per-world status (bottom of main area)
        var groupHost = new Panel { Dock = DockStyle.Bottom, Height = 236, Padding = new Padding(16, 4, 16, 4) };
        var worldStatusBar = new Panel { Dock = DockStyle.Top, Height = 32 };
        _worldStatusLabel.Location = new Point(0, 6);
        _worldStatus.Location = new Point(170, 2);
        _worldStatus.Width = 220;
        worldStatusBar.Controls.Add(_worldStatusLabel);
        worldStatusBar.Controls.Add(_worldStatus);
        worldStatusBar.Resize += (_, _) =>
        {
            _worldStatus.Location = new Point(_worldStatusLabel.Right + 8, 2);
            _worldStatus.Width = Math.Max(180, worldStatusBar.ClientSize.Width - _worldStatus.Left - 8);
        };
        var groupArea = new Panel { Dock = DockStyle.Fill };
        var bannerHost = new Panel { Dock = DockStyle.Right, Width = 220 };
        bannerHost.Controls.Add(_groupBanner);
        bannerHost.Controls.Add(_groupBannerCaption);
        groupArea.Controls.Add(_groupList);
        groupArea.Controls.Add(bannerHost);
        groupHost.Controls.Add(groupArea);
        groupHost.Controls.Add(worldStatusBar);
        groupHost.Controls.Add(_groupHint);

        // Side-by-side worlds: Recent | arrows | Protected
        var worldsHost = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Padding = new Padding(12, 8, 12, 4),
        };
        worldsHost.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        worldsHost.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 56f));
        worldsHost.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));

        var recentHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(4) };
        var recentTitle = new Label
        {
            Text = "Recent",
            Dock = DockStyle.Top,
            Height = 22,
            Font = new Font(Font, FontStyle.Bold),
        };
        recentHost.Controls.Add(_recentList);
        recentHost.Controls.Add(_recentHint);
        recentHost.Controls.Add(recentTitle);

        var arrowCol = new Panel { Dock = DockStyle.Fill };
        var arrowStack = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            Anchor = AnchorStyles.None,
        };
        _toProtected.Margin = new Padding(6, 0, 6, 8);
        _toRecent.Margin = new Padding(6, 0, 6, 0);
        arrowStack.Controls.Add(_toProtected);
        arrowStack.Controls.Add(_toRecent);
        arrowCol.Controls.Add(arrowStack);
        arrowCol.Resize += (_, _) =>
        {
            arrowStack.Location = new Point(
                Math.Max(0, (arrowCol.ClientSize.Width - arrowStack.Width) / 2),
                Math.Max(0, (arrowCol.ClientSize.Height - arrowStack.Height) / 2));
        };

        var protectedHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(4) };
        var protectedTitle = new Label
        {
            Text = "Protected",
            Dock = DockStyle.Top,
            Height = 22,
            Font = new Font(Font, FontStyle.Bold),
        };
        var protectedHint = new Label
        {
            Text = "Status + optional group apply when you join these.",
            AutoSize = false,
            Dock = DockStyle.Top,
            Height = 22,
            ForeColor = SystemColors.GrayText,
            TextAlign = ContentAlignment.MiddleLeft,
        };
        protectedHost.Controls.Add(_worldList);
        protectedHost.Controls.Add(protectedHint);
        protectedHost.Controls.Add(protectedTitle);

        worldsHost.Controls.Add(recentHost, 0, 0);
        worldsHost.Controls.Add(arrowCol, 1, 0);
        worldsHost.Controls.Add(protectedHost, 2, 0);

        Controls.Add(worldsHost);
        Controls.Add(groupHost);
        Controls.Add(addBar);
        Controls.Add(statusBar);
        Controls.Add(dialogButtons);

        foreach (var id in settings.ProtectedWorldIds.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            AddWorldItem(_worldList, id, null);
        }

        // Seed "none" option
        EnsureGroupNoneItem();

        Shown += async (_, _) => await LoadListsAsync();

        _add.Click += async (_, _) => await AddWorldFromInputAsync();
        _worldInput.KeyDown += async (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                await AddWorldFromInputAsync();
            }
        };
        _toProtected.Click += async (_, _) => await MoveSelectedRecentToProtectedAsync();
        _toRecent.Click += (_, _) => MoveSelectedProtectedToRecent();
        _recentList.SelectedIndexChanged += (_, _) => UpdateButtons();
        _worldList.SelectedIndexChanged += (_, _) =>
        {
            UpdateButtons();
            SyncGroupSelectionToWorld();
            SyncWorldStatusToSelection();
        };
        _status.SelectedIndexChanged += (_, _) => SyncWorldStatusToSelection();
        _worldStatus.SelectedIndexChanged += (_, _) => OnWorldStatusChanged();
        _recentList.DoubleClick += async (_, _) => await MoveSelectedRecentToProtectedAsync();
        _worldList.DoubleClick += (_, _) => MoveSelectedProtectedToRecent();
        _worldList.KeyDown += (_, e) =>
        {
            if (e.KeyCode is Keys.Delete or Keys.Back)
            {
                e.SuppressKeyPress = true;
                MoveSelectedProtectedToRecent();
            }
        };
        _groupList.SelectedIndexChanged += (_, _) => OnGroupSelectionChanged();
        _save.Click += (_, _) =>
        {
            if (!TryApply())
            {
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        };
        FormClosed += (_, _) =>
        {
            _loadCts?.Cancel();
            _loadCts?.Dispose();
            _worldLookup.Dispose();
            _http.Dispose();
            _worldPlaceholder.Dispose();
            _groupPlaceholder.Dispose();
            _worldThumbs.Dispose();
            _groupIcons.Dispose();
            _groupBanner.Image?.Dispose();
        };

        AcceptButton = _save;
        CancelButton = _cancel;
        UpdateButtons();
        SyncWorldStatusToSelection();
    }

    public AppSettings Result => _settings;

    private void UpdateButtons()
    {
        _toProtected.Enabled = _recentList.SelectedItems.Count > 0;
        _toRecent.Enabled = _worldList.SelectedItems.Count > 0;
        _worldStatus.Enabled = _worldList.SelectedItems.Count > 0;
    }

    private ProtectionStatus SelectedDefaultStatus() => _status.SelectedIndex switch
    {
        1 => ProtectionStatus.Red,
        2 => ProtectionStatus.Blue,
        _ => ProtectionStatus.Orange,
    };

    private void PopulateWorldStatusCombo(bool keepSelection = false)
    {
        var previous = keepSelection && _worldStatus.SelectedIndex >= 0 ? _worldStatus.SelectedIndex : -1;
        var def = SelectedDefaultStatus();
        _worldStatus.Items.Clear();
        _worldStatus.Items.Add($"(use default: {ProtectionStatusMapping.ToUserLabel(def)})");
        _worldStatus.Items.Add(ProtectionStatusMapping.ToUserLabel(ProtectionStatus.Orange));
        _worldStatus.Items.Add(ProtectionStatusMapping.ToUserLabel(ProtectionStatus.Red));
        _worldStatus.Items.Add(ProtectionStatusMapping.ToUserLabel(ProtectionStatus.Blue));
        if (previous >= 0 && previous < _worldStatus.Items.Count)
        {
            _worldStatus.SelectedIndex = previous;
        }
        else if (_worldStatus.Items.Count > 0)
        {
            _worldStatus.SelectedIndex = 0;
        }
    }

    private void SyncWorldStatusToSelection()
    {
        _suppressWorldStatusPersist = true;
        try
        {
            PopulateWorldStatusCombo();
            var worldId = _worldList.SelectedItems.Count > 0 ? GetWorldId(_worldList.SelectedItems[0]) : null;
            if (string.IsNullOrWhiteSpace(worldId))
            {
                _worldStatus.Enabled = false;
                _worldStatus.SelectedIndex = 0;
                return;
            }

            _worldStatus.Enabled = true;
            if (_statusByWorld.TryGetValue(worldId, out var status))
            {
                _worldStatus.SelectedIndex = status switch
                {
                    ProtectionStatus.Orange => 1,
                    ProtectionStatus.Red => 2,
                    ProtectionStatus.Blue => 3,
                    _ => 0,
                };
            }
            else
            {
                _worldStatus.SelectedIndex = 0;
            }
        }
        finally
        {
            _suppressWorldStatusPersist = false;
        }
    }

    private void OnWorldStatusChanged()
    {
        if (_suppressWorldStatusPersist)
        {
            return;
        }

        var worldId = _worldList.SelectedItems.Count > 0 ? GetWorldId(_worldList.SelectedItems[0]) : null;
        if (string.IsNullOrWhiteSpace(worldId))
        {
            return;
        }

        if (_worldStatus.SelectedIndex <= 0)
        {
            _statusByWorld.Remove(worldId);
            return;
        }

        _statusByWorld[worldId] = _worldStatus.SelectedIndex switch
        {
            2 => ProtectionStatus.Red,
            3 => ProtectionStatus.Blue,
            _ => ProtectionStatus.Orange,
        };
    }

    private static ListView CreateWorldListView()
    {
        return new ListView
        {
            Dock = DockStyle.Fill,
            View = View.LargeIcon,
            MultiSelect = false,
            HideSelection = false,
            FullRowSelect = true,
        };
    }

    private static ListView CreateGroupListView()
    {
        var list = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            HideSelection = false,
            MultiSelect = false,
            HeaderStyle = ColumnHeaderStyle.Nonclickable,
        };
        list.Columns.Add("Group", 200);
        list.Columns.Add("Header", 120);
        list.Columns.Add("Id", 260);
        return list;
    }

    private void EnsureGroupNoneItem()
    {
        if (_groupList.Items.Count > 0)
        {
            return;
        }

        EnsureGroupIconKey("__none__");
        _groupList.Items.Add(new ListViewItem(new[] { "(no change)", "—", "" }, "__none__")
        {
            Tag = null,
        });
    }

    private void AddWorldItem(ListView list, string worldId, WorldInfo? info)
    {
        EnsureWorldThumbKey(worldId);
        var name = info?.Name ?? "Loading…";
        var item = new ListViewItem(name, worldId)
        {
            Tag = info ?? (object)worldId,
            ToolTipText = worldId,
        };
        list.Items.Add(item);
    }

    private void EnsureWorldThumbKey(string key)
    {
        if (!_worldThumbs.Images.ContainsKey(key))
        {
            _worldThumbs.Images.Add(key, _worldPlaceholder);
        }
    }

    private void EnsureGroupIconKey(string key)
    {
        if (!_groupIcons.Images.ContainsKey(key))
        {
            _groupIcons.Images.Add(key, _groupPlaceholder);
        }
    }

    private async Task LoadListsAsync()
    {
        _loadCts?.Cancel();
        _loadCts = new CancellationTokenSource();
        var ct = _loadCts.Token;
        _busy.Text = "Loading…";
        try
        {
            foreach (ListViewItem item in _worldList.Items)
            {
                var id = GetWorldId(item);
                if (id is not null)
                {
                    await EnrichWorldItemAsync(item, id, ct).ConfigureAwait(true);
                }
            }

            await LoadRecentAsync(ct).ConfigureAwait(true);
            await LoadGroupsAsync(ct).ConfigureAwait(true);
            SyncGroupSelectionToWorld();
        }
        catch (OperationCanceledException)
        {
            // closed
        }
        finally
        {
            if (!IsDisposed)
            {
                _busy.Text = string.Empty;
            }
        }
    }

    private async Task LoadRecentAsync(CancellationToken ct)
    {
        _recentList.Items.Clear();
        IReadOnlyList<WorldInfo> recent = Array.Empty<WorldInfo>();

        if (_session?.IsAuthenticated == true)
        {
            _recentHint.Text = "Recent from your account — → to protect";
            recent = await _session.GetRecentWorldsAsync(20, ct).ConfigureAwait(true);
        }

        if (recent.Count == 0)
        {
            _recentHint.Text = "Recent from local VRChat logs — → to protect";
            var ids = RecentWorldsFromLogs.GetRecentWorldIds(20);
            var built = new List<WorldInfo>();
            foreach (var id in ids)
            {
                ct.ThrowIfCancellationRequested();
                built.Add(await _worldLookup.GetAsync(id, ct).ConfigureAwait(true));
            }

            recent = built;
        }

        if (recent.Count == 0)
        {
            _recentHint.Text = _session?.IsAuthenticated == true
                ? "No recent worlds. Visit one in VRChat, then reopen."
                : "Log in (tray → Login) or visit worlds in VRChat.";
            return;
        }

        var protectedIds = ProtectedIdSet();
        foreach (var info in recent)
        {
            if (protectedIds.Contains(info.Id))
            {
                continue;
            }

            AddWorldItem(_recentList, info.Id, info);
            var item = _recentList.Items[^1];
            _ = EnrichThumbOnlyAsync(item, info, ct);
        }
    }

    private async Task LoadGroupsAsync(CancellationToken ct)
    {
        _groups.Clear();
        _groupList.Items.Clear();
        EnsureGroupNoneItem();

        IReadOnlyList<GroupInfo> groups = Array.Empty<GroupInfo>();
        if (_session?.IsAuthenticated == true)
        {
            _groupHint.Text = "Groups ordered from VRChat on-disk prefs; names/banners from API.";
            groups = await _session.GetUserGroupsAsync(ct).ConfigureAwait(true);
        }
        else
        {
            // Disk order only (ids) — no names until login.
            var order = LocalVrchatGroups.ReadGroupOrder();
            groups = order.Select(id => new GroupInfo(id, id, null, null, null, null, false)).ToList();
            _groupHint.Text = order.Count > 0
                ? "Logged-out: showing group IDs from VRChat registry order. Login for names/banners."
                : "Log in to load your groups (names, headers, banners).";
        }

        _groups.AddRange(groups);
        foreach (var g in groups)
        {
            EnsureGroupIconKey(g.Id);
            var item = new ListViewItem(new[] { g.Name, g.HeaderLabel, g.Id }, g.Id)
            {
                Tag = g,
            };
            _groupList.Items.Add(item);
            _ = LoadGroupIconAsync(g, ct);
        }

        // Widen columns a bit after populate
        if (_groupList.Columns.Count >= 3)
        {
            _groupList.Columns[0].Width = 220;
            _groupList.Columns[1].Width = 110;
            _groupList.Columns[2].Width = 280;
        }
    }

    private HashSet<string> ProtectedIdSet() =>
        _worldList.Items.Cast<ListViewItem>()
            .Select(GetWorldId)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToHashSet(StringComparer.OrdinalIgnoreCase)!;

    private static string? GetWorldId(ListViewItem item) =>
        item.Tag switch
        {
            WorldInfo info => info.Id,
            string s => s,
            _ => null,
        };

    private async Task EnrichWorldItemAsync(ListViewItem item, string worldId, CancellationToken ct)
    {
        var info = await _worldLookup.GetAsync(worldId, ct).ConfigureAwait(true);
        if (IsDisposed || ct.IsCancellationRequested)
        {
            return;
        }

        item.Text = info.Name;
        item.Tag = info;
        item.ToolTipText = $"{info.Name}\n{info.AuthorName}\n{info.Id}";
        await ApplyWorldThumbnailAsync(item, info, ct).ConfigureAwait(true);
    }

    private async Task EnrichThumbOnlyAsync(ListViewItem item, WorldInfo info, CancellationToken ct)
    {
        try
        {
            await ApplyWorldThumbnailAsync(item, info, ct).ConfigureAwait(true);
        }
        catch
        {
            // ignore
        }
    }

    private async Task ApplyWorldThumbnailAsync(ListViewItem item, WorldInfo info, CancellationToken ct)
    {
        var thumb = await TryDownloadImageAsync(info.ThumbnailImageUrl, ThumbW, ThumbH, ct).ConfigureAwait(true);
        if (IsDisposed || ct.IsCancellationRequested || thumb is null)
        {
            thumb?.Dispose();
            return;
        }

        if (_worldThumbs.Images.ContainsKey(info.Id))
        {
            _worldThumbs.Images.RemoveByKey(info.Id);
        }

        _worldThumbs.Images.Add(info.Id, thumb);
        item.ImageKey = info.Id;
    }

    private async Task LoadGroupIconAsync(GroupInfo group, CancellationToken ct)
    {
        try
        {
            var icon = await TryDownloadImageAsync(group.IconUrl, GroupIconSize, GroupIconSize, ct).ConfigureAwait(true);
            if (IsDisposed || ct.IsCancellationRequested || icon is null)
            {
                icon?.Dispose();
                return;
            }

            if (_groupIcons.Images.ContainsKey(group.Id))
            {
                _groupIcons.Images.RemoveByKey(group.Id);
            }

            _groupIcons.Images.Add(group.Id, icon);
            foreach (ListViewItem item in _groupList.Items)
            {
                if (item.Tag is GroupInfo g && string.Equals(g.Id, group.Id, StringComparison.OrdinalIgnoreCase))
                {
                    item.ImageKey = group.Id;
                    break;
                }
            }
        }
        catch
        {
            // ignore
        }
    }

    private async Task<Image?> TryDownloadImageAsync(string? url, int w, int h, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        try
        {
            await using var stream = await _http.GetStreamAsync(url, ct).ConfigureAwait(false);
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms, ct).ConfigureAwait(false);
            ms.Position = 0;
            using var img = Image.FromStream(ms);
            return new Bitmap(img, new Size(w, h));
        }
        catch
        {
            return null;
        }
    }

    private async Task AddWorldFromInputAsync()
    {
        var trimmed = _worldInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            MessageBox.Show(this, "Paste a world ID or VRChat world URL first.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (!WorldId.TryParseFromUserInput(trimmed, out var world))
        {
            MessageBox.Show(this, $"Not a valid world id/URL:\n{trimmed}", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        await EnsureProtectedAsync(world.Value).ConfigureAwait(true);
        _worldInput.Clear();
    }

    private async Task MoveSelectedRecentToProtectedAsync()
    {
        if (_recentList.SelectedItems.Count == 0)
        {
            return;
        }

        var item = _recentList.SelectedItems[0];
        var id = GetWorldId(item);
        if (string.IsNullOrWhiteSpace(id))
        {
            return;
        }

        var info = item.Tag as WorldInfo;
        _recentList.Items.Remove(item);
        await EnsureProtectedAsync(id, info).ConfigureAwait(true);
        UpdateButtons();
    }

    private void MoveSelectedProtectedToRecent()
    {
        if (_worldList.SelectedItems.Count == 0)
        {
            return;
        }

        var item = _worldList.SelectedItems[0];
        var id = GetWorldId(item);
        if (string.IsNullOrWhiteSpace(id))
        {
            return;
        }

        // Keep default world protectable-but-don't-orphan silently — still allow remove;
        // Save will re-add default if list empty via EnsureDefaultWorldPresent.
        var info = item.Tag as WorldInfo;
        _worldList.Items.Remove(item);
        _groupByWorld.Remove(id);
        _statusByWorld.Remove(id);

        // Put back on recent if not already there
        var already = _recentList.Items.Cast<ListViewItem>()
            .Any(i => string.Equals(GetWorldId(i), id, StringComparison.OrdinalIgnoreCase));
        if (!already)
        {
            AddWorldItem(_recentList, id, info);
            var added = _recentList.Items[^1];
            if (info is not null)
            {
                _ = EnrichThumbOnlyAsync(added, info, CancellationToken.None);
            }
        }

        if (_worldList.Items.Count > 0)
        {
            _worldList.Items[0].Selected = true;
        }
        else
        {
            SyncGroupSelectionToWorld();
        }

        UpdateButtons();
    }

    private async Task EnsureProtectedAsync(string worldId, WorldInfo? known = null)
    {
        foreach (ListViewItem existing in _worldList.Items)
        {
            if (string.Equals(GetWorldId(existing), worldId, StringComparison.OrdinalIgnoreCase))
            {
                existing.Selected = true;
                existing.EnsureVisible();
                UpdateButtons();
                SyncGroupSelectionToWorld();
                return;
            }
        }

        // Remove from recent if present
        foreach (ListViewItem recent in _recentList.Items.Cast<ListViewItem>().ToList())
        {
            if (string.Equals(GetWorldId(recent), worldId, StringComparison.OrdinalIgnoreCase))
            {
                _recentList.Items.Remove(recent);
            }
        }

        AddWorldItem(_worldList, worldId, known);
        var item = _worldList.Items[^1];
        item.Selected = true;
        item.EnsureVisible();
        UpdateButtons();
        SyncGroupSelectionToWorld();
        await EnrichWorldItemAsync(item, worldId, CancellationToken.None).ConfigureAwait(true);
    }

    private void SyncGroupSelectionToWorld()
    {
        var worldId = _worldList.SelectedItems.Count > 0 ? GetWorldId(_worldList.SelectedItems[0]) : null;
        _suppressGroupPersist = true;
        try
        {
            if (string.IsNullOrWhiteSpace(worldId))
            {
                _groupHint.Text = "Select a protected world to choose its representing group.";
                if (_groupList.Items.Count > 0)
                {
                    _groupList.Items[0].Selected = true;
                }

                ClearBanner();
                return;
            }

            _groupHint.Text = $"Representing group for {_worldList.SelectedItems[0].Text}:";
            _groupByWorld.TryGetValue(worldId, out var selectedGroupId);

            ListViewItem? match = null;
            foreach (ListViewItem item in _groupList.Items)
            {
                if (item.Tag is GroupInfo g &&
                    string.Equals(g.Id, selectedGroupId, StringComparison.OrdinalIgnoreCase))
                {
                    match = item;
                    break;
                }
            }

            match ??= _groupList.Items.Count > 0 ? _groupList.Items[0] : null;
            if (match is not null)
            {
                match.Selected = true;
                match.EnsureVisible();
            }

            _ = UpdateBannerAsync(match?.Tag as GroupInfo);
        }
        finally
        {
            _suppressGroupPersist = false;
        }
    }

    private void OnGroupSelectionChanged()
    {
        if (_groupList.SelectedItems.Count > 0)
        {
            _ = UpdateBannerAsync(_groupList.SelectedItems[0].Tag as GroupInfo);
        }
        else
        {
            ClearBanner();
        }

        if (_suppressGroupPersist)
        {
            return;
        }

        var worldId = _worldList.SelectedItems.Count > 0 ? GetWorldId(_worldList.SelectedItems[0]) : null;
        if (string.IsNullOrWhiteSpace(worldId) || _groupList.SelectedItems.Count == 0)
        {
            return;
        }

        if (_groupList.SelectedItems[0].Tag is GroupInfo g)
        {
            _groupByWorld[worldId] = g.Id;
        }
        else
        {
            _groupByWorld.Remove(worldId);
        }
    }

    private async Task UpdateBannerAsync(GroupInfo? group)
    {
        if (group is null)
        {
            ClearBanner();
            return;
        }

        _groupBannerCaption.Text = $"{group.Name}\n{group.HeaderLabel}";
        var banner = await TryDownloadImageAsync(group.BannerUrl ?? group.IconUrl, 220, 120, CancellationToken.None)
            .ConfigureAwait(true);
        if (IsDisposed)
        {
            banner?.Dispose();
            return;
        }

        var old = _groupBanner.Image;
        _groupBanner.Image = banner;
        old?.Dispose();
        if (banner is null)
        {
            _groupBannerCaption.Text = $"{group.Name}\n{group.HeaderLabel} (no banner URL)";
        }
    }

    private void ClearBanner()
    {
        var old = _groupBanner.Image;
        _groupBanner.Image = null;
        old?.Dispose();
        _groupBannerCaption.Text = "";
    }

    private bool TryApply()
    {
        var ids = _worldList.Items.Cast<ListViewItem>()
            .Select(GetWorldId)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (ids.Count == 0)
        {
            ids.Add(AppSettings.DefaultProtectedWorldId);
        }

        _settings.ProtectedWorldIds = ids!;
        _settings.ProtectionStatus = SelectedDefaultStatus();
        _settings.RepresentGroupByWorld = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in _groupByWorld)
        {
            if (ids.Contains(kv.Key, StringComparer.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(kv.Value))
            {
                _settings.RepresentGroupByWorld[kv.Key] = kv.Value;
            }
        }

        _settings.StatusByWorld = new Dictionary<string, ProtectionStatus>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in _statusByWorld)
        {
            if (ids.Contains(kv.Key, StringComparer.OrdinalIgnoreCase))
            {
                _settings.StatusByWorld[kv.Key] = kv.Value;
            }
        }

        _settings.EnsureDefaultWorldPresent();
        return true;
    }

    private static Image CreatePlaceholder(int w, int h, string glyph)
    {
        var bmp = new Bitmap(w, h);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.FromArgb(230, 230, 230));
        using var pen = new Pen(Color.Gray);
        g.DrawRectangle(pen, 0, 0, w - 1, h - 1);
        TextRenderer.DrawText(
            g,
            glyph,
            SystemFonts.DefaultFont,
            new Rectangle(0, 0, w, h),
            Color.Gray,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        return bmp;
    }
}
