namespace StageManager.Services;

internal sealed class IconBadgeRegistry<TBadge>
    where TBadge : class
{
    private readonly Dictionary<nint, TBadge> _badges = [];

    internal int Count => _badges.Count;

    internal bool TryGetValue(nint handle, out TBadge badge) =>
        _badges.TryGetValue(handle, out badge!);

    internal void Add(nint handle, TBadge badge) => _badges.Add(handle, badge);

    internal void RemoveObsolete(IReadOnlyCollection<nint> visibleHandles, Action<TBadge> release)
    {
        foreach (var handle in _badges.Keys.Where(handle => !visibleHandles.Contains(handle)).ToArray())
        {
            if (_badges.Remove(handle, out var badge))
            {
                release(badge);
            }
        }
    }

    internal void Clear(Action<TBadge> release)
    {
        foreach (var badge in _badges.Values)
        {
            release(badge);
        }

        _badges.Clear();
    }
}
