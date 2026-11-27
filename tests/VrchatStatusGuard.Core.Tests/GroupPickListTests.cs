using VrchatPrivacy.Core.Groups;

namespace VrchatStatusGuard.Core.Tests;

public class GroupPickListTests
{
    [Fact]
    public void Build_IncludesAllCatalogGroups_WhenMoreThanForty()
    {
        var catalog = Enumerable.Range(1, 55)
            .Select(i => new GroupInfo($"grp_{i:D4}", $"Group {i}", null, null, null, null, false))
            .ToList();

        var list = GroupPickList.Build(catalog, selected: null);

        Assert.Equal(55, list.Count);
        Assert.Equal("grp_0055", list[^1].Id);
    }

    [Fact]
    public void Build_PutsSelectedFirst_AndDoesNotDuplicate()
    {
        var catalog = Enumerable.Range(1, 5)
            .Select(i => new GroupInfo($"grp_{i}", $"G{i}", null, null, null, null, false))
            .ToList();
        var selected = catalog[3];

        var list = GroupPickList.Build(catalog, selected);

        Assert.Equal(5, list.Count);
        Assert.Equal(selected.Id, list[0].Id);
        Assert.Equal(1, list.Count(g => g.Id == selected.Id));
    }
}
