using Velopack;
using VrchatPrivacy.Core.Settings;

namespace VrchatPrivacy.App;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        // Must run before WinForms init so Velopack install/update hooks can exit early.
        VelopackApp.Build().Run();

        ApplicationConfiguration.Initialize();
        AppDataPaths.MigrateFromLegacyIfNeeded();
        Application.Run(new TrayAppContext());
    }
}
