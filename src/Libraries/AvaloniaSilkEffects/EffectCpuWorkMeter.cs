using System.Diagnostics;

namespace AvaloniaSilkEffects;

/// <summary>Measures synchronous UI preparation separately from GL submission. Instances are thread-affine.</summary>
public sealed class EffectCpuWorkMeter
{
    private int _depth;
    public EffectPerformanceHistory History { get; } = new();
    public Scope Measure() => new(this);

    public readonly struct Scope : IDisposable
    {
        private readonly EffectCpuWorkMeter _owner;
        private readonly long _started;
        private readonly long _allocated;
        internal Scope(EffectCpuWorkMeter owner)
        {
            _owner = owner;
            _started = Stopwatch.GetTimestamp();
            _allocated = GC.GetAllocatedBytesForCurrentThread();
            owner._depth++;
        }
        public void Dispose()
        {
            if (--_owner._depth != 0) return;
            var elapsed = Stopwatch.GetElapsedTime(_started).TotalMilliseconds;
            _owner.History.Record(new(0, 0, 0, 0, 0, 0, elapsed,
                GC.GetAllocatedBytesForCurrentThread() - _allocated));
        }
    }
}
