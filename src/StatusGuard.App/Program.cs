using Velopack;
using StatusGuard.Core.Settings;
using StatusGuard.Core.Telemetry;
using StatusGuard.App.Telemetry;

namespace StatusGuard.App;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        // Must run before WinForms init so Velopack install/update hooks can exit early.
        VelopackApp.Build().Run();

        ApplicationConfiguration.Initialize();
        AppDataPaths.MigrateFromLegacyIfNeeded();

        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) =>
        {
            TelemetryUi.Mark(
                "unhandled_ui",
                ("type", e.Exception.GetType().Name),
                ("message", e.Exception.Message));
            _ = TelemetryGate.FlushAsync();
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
            {
                TelemetryUi.Mark(
                    "unhandled",
                    ("type", ex.GetType().Name),
                    ("message", ex.Message));
            }

            _ = TelemetryGate.FlushAsync();
        };

        Application.Run(new TrayAppContext());
    }
}
