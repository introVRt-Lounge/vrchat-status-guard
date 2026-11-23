using System.Diagnostics;
using VrchatPrivacy.Core.Telemetry;

namespace VrchatPrivacy.App.Telemetry;

/// <summary>UI-side helpers for extreme early-adopter funnel timings.</summary>
internal static class TelemetryUi
{
    private static readonly Stopwatch Session = Stopwatch.StartNew();
    private static long _lastWizardMarkMs;

    public static void WizardStep(string step, string outcome)
    {
        var now = Session.ElapsedMilliseconds;
        var since = now - Interlocked.Read(ref _lastWizardMarkMs);
        Interlocked.Exchange(ref _lastWizardMarkMs, now);
        TelemetryGate.Track(
            "wizard_step",
            ("step", step),
            ("outcome", outcome),
            ("ms_since_prev", since.ToString()),
            ("ms_since_start", now.ToString()));
    }

    public static void Surface(string name, string? source = null) =>
        TelemetryGate.Track(
            "ui_surface",
            ("name", name),
            ("source", source));

    public static void Mark(string name, params (string Key, string? Value)[] props) =>
        TelemetryGate.Track(name, props);
}
