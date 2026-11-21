using Velopack;
using VrchatPrivacy.Core.Auth;
using VrchatPrivacy.Core.Logs;
using VrchatPrivacy.Core.Protection;
using VrchatPrivacy.Core.Settings;
using VrchatPrivacy.App.Ui;
using VrchatPrivacy.App.Updates;

namespace VrchatPrivacy.App;

public sealed class TrayAppContext : ApplicationContext
{
    private readonly NotifyIcon _tray;
    private readonly ContextMenuStrip _menu;
    private readonly ToolStripMenuItem _statusItem;
    private readonly ToolStripMenuItem _pauseItem;
    private readonly AppSettingsStore _settingsStore;
    private readonly DpapiCredentialStore _credentialStore;
    private readonly VrchatApiSession _session;
    private readonly VrchatLogDirectoryWatcher _watcher;
    private readonly ProtectionOrchestrator _orchestrator;
    private readonly HubCatalog _catalog;
    private readonly AppUpdateService _updates;
    private readonly SynchronizationContext _ui;
    private DateTime _suppressSingleClickUntilUtc = DateTime.MinValue;
    private int _pendingClickGeneration;
    private bool _hubOpen;
    private ProtectionHubForm? _hubForm;

    public TrayAppContext()
    {
        _ui = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
        _settingsStore = new AppSettingsStore();
        _credentialStore = new DpapiCredentialStore();
        _session = new VrchatApiSession(_credentialStore);
        _watcher = new VrchatLogDirectoryWatcher();
        _orchestrator = new ProtectionOrchestrator(_watcher, _session, _settingsStore);
        _catalog = new HubCatalog(_session);
        _updates = new AppUpdateService();
        _orchestrator.StateChanged += (_, _) => BeginInvoke(UpdateTray);

        _statusItem = new ToolStripMenuItem("Status: starting…") { Enabled = false };
        _pauseItem = new ToolStripMenuItem("Pause protection", null, (_, _) => TogglePause());

        _menu = new ContextMenuStrip();
        _menu.Items.Add(_statusItem);
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add("Login…", null, async (_, _) => await LoginAsync());
        _menu.Items.Add("Apply status now", null, async (_, _) => await ForceApplyAsync());
        _menu.Items.Add(_pauseItem);
        _menu.Items.Add("Protection hub…", null, (_, _) => EditSettings());
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add("Check for updates…", null, async (_, _) => await CheckForUpdatesInteractiveAsync());
        _menu.Items.Add("What's New…", null, async (_, _) => await ShowWhatsNewAsync());
        _menu.Items.Add("About…", null, (_, _) => AboutForm.ShowAbout(null));
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add("Exit", null, (_, _) => Exit());

        _tray = new NotifyIcon
        {
            Text = "VRChat Status Guard",
            // Clone SystemIcons - assigning them live often yields a blank tray glyph.
            Icon = TrayIcons.Default,
            // Keep ContextMenuStrip for right-click. Left single-click is MouseClick;
            // NotifyIcon skips MouseClick on the second half of a double-click (see WinForms source).
            ContextMenuStrip = _menu,
            Visible = true,
        };
        _tray.MouseClick += OnTrayMouseClick;
        _tray.MouseDoubleClick += OnTrayMouseDoubleClick;
        _tray.DoubleClick += OnTrayMouseDoubleClick;

        _ = StartAsync();
    }

    private async void OnTrayMouseClick(object? sender, MouseEventArgs e)
    {
        VrchatPrivacy.Core.Logging.AppLog.Info(
            $"Tray MouseClick button={e.Button} hubOpen={_hubOpen} suppress={DateTime.UtcNow < _suppressSingleClickUntilUtc}");

        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        if (DateTime.UtcNow < _suppressSingleClickUntilUtc || _hubOpen)
        {
            return;
        }

        // NotifyIcon fires MouseClick for a true single-click only. Still wait out the
        // OS double-click window so a trailing quirk cannot race DoubleClick.
        var gen = Interlocked.Increment(ref _pendingClickGeneration);
        var delayMs = Math.Max(SystemInformation.DoubleClickTime + 50, 400);
        try
        {
            await Task.Delay(delayMs).ConfigureAwait(true);
        }
        catch
        {
            return;
        }

        if (gen != _pendingClickGeneration || _hubOpen || DateTime.UtcNow < _suppressSingleClickUntilUtc)
        {
            VrchatPrivacy.Core.Logging.AppLog.Info(
                $"Tray single-click cancelled gen={gen}/{_pendingClickGeneration} hubOpen={_hubOpen}");
            return;
        }

        TogglePause();
    }

