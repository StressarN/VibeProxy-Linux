using System.Collections.Generic;

namespace VibeProxy.Linux.Utilities;

public sealed class RingBuffer<T>
{
    private readonly Queue<T> _queue;
    private readonly int _capacity;

    public RingBuffer(int capacity)
    {
        _capacity = capacity;
        _queue = new Queue<T>(capacity);
    }

    public void Add(T item)
    {
        if (_queue.Count >= _capacity)
        {
            _queue.Dequeue();
        }

        _queue.Enqueue(item);
    }

    public IReadOnlyList<T> Snapshot() => _queue.ToArray();
}
