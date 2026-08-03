namespace Happie.Web.Domain;

/// <summary>Resolves committed saved dish IDs against a list of available dishes, omitting unresolved IDs.</summary>
public static class SavedDishResolver
{
    /// <summary>
    /// Returns the subset of <paramref name="committedIds"/> that exist in <paramref name="availableIds"/>,
    /// preserving the order from <paramref name="committedIds"/>.
    /// IDs in <paramref name="committedIds"/> that do not appear in <paramref name="availableIds"/> are omitted.
    /// </summary>
    public static IReadOnlyList<Guid> Resolve(IReadOnlyList<Guid> committedIds, IReadOnlySet<Guid> availableIds)
    {
        if (committedIds is null || availableIds is null)
            return [];

        return committedIds
            .Where(x => availableIds.Contains(x))
            .ToList();
    }
}
