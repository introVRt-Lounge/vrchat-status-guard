using VrchatPrivacy.Core.Settings;

namespace VrchatPrivacy.App;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        AppDataPaths.MigrateFromLegacyIfNeeded();
        Application.Run(new TrayAppContext());
    }
}
