using System.Collections.Concurrent;

namespace wwwmmm.FluidEngine.Core.Context;

public sealed class FluidLayer
{
    private readonly ConcurrentDictionary<string, FluidSlot> _state = new();

    public void Set(string sessionId, string key, object value)
    {
        var slot = _state.GetOrAdd(sessionId, _ => new FluidSlot());
        slot.Data[key] = value;
        slot.UpdatedAt = DateTimeOffset.UtcNow;
    }

    public object? Get(string sessionId, string key, object? defaultValue = null)
    {
        if (_state.TryGetValue(sessionId, out var slot) && slot.Data.TryGetValue(key, out var value))
        {
            return value;
        }
        return defaultValue;
    }

    public IReadOnlyDictionary<string, object> Snapshot(string sessionId)
    {
        if (_state.TryGetValue(sessionId, out var slot))
        {
            return new Dictionary<string, object>(slot.Data);
        }
        return new Dictionary<string, object>();
    }

    private sealed class FluidSlot
    {
        public ConcurrentDictionary<string, object> Data { get; } = new();
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
