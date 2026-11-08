namespace FactionColonies;

/// <summary>
/// Internal helper that encapsulates the list-management boilerplate every per-domain
/// registry shares: add-if-new, remove, clear, and a read-only view. Use as a private
/// field inside a per-domain registry; the registry exposes <c>Register / Unregister /
/// ClearAll</c> as thin delegating wrappers.
/// </summary>
internal class RegistryList<T> : RegistryListBase where T : class
{
    private readonly List<T> _items = [];

    internal override object Capture() => new List<T>(_items);

    internal override void Restore(object state)
    {
        _items.Clear();
        if (state is List<T> items) _items.AddRange(items);
        Version++;
    }

    /// <summary>Bumped on every change to the list, so a registry can cache a derived view
    /// (e.g. a sorted copy) and rebuild it only when this differs from the value it cached at.</summary>
    public int Version { get; private set; }

    public void Register(T item)
    {
        if (item is not null && !_items.Contains(item))
        {
            _items.Add(item);
            Version++;
        }
    }

    public void Unregister(T item)
    {
        if (item is not null && _items.Remove(item)) Version++;
    }

    public void ClearAll()
    {
        _items.Clear();
        Version++;
    }

    public IReadOnlyList<T> Items => _items;

    /// <summary>
    /// Returns the registered items that pass <paramref name="isValid"/>. Interface members default
    /// to null/invalid identities (e.g. a null <c>WorldObject</c>), so an implementer that forgot to
    /// override one is skipped here, with a one-time error naming its type, instead of crashing a
    /// consumer later. A check that throws counts as invalid. Returns the backing list itself when
    /// every item passes, so the common case allocates nothing.
    /// </summary>
    public IReadOnlyList<T> Valid(Func<T, bool> isValid, string requirement)
    {
        List<T> filtered = null;
        for (int i = 0; i < _items.Count; i++)
        {
            T item = _items[i];
            bool ok;
            try { ok = isValid(item); }
            catch { ok = false; }

            if (ok)
            {
                filtered?.Add(item);
                continue;
            }

            LogUtil.ErrorOnce($"{typeof(T).Name} {item.GetType().FullName} is registered but {requirement}; skipping it.");
            filtered ??= _items.GetRange(0, i);
        }
        return filtered ?? _items;
    }

    public int Count => _items.Count;
}
