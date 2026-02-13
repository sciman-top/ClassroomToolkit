using System;
using System.Collections.Generic;

namespace ClassroomToolkit.App.Ink;

public sealed class InkFinalCache
{
    private readonly int _capacity;
    private readonly Dictionary<string, LinkedListNode<CacheEntry>> _entries = new(StringComparer.OrdinalIgnoreCase);
    private readonly LinkedList<CacheEntry> _lru = new();

    public InkFinalCache(int capacity)
    {
        _capacity = Math.Max(1, capacity);
    }

    public bool TryGet(string key, out List<InkStrokeData> strokes)
    {
        strokes = new List<InkStrokeData>();
        if (!_entries.TryGetValue(key, out var node))
        {
            return false;
        }
        _lru.Remove(node);
        _lru.AddFirst(node);
        strokes = node.Value.Strokes;
        return true;
    }

    public void Set(string key, List<InkStrokeData> strokes)
    {
        if (_entries.TryGetValue(key, out var existing))
        {
            existing.Value.Strokes = strokes;
            _lru.Remove(existing);
            _lru.AddFirst(existing);
            return;
        }
        var entry = new CacheEntry(key, strokes);
        var node = new LinkedListNode<CacheEntry>(entry);
        _lru.AddFirst(node);
        _entries[key] = node;
        TrimIfNeeded();
    }

    public void Clear()
    {
        _entries.Clear();
        _lru.Clear();
    }

    public void Remove(string key)
    {
        if (!_entries.TryGetValue(key, out var node))
        {
            return;
        }
        _entries.Remove(key);
        _lru.Remove(node);
    }

    private void TrimIfNeeded()
    {
        while (_entries.Count > _capacity && _lru.Last != null)
        {
            var last = _lru.Last;
            _lru.RemoveLast();
            _entries.Remove(last.Value.Key);
        }
    }

    private sealed class CacheEntry
    {
        public CacheEntry(string key, List<InkStrokeData> strokes)
        {
            Key = key;
            Strokes = strokes;
        }

        public string Key { get; }
        public List<InkStrokeData> Strokes { get; set; }
    }
}
