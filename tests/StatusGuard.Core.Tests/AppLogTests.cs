using StatusGuard.Core.Logging;

namespace StatusGuard.Core.Tests;

[Collection("AppLogSerial")]
public class AppLogTests : IDisposable
{
    private readonly string _dir;

    public AppLogTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "vsg-applog-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        AppLog.SetDirectoryOverride(_dir);
    }

    public void Dispose()
    {
        AppLog.SetDirectoryOverride(null);
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch
        {
            // best-effort cleanup
        }
    }

    [Fact]
    public void ReadRecent_ReturnsTrailingLines()
    {
        AppLog.Info("alpha");
        AppLog.Info("bravo");
        AppLog.Warn("charlie");

        var text = AppLog.ReadRecent(maxLines: 2);
        Assert.DoesNotContain("alpha", text);
        Assert.Contains("bravo", text);
        Assert.Contains("charlie", text);
        Assert.Contains("[WARN]", text);
    }

    [Fact]
    public void SetDirectoryOverride_WritesAwayFromDefault()
    {
        AppLog.Info("isolated");
        var expected = Path.Combine(_dir, "guard.log");
        Assert.Equal(expected, AppLog.FilePath);
        Assert.True(File.Exists(expected));
        Assert.Contains("isolated", File.ReadAllText(expected));
    }
}
