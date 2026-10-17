using System;
using System.Collections.Generic;
using System.Numerics;

// 统一的 2D 矢量路径与绘图命令记录器：提供路径动词、描边/填充命令以及几何路径分段构建能力

namespace AvaloniaSilkEffects;

public enum EffectPathVerb
{
    MoveTo,
    LineTo,
    QuadraticCurveTo,
    BezierCurveTo,
    Arc,
    Circle,
    Rectangle,
}

public enum EffectPaintKind
{
    Stroke,
    Fill,
}

public sealed record EffectPathCommand(
    EffectPathVerb Verb,
    double A, double B, double C = 0, double D = 0, double E = 0, double F = 0,
    bool Anticlockwise = false,
    double Length = 0,
    double LastX = 0,
    double LastY = 0);

public sealed record EffectPaintCommand(
    EffectPaintKind Kind,
    IReadOnlyList<EffectPathCommand> Path,
    uint Color,
    double Alpha,
    double Width,
    double Length,
    double StaggerDelay,
    double StaggerSpan);
