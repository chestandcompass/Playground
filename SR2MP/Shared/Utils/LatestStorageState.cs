namespace SR2MP.Shared.Utils;

// Absolute states can safely wait for a scene/storage to load; deltas cannot.
internal sealed class LatestStorageState<T>
{
    private readonly Dictionary<string, (uint revision, T value)> states = new(StringComparer.Ordinal);
    internal bool Store(string id, uint revision, T value)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Length > 256 || revision == 0) throw new InvalidDataException("Invalid storage state.");
        if (states.TryGetValue(id, out var old) && old.revision > revision) return false;
        if (!states.ContainsKey(id) && states.Count >= 4096) throw new InvalidDataException("Too many storage states.");
        states[id] = (revision, value);
        return true;
    }
    internal bool TryGet(string id, out T value)
    {
        if (states.TryGetValue(id, out var entry)) { value = entry.value; return true; }
        value = default!; return false;
    }
    internal IEnumerable<string> Keys => states.Keys;
    internal void Clear() => states.Clear();
}

internal sealed class AtomicStockGate
{
    private readonly Dictionary<string, bool> completed = new(StringComparer.Ordinal);
    private readonly Queue<string> order = new();
    internal bool Execute(string request, Func<int> read, Action<int> write, Func<bool> place)
    {
        lock (completed)
        {
            if (completed.TryGetValue(request, out var result)) return result;
            var count = read();
            var accepted = false;
            if (count > 0)
            {
                try { write(count - 1); accepted = place(); }
                finally { if (!accepted) write(count); }
            }
            completed[request] = accepted;
            order.Enqueue(request);
            if (order.Count > 4096) completed.Remove(order.Dequeue());
            return accepted;
        }
    }
    internal void Clear() { lock (completed) { completed.Clear(); order.Clear(); } }
}
