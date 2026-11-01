using System.Collections.Generic;

namespace FactionColonies
{
    /// <summary>
    /// Internal helper that encapsulates the list-management boilerplate every per-domain
    /// registry shares: add-if-new, remove, clear, and a read-only view. Use as a private
    /// field inside a per-domain registry; the registry exposes <c>Register / Unregister /
    /// ClearAll</c> as thin delegating wrappers.
    /// </summary>
    internal class RegistryList<T> where T : class
    {
        private readonly List<T> _items = new List<T>();

        public void Register(T item)
        {
            if (item is object && !_items.Contains(item)) _items.Add(item);
        }

        public void Unregister(T item)
        {
            if (item is object) _items.Remove(item);
        }

        public void ClearAll() => _items.Clear();

        public IReadOnlyList<T> Items => _items;

        public int Count => _items.Count;
    }
}
