using VrchatPrivacy.Core.Activity;
using VrchatPrivacy.Core.Protection;

namespace VrchatPrivacy.Core.Tests;

public class ActivityJournalTests : IDisposable
{
    private readonly string _dir;
    private readonly ActivityJournal _journal;

    public ActivityJournalTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "vsg-activity-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _journal = new ActivityJournal();
        _journal.SetDirectoryOverride(_dir);
    }

    public void Dispose()
    {
        _journal.SetDirectoryOverride(null);
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch
        {
            // best-effort
        }
    }

    [Fact]
    public void RecordStatusApplied_PersistsAndReadsBack()
    {
        _journal.RecordStatusApplied(
            "wrld_aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee",
            "Sleep World",
            "https://example.com/t.png",
            ProtectionStatus.Orange);

        var recent = _journal.ReadRecent();
        Assert.Single(recent);
        Assert.Equal(FeedKind.StatusApplied, recent[0].Kind);
        Assert.Contains("Ask Me", recent[0].Title, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(_journal.FilePath));
    }

    [Fact]
    public void ProtectedWorldEntered_IsUserFacing()
    {
        _journal.RecordProtectedWorldEntered(
            "wrld_aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee",
            "Home",
            null,
            ProtectionStatus.Blue);

        var entry = Assert.Single(_journal.ReadRecent());
        Assert.Equal(FeedKind.ProtectedWorldEntered, entry.Kind);
        Assert.Equal("Entered Home", entry.Title);
        Assert.Contains("Join Me", entry.Detail, StringComparison.OrdinalIgnoreCase);
    }
}
