namespace AvaloniaSilkEffects;

internal enum EffectCacheWorkKind { None, Paragraph, Credits }
internal readonly record struct EffectCacheWork(EffectCacheWorkKind Kind, int Index = -1);

/// <summary>Shared retention and one-expensive-operation policy; transitions and paragraph construction stay theme-owned.</summary>
internal sealed class EffectParagraphCache<T> : Dictionary<int, T>, IDisposable where T : IDisposable
{
    private readonly Queue<T> _retired = new();

    public void RetireAll()
    {
        foreach (var item in Values) _retired.Enqueue(item);
        Clear();
    }

    public EffectCacheWork NextWork(int active, int paragraphCount, bool creditsNeeded)
    {
        if (_retired.TryDequeue(out var retired))
        {
            retired.Dispose();
            return default;
        }
        if (active + 1 < paragraphCount && !ContainsKey(active + 1))
            return new(EffectCacheWorkKind.Paragraph, active + 1);
        if (creditsNeeded) return new(EffectCacheWorkKind.Credits);
        if (active > 0 && !ContainsKey(active - 1))
            return new(EffectCacheWorkKind.Paragraph, active - 1);
        return default;
    }

    public void Dispose()
    {
        foreach (var item in Values) item.Dispose();
        Clear();
        while (_retired.TryDequeue(out var retired)) retired.Dispose();
    }
}
