namespace StatusGuard.Core.Groups;

/// <summary>
/// Builds the ordered group list for the hub picker. Selected first; full catalog after (scroll the UI).
/// </summary>
public static class GroupPickList
{
    public static List<GroupInfo> Build(IReadOnlyList<GroupInfo> catalog, GroupInfo? selected)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        var list = new List<GroupInfo>(catalog.Count + 1);
        if (selected is not null)
        {
            list.Add(selected);
        }

        foreach (var g in catalog)
        {
            if (selected is not null &&
                string.Equals(g.Id, selected.Id, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            list.Add(g);
        }

        return list;
    }
}
