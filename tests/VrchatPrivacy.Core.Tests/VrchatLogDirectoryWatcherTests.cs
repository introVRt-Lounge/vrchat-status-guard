using VrchatPrivacy.Core.Logs;
using VrchatPrivacy.Core.Settings;

namespace VrchatPrivacy.Core.Tests;

public class VrchatLogDirectoryWatcherTests
{
    [Fact]
    public async Task GetCurrentWorldAsync_ReadsLatestJoinFromFixtureDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "vrchat-privacy-logs", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, "output_log_2026-11-20_00-42-24.txt");
            await File.WriteAllTextAsync(path, string.Join(Environment.NewLine, new[]
            {
                "2026.11.20 00:42:39 Debug      -  [Behaviour] Joining wrld_4432ea9b-729c-46e3-8eaf-846aa0a37fdd:1~region(eu)",
                "2026.11.20 00:43:18 Debug      -  [Behaviour] Joining " + AppSettings.DefaultProtectedWorldId + ":2~region(us)",
            }));

            using var watcher = new VrchatLogDirectoryWatcher(dir);
            var join = await watcher.GetCurrentWorldAsync();
            Assert.NotNull(join);
            Assert.Equal(AppSettings.DefaultProtectedWorldId, join!.WorldId.Value);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
