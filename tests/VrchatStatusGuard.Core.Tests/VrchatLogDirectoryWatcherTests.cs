using VrchatPrivacy.Core.Logs;
using VrchatPrivacy.Core.Settings;

namespace VrchatStatusGuard.Core.Tests;

public class VrchatLogDirectoryWatcherTests
{
    [Fact]
    public async Task GetCurrentWorldAsync_ReadsLatestJoinFromFixtureDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "vrchat-status-guard-logs", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, "output_log_2026-11-20_00-42-24.txt");
            await File.WriteAllTextAsync(path, string.Join(Environment.NewLine, new[]
            {
                "2026.11.20 00:42:39 Debug      -  [Behaviour] Joining wrld_11111111-2222-4333-8444-555555555555:1~region(eu)",
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

    [Fact]
    public async Task StartAsync_DoesNotReplayHistoricalJoins()
    {
        var dir = Path.Combine(Path.GetTempPath(), "vrchat-status-guard-logs", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, "output_log_2026-11-21_18-00-00.txt");
            var older = "wrld_aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee";
            var current = AppSettings.DefaultProtectedWorldId;
            await File.WriteAllTextAsync(path, string.Join(Environment.NewLine, new[]
            {
                $"2026.11.21 18:00:01 Debug      -  [Behaviour] Joining {older}:1~region(eu)",
                $"2026.11.21 18:00:02 Debug      -  [Behaviour] Joining wrld_7a0c95e2-476c-4655-9b4e-7380933c4fd3:1~region(us)",
                $"2026.11.21 18:00:03 Debug      -  [Behaviour] Joining {current}:2~region(us)",
                $"2026.11.21 18:00:04 Debug      -  [Behaviour] Joining {current}:2~region(us)",
            }));

            using var watcher = new VrchatLogDirectoryWatcher(dir);
            var seen = new List<string>();
            watcher.WorldJoined += (_, e) => seen.Add(e.WorldId.Value);

            await watcher.StartAsync();
            await Task.Delay(400); // historical replay would land here if position=0
            Assert.Empty(seen);

            // Orchestrator catch-up path: one explicit current-world read.
            var currentJoin = await watcher.GetCurrentWorldAsync();
            Assert.NotNull(currentJoin);
            Assert.Equal(current, currentJoin!.WorldId.Value);
        }
        finally
        {
            try
            {
                Directory.Delete(dir, recursive: true);
            }
            catch
            {
                // watcher may still hold the file briefly
            }
        }
    }
}