    private void OnTrayMouseDoubleClick(object? sender, EventArgs e)
    {
        VrchatPrivacy.Core.Logging.AppLog.Info($"Tray DoubleClick -> hub (open={_hubOpen})");
        Interlocked.Increment(ref _pendingClickGeneration); // cancel any pending pause
        _suppressSingleClickUntilUtc = DateTime.UtcNow.AddMilliseconds(
            Math.Max(SystemInformation.DoubleClickTime * 2, 800));
        EditSettings();
    }

    private async Task StartAsync()
    {
        try
        {
            await _orchestrator.StartAsync().ConfigureAwait(true);
            if (!_session.IsAuthenticated)
            {
                await LoginAsync().ConfigureAwait(true);
            }

            if (_session.IsAuthenticated)
            {
                _catalog.BeginWarmup();
            }

            MaybeShowFirstRunWizard();
            _ = CheckForUpdatesQuietAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to start protection: {ex.Message}", "VRChat Status Guard", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        BeginInvoke(UpdateTray);
    }

    private async Task CheckForUpdatesQuietAsync()
    {
        try
        {
            var update = await _updates.CheckForUpdatesAsync().ConfigureAwait(true);
            if (update is null)
            {
                return;
            }

            var ver = update.TargetFullRelease?.Version?.ToString() ?? "a newer build";
            try
            {
                _tray.ShowBalloonTip(
                    5000,
                    "Update available",
                    $"Status Guard {ver} is ready. Use tray → Check for updates…",
                    ToolTipIcon.Info);
            }
            catch
            {
                // Balloons are best-effort.
            }
        }
        catch (Exception ex)
        {
            VrchatPrivacy.Core.Logging.AppLog.Warn($"Quiet update check failed: {ex.Message}");
        }
    }

    private async Task CheckForUpdatesInteractiveAsync()
    {
        try
        {
            if (!_updates.IsVelopackInstalled)
            {
                var open = MessageBox.Show(
                    "This copy was not installed via Setup.exe (dev run or side-by-side publish).\n\n" +
                    "Open GitHub Releases to download the installer?",
                    "VRChat Status Guard",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information);
                if (open == DialogResult.Yes)
                {
                    AppUpdateService.OpenReleasesPage();
                }

                return;
            }

            var update = await _updates.CheckForUpdatesAsync().ConfigureAwait(true);
            if (update is null)
            {
                MessageBox.Show(
                    $"You're on the latest build ({AppUpdateService.CurrentVersion}).",
                    "VRChat Status Guard",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            var ver = update.TargetFullRelease?.Version?.ToString() ?? "new version";
            var go = MessageBox.Show(
                $"Update to {ver} now? The app will download, restart, and apply.",
                "VRChat Status Guard",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            if (go != DialogResult.Yes)
            {
                return;
            }

            await _updates.DownloadAndApplyAsync(update).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Update check failed: {ex.Message}",
                "VRChat Status Guard",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private async Task ShowWhatsNewAsync()
    {
        try
        {
            var notes = await _updates.GetReleaseNotesAsync().ConfigureAwait(true);
            using var form = new WhatsNewForm(notes);
            form.ShowDialog();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Could not load What's New: {ex.Message}",
                "VRChat Status Guard",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private void MaybeShowFirstRunWizard()
    {
        var settings = _settingsStore.Load();
        if (settings.OnboardingCompleted)
        {
            return;
        }

        using var wizard = new FirstRunWizardForm(settings, _catalog, _tray.Icon);
        if (wizard.ShowDialog() == DialogResult.OK)
        {
            _settingsStore.Save(wizard.Result);
            _orchestrator.ReloadSettings();
        }
        else
        {
            // Closing the wizard without Finish still marks complete so we don't trap them.
            settings.OnboardingCompleted = true;
            _settingsStore.Save(settings);
        }
    }

    private void TogglePause()
    {
        var pause = !_orchestrator.IsPaused;
        _orchestrator.SetPaused(pause);
        UpdateTray();
        VrchatPrivacy.Core.Logging.AppLog.Info(pause ? "Tray: protection paused" : "Tray: protection resumed");
        try
        {
            _tray.ShowBalloonTip(
                2500,
                "Status Guard",
                pause ? "Protection paused. Single-click tray icon to resume." : "Protection resumed.",
                pause ? ToolTipIcon.Warning : ToolTipIcon.Info);
        }
        catch
        {
            // Balloon tips can fail if the shell is busy; icon/tooltip still update.
        }
    }

    private async Task LoginAsync()
    {
        var settings = _settingsStore.Load();
        using var form = new LoginForm(settings.UsernameHint);
        if (form.ShowDialog() != DialogResult.OK)
        {
            return;
        }

        try
        {
            VrchatPrivacy.Core.Logging.AppLog.Info($"Login UI: user={form.Username}");
            var challenge = await _session.BeginLoginAsync(form.Username, form.Password).ConfigureAwait(true);
            if (challenge is not null)
            {
                // Prefer code typed on the first form; otherwise prompt (normal path).
                var otp = !string.IsNullOrWhiteSpace(form.OtpCode)
                    ? form.OtpCode.Trim()
                    : PromptOtpOnUi(challenge.RequiresEmailOtp
                        ? "Enter the email verification code:"
                        : "Enter the authenticator code:");

                if (string.IsNullOrWhiteSpace(otp))
                {
                    VrchatPrivacy.Core.Logging.AppLog.Warn("Login UI: 2FA cancelled or empty");
                    UpdateTray();
                    return;
                }

                if (challenge.RequiresEmailOtp)
                {
                    await _session.CompleteEmailOtpAsync(otp).ConfigureAwait(true);
                }
                else
                {
                    await _session.CompleteTotpAsync(otp).ConfigureAwait(true);
                }
            }

            settings.UsernameHint = form.Username;
            _settingsStore.Save(settings);
            _orchestrator.ReloadSettings();
            _orchestrator.SetPaused(false);
            UpdateTray();
        }
        catch (Exception ex)
        {
            VrchatPrivacy.Core.Logging.AppLog.Error("Login UI failed", ex);
            MessageBox.Show(
                $"Login failed: {ex.GetType().Name}: {ex.Message}\n\nDetails: %LOCALAPPDATA%\\introVRt Lounge\\VRChat Status Guard\\guard.log",
                "VRChat Status Guard",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            UpdateTray();
            return;
        }

        // World re-eval is best-effort; do not surface as a login failure.
        try
        {
            await _orchestrator.StopAsync().ConfigureAwait(true);
            await _orchestrator.StartAsync().ConfigureAwait(true);
            _catalog.BeginWarmup();
            UpdateTray();
            MaybeShowFirstRunWizard();
            UpdateTray();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Logged in, but protection refresh failed: {ex.Message}",
                "VRChat Status Guard",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            UpdateTray();
        }
    }

    private string? PromptOtpOnUi(string message)
    {
        // SDK may invoke the 2FA callback off the UI thread; ShowDialog must run on UI.
        if (SynchronizationContext.Current == _ui)
        {
            return PromptOtpCore(message);
        }

        string? code = null;
        _ui.Send(_ => code = PromptOtpCore(message), null);
        return code;
    }

    private static string? PromptOtpCore(string message)
    {
        using var prompt = new Form
        {
            Width = 360,
            Height = 140,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            Text = "Two-factor authentication",
            StartPosition = FormStartPosition.CenterScreen,
            MinimizeBox = false,
            MaximizeBox = false,
            ShowInTaskbar = true,
        };
        var label = new Label { Left = 12, Top = 12, Width = 320, Text = message };
        var box = new TextBox { Left = 12, Top = 40, Width = 320 };
        var ok = new Button { Text = "OK", Left = 176, Width = 75, Top = 70, DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "Cancel", Left = 257, Width = 75, Top = 70, DialogResult = DialogResult.Cancel };
        prompt.Controls.AddRange(new Control[] { label, box, ok, cancel });
        prompt.AcceptButton = ok;
        prompt.CancelButton = cancel;
        var result = prompt.ShowDialog();
        if (result != DialogResult.OK)
        {
            return null;
        }

        var code = box.Text.Trim();
        return string.IsNullOrWhiteSpace(code) ? null : code;
    }

    private async Task ForceApplyAsync()
    {
        if (!_session.IsAuthenticated)
        {
            await LoginAsync().ConfigureAwait(true);
            if (!_session.IsAuthenticated)
            {
                return;
            }
        }

        try
        {
            var settings = _settingsStore.Load();
            await _session.ApplyProtectionStatusAsync(settings.ProtectionStatus).ConfigureAwait(true);
            await _orchestrator.StopAsync().ConfigureAwait(true);
            await _orchestrator.StartAsync().ConfigureAwait(true);
            UpdateTray();
            MessageBox.Show(
                $"Applied {ProtectionStatusMapping.ToUserLabel(settings.ProtectionStatus)}.",
                "VRChat Status Guard",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Apply failed: {ex.Message}", "VRChat Status Guard", MessageBoxButtons.OK, MessageBoxIcon.Error);
            UpdateTray();
        }
    }

    private void EditSettings()
    {
        if (_hubOpen)
        {
            BringHubToFront();
            return;
        }

        _hubOpen = true;
        Interlocked.Increment(ref _pendingClickGeneration);
        try
        {
            var settings = _settingsStore.Load();
            using var form = new ProtectionHubForm(settings, _catalog);
            _hubForm = form;
            if (form.ShowDialog() != DialogResult.OK)
            {
                return;
            }

            _settingsStore.Save(form.Result);
            _orchestrator.ReloadSettings();
            UpdateTray();
        }
        finally
        {
            _hubForm = null;
            _hubOpen = false;
            _suppressSingleClickUntilUtc = DateTime.UtcNow.AddMilliseconds(500);
        }
    }

    private void BringHubToFront()
    {
        var form = _hubForm;
        if (form is null || form.IsDisposed)
        {
            return;
        }

        VrchatPrivacy.Core.Logging.AppLog.Info("Tray: bringing open hub to front");
        if (form.WindowState == FormWindowState.Minimized)
        {
            form.WindowState = FormWindowState.Normal;
        }

        form.Show();
        form.Activate();
        form.BringToFront();
        // WinForms TopMost bounce helps reclaim focus from other apps.
        form.TopMost = true;
        form.TopMost = false;
        form.Focus();
    }

    private void UpdateTray()
    {
        var state = _orchestrator.State;
        var message = _orchestrator.LastMessage;
        _statusItem.Text = $"Status: {state} — {Trim(message, 60)}";
        _pauseItem.Text = _orchestrator.IsPaused ? "Resume protection" : "Pause protection";
        _tray.Text = Trim($"Status Guard: {state}", 63);
        _tray.Icon = TrayIcons.ForState(state);
        if (!_tray.Visible)
        {
            _tray.Visible = true;
        }
    }

    private static string Trim(string value, int max) =>
        value.Length <= max ? value : value[..(max - 1)] + "…";

    private void BeginInvoke(Action action)
    {
        if (_ui is not null)
        {
            _ui.Post(_ => action(), null);
        }
        else
        {
            action();
        }
    }

    private async void Exit()
    {
        try
        {
            await _orchestrator.StopAsync().ConfigureAwait(true);
        }
        catch
        {
            // ignore shutdown errors
        }

        _tray.Visible = false;
        _tray.Dispose();
        _orchestrator.Dispose();
        _catalog.Dispose();
        _session.Dispose();
        ExitThread();
    }
}
