namespace VrchatPrivacy.App;

public partial class Form1 : Form
{
    private readonly NotifyIcon _tray;

    public Form1()
    {
        InitializeComponent();

        Text = "VRChat Privacy Guard";
        ShowInTaskbar = false;
        WindowState = FormWindowState.Minimized;
        Visible = false;

        _tray = new NotifyIcon
        {
            Text = "VRChat Privacy Guard (scaffold)",
            Visible = true,
            Icon = SystemIcons.Shield,
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add("Status: scaffold only - awaiting login UI", null, (_, _) => { });
        menu.Items.Add("Exit", null, (_, _) =>
        {
            _tray.Visible = false;
            Application.Exit();
        });
        _tray.ContextMenuStrip = menu;

        FormClosing += (_, _) =>
        {
            _tray.Visible = false;
            _tray.Dispose();
        };
    }
}
