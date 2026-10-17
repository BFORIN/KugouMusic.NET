namespace AvaloniaSilkEffects.Tempera;

/// <summary>
/// Folia visualizerSeedHash.ts / temperaRandom.ts. Seeds stay <see cref="long"/> because Tempera adds offsets to a 32-bit hash
/// (<c>sceneSeed + shotIndex * 97</c>) and a few call sites read the unwrapped sum (<c>seed % 7</c>); the hash itself works
/// modulo 2^32 exactly like JavaScript's ToInt32.
/// </summary>
public static class TemperaRandom
{
    /// <summary>FNV-1a over UTF-16 code units, like <c>charCodeAt</c>.</summary>
    public static uint Hash(string value)
    {
        var hash = 2166136261u;
        foreach (var character in value)
        {
            hash ^= character;
            hash = unchecked(hash * 16777619u);
        }
        return hash;
    }

    /// <summary><c>Math.imul((Math.trunc(seed) ^ salt) &gt;&gt;&gt; 0, 2654435761) &gt;&gt;&gt; 0</c>.</summary>
    public static uint Mix(long seed, long salt) => unchecked(((uint)seed ^ (uint)salt) * 2654435761u);

    /// <summary>Deterministic 0..1 jitter per element index.</summary>
    public static double Hash01(long seed, long index, long salt) =>
        Mix(seed + unchecked((int)((index + 1) * 97)), salt) / 4294967296d;

    /// <summary>A deterministic pick that differs from <paramref name="previous"/> when possible.</summary>
    public static string ChooseWithoutRepeat(IReadOnlyList<string> choices, string seed, string? previous)
    {
        var start = (int)(Hash(seed) % (uint)choices.Count);
        for (var offset = 0; offset < choices.Count; offset++)
        {
            var candidate = choices[(start + offset) % choices.Count];
            if (candidate != previous) return candidate;
        }
        return choices[start];
    }
}
