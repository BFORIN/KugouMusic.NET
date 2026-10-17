using System;
using System.Collections.Generic;

// Sonnet 调色板兼容门面：委托给全局通用的 PaletteExtractor

namespace AvaloniaSilkEffects.Sonnet;

public static class SonnetCoverPalette
{
    public static IReadOnlyList<EffectColor> Extract(ReadOnlySpan<byte> rgba, int count = 6) =>
        PaletteExtractor.Extract(rgba, count);
}
