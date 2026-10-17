namespace AvaloniaSilkEffects.Sonnet;

public static class SonnetRandom
{
    public static uint Hash(string value) => EffectMath.Hash(value);
    public static uint Mix(uint seed, uint salt) => EffectMath.Mix(seed, salt);
    public static double Hash01(uint seed, int index, uint salt) => EffectMath.Hash01(seed, index, salt);
}
