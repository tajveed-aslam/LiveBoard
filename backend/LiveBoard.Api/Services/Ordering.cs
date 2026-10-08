namespace LiveBoard.Api.Services;

/// <summary>Pure list-ordering helpers behind column and card moves (kept separate so they're easy to unit test).</summary>
public static class Ordering
{
    /// <summary>Clamps a requested insertion index into [0, count].</summary>
    public static int ClampIndex(int index, int count) => Math.Max(0, Math.Min(index, count));

    /// <summary>
    /// Moves <paramref name="item"/> within <paramref name="items"/> to <paramref name="toIndex"/> (clamped),
    /// where the index refers to the list after the item has been taken out. Returns a new list.
    /// </summary>
    public static List<T> Move<T>(IReadOnlyList<T> items, T item, int toIndex)
    {
        var list = items.Where(i => !EqualityComparer<T>.Default.Equals(i, item)).ToList();
        if (list.Count == items.Count)
            throw new ArgumentException("Item is not in the list.", nameof(item));
        list.Insert(ClampIndex(toIndex, list.Count), item);
        return list;
    }

    /// <summary>Inserts <paramref name="item"/> at <paramref name="toIndex"/> (clamped). Returns a new list.</summary>
    public static List<T> Insert<T>(IReadOnlyList<T> items, T item, int toIndex)
    {
        var list = items.ToList();
        list.Insert(ClampIndex(toIndex, list.Count), item);
        return list;
    }
}
