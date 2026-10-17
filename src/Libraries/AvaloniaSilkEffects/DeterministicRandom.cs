namespace AvaloniaSilkEffects;

public struct DeterministicRandom(uint seed)
{
    private uint _state = seed == 0 ? 0x6D2B79F5u : seed;

    public static uint Hash(string value) => EffectMath.Hash(value);

    public uint NextUInt()
    {
        var value = _state;
        value ^= value << 13;
        value ^= value >> 17;
        value ^= value << 5;
        _state = value;
        return value;
    }

    public float NextSingle() => (NextUInt() >> 8) * (1f / 16_777_216f);
}
