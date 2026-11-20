using VrchatPrivacy.Core.Auth;
using VrchatPrivacy.Core.Logs;
using VrchatPrivacy.Core.Protection;
using VrchatPrivacy.Core.Settings;

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
    private readonly SynchronizationContext _ui;

    public TrayAppContext()
    {
        _ui = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
        _settingsStore = new AppSettingsStore();
        _credentialStore = new DpapiCredentialStore();
        _session = new VrchatApiSession(_credentialStore);
        _watcher = new VrchatLogDirectoryWatcher();
        _orchestrator = new ProtectionOrchestrator(_watcher, _session, _settingsStore);
        _orchestrator.StateChanged += (_, _) => BeginInvoke(UpdateTray);

        _statusItem = new ToolStripMenuItem("Status: starting…") { Enabled = false };
        _pauseItem = new ToolStripMenuItem("Pause protection", null, (_, _) => TogglePause());

        _menu = new ContextMenuStrip();
        _menu.Items.Add(_statusItem);
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add("Login…", null, async (_, _) => await LoginAsync());
        _menu.Items.Add(_pauseItem);
        _menu.Items.Add("Protected worlds…", null, (_, _) => EditSettings());
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add("Exit", null, (_, _) => Exit());

        _tray = new NotifyIcon
        {
            Text = "VRChat Privacy Guard",
            Visible = true,
            Icon = SystemIcons.Shield,
            ContextMenuStrip = _menu,
        };

        _ = StartAsync();
    }

    private async Task StartAsync()
    {
        try
        {
            await _orchestrator.StartAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to start protection: {ex.Message}", "VRChat Privacy Guard", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        BeginInvoke(UpdateTray);
    }

    private void TogglePause()
    {
        _orchestrator.SetPaused(!_orchestrator.IsPaused);
        UpdateTray();
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
            var challenge = await _session.BeginLoginAsync(form.Username, form.Password).ConfigureAwait(true);
            if (challenge is not null)
            {
                var otp = PromptOtp(challenge.RequiresEmailOtp
                    ? "Enter the email verification code:"
                    : "Enter the authenticator code:");
                if (otp is null)
                {
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

            // Re-evaluate current world after login.
            var current = await _watcher.GetCurrentWorldAsync().ConfigureAwait(true);
            if (current is not null)
            {
                // Trigger via orchestrator start path: temporarily fire by reloading join
                // ProtectionOrchestrator doesn't expose HandleJoin - restart watcher scan:
                await _orchestrator.StopAsync().ConfigureAwait(true);
                await _orchestrator.StartAsync().ConfigureAwait(true);
            }

            UpdateTray();
            MessageBox.Show("Logged in. Protection will apply in protected worlds.", "VRChat Privacy Guard",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Login failed: {ex.Message}", "VRChat Privacy Guard", MessageBoxButtons.OK, MessageBoxIcon.Error);
            UpdateTray();
        }
    }

    private static string? PromptOtp(string message)
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
        };
        var label = new Label { Left = 12, Top = 12, Width = 320, Text = message };
        var box = new TextBox { Left = 12, Top = 40, Width = 320 };
        var ok = new Button { Text = "OK", Left = 176, Width = 75, Top = 70, DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "Cancel", Left = 257, Width = 75, Top = 70, DialogResult = DialogResult.Cancel };
        prompt.Controls.AddRange(new Control[] { label, box, ok, cancel });
        prompt.AcceptButton = ok;
        prompt.CancelButton = cancel;
        return prompt.ShowDialog() == DialogResult.OK ? box.Text.Trim() : null;
    }

    private void EditSettings()
    {
        var settings = _settingsStore.Load();
        using var form = new SettingsForm(settings);
        if (form.ShowDialog() != DialogResult.OK)
        {
            return;
        }

        _settingsStore.Save(form.Result);
        _orchestrator.ReloadSettings();
        UpdateTray();
    }

    private void UpdateTray()
    {
        var state = _orchestrator.State;
        var message = _orchestrator.LastMessage;
        _statusItem.Text = $"Status: {state} — {Trim(message, 60)}";
        _pauseItem.Text = _orchestrator.IsPaused ? "Resume protection" : "Pause protection";
        _tray.Text = Trim($"Privacy Guard: {state}", 63);
        _tray.Icon = state switch
        {
            ProtectionAppState.Working => SystemIcons.Shield,
            ProtectionAppState.Paused => SystemIcons.Warning,
            ProtectionAppState.Failed => SystemIcons.Error,
            ProtectionAppState.Disconnected => SystemIcons.Error,
            ProtectionAppState.AwaitingLogin => SystemIcons.Question,
            _ => SystemIcons.Application,
        };
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
        _session.Dispose();
        ExitThread();
    }
}
