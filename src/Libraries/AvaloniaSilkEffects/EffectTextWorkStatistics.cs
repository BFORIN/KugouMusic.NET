namespace AvaloniaSilkEffects;

/// <summary>Text preparation sub-stages, nested within frame Update/Draw. Warmup includes all of its sub-stages.</summary>
public readonly record struct EffectTextWorkStatistics(
    double MeasureMilliseconds,
    double RasterMilliseconds,
    double UploadMilliseconds,
    double WarmupMilliseconds,
    int WarmedTextures,
    long WarmedBytes);

internal enum EffectTextStage { Measure, Raster, Upload, Warmup }
