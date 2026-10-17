namespace AvaloniaSilkEffects;

/// <summary>CPU wall times, not GPU execution times. Text preparation is nested inside Update/Draw.</summary>
public readonly record struct EffectFrameTimings(
    double PrepareMilliseconds,
    double UpdateMilliseconds,
    double DrawMilliseconds,
    double PostProcessMilliseconds,
    double CollectMilliseconds,
    double TextPreparationMilliseconds,
    double CpuMilliseconds,
    long AllocatedBytes)
{
    public EffectTextWorkStatistics TextWork { get; init; }
}

public readonly record struct EffectMetricDistribution(double Mean, double P95, double P99, double Maximum);

public readonly record struct EffectPerformanceSnapshot(
    int SampleCount,
    EffectMetricDistribution Cpu,
    EffectMetricDistribution Prepare,
    EffectMetricDistribution Update,
    EffectMetricDistribution Draw,
    EffectMetricDistribution PostProcess,
    EffectMetricDistribution Collect,
    EffectMetricDistribution TextPreparation,
    EffectMetricDistribution AllocatedBytes,
    EffectMetricDistribution FrameInterval)
{
    public EffectMetricDistribution TextMeasure { get; init; }
    public EffectMetricDistribution TextRaster { get; init; }
    public EffectMetricDistribution TextUpload { get; init; }
    public EffectMetricDistribution TextWarmup { get; init; }
}

/// <summary>
/// Last 600 completed frames by default. Recording allocates nothing; percentiles use nearest rank and
/// are calculated only on demand. Frame intervals exclude the first frame and explicit discontinuities.
/// Allocation counts cover only the calling managed thread, excluding native/worker allocations.
/// </summary>
public sealed class EffectPerformanceHistory
{
    private readonly object _gate = new();
    private readonly (EffectFrameTimings Timings, double Interval)[] _samples;
    private readonly double[] _scratch;
    private int _cursor;
    private int _count;

    public EffectPerformanceHistory(int capacity = 600)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        _samples = new (EffectFrameTimings, double)[capacity];
        _scratch = new double[capacity];
    }

    public void Record(in EffectFrameTimings timings, double frameIntervalMilliseconds = 0)
    {
        lock (_gate)
        {
            _samples[_cursor] = (timings, frameIntervalMilliseconds);
            _cursor = (_cursor + 1) % _samples.Length;
            _count = Math.Min(_count + 1, _samples.Length);
        }
    }

    public void Reset()
    {
        lock (_gate) { _cursor = 0; _count = 0; }
    }

    public EffectPerformanceSnapshot Snapshot()
    {
        lock (_gate)
            return new(_count, Distribution(0), Distribution(1), Distribution(2), Distribution(3),
                Distribution(4), Distribution(5), Distribution(6), Distribution(7), Distribution(8))
            {
                TextMeasure = Distribution(9), TextRaster = Distribution(10),
                TextUpload = Distribution(11), TextWarmup = Distribution(12),
            };
    }

    private EffectMetricDistribution Distribution(int metric)
    {
        var count = 0;
        var sum = 0d;
        for (var index = 0; index < _count; index++)
        {
            var (t, interval) = _samples[index];
            var value = metric switch
            {
                0 => t.CpuMilliseconds,
                1 => t.PrepareMilliseconds,
                2 => t.UpdateMilliseconds,
                3 => t.DrawMilliseconds,
                4 => t.PostProcessMilliseconds,
                5 => t.CollectMilliseconds,
                6 => t.TextPreparationMilliseconds,
                7 => t.AllocatedBytes,
                8 => interval,
                9 => t.TextWork.MeasureMilliseconds,
                10 => t.TextWork.RasterMilliseconds,
                11 => t.TextWork.UploadMilliseconds,
                _ => t.TextWork.WarmupMilliseconds,
            };
            if (!double.IsFinite(value) || value < 0 || metric == 8 && value == 0) continue;
            _scratch[count++] = value;
            sum += value;
        }
        if (count == 0) return default;
        Array.Sort(_scratch, 0, count);
        return new(sum / count, _scratch[(int)Math.Ceiling(count * 0.95) - 1],
            _scratch[(int)Math.Ceiling(count * 0.99) - 1], _scratch[count - 1]);
    }
}
