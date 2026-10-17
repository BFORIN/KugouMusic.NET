namespace AvaloniaSilkEffects.Tempera;

public readonly record struct TemperaCameraFrame(double X, double Y, double Scale, double Rotation);

/// <summary>Folia temperaCamera.ts: the shot-level camera path plus a deterministic hand-held breath.</summary>
public static class TemperaCamera
{
    private const double BreathMaxOffset = 0.006;
    private const double BreathMaxScale = 0.002;
    private const double BreathMaxRotation = 0.0015;

    private static double EaseInOutCubic(double value)
    {
        var t = TemperaEasing.Clamp01(value);
        return t < 0.5 ? 4 * t * t * t : 1 - Math.Pow(-2 * t + 2, 3) / 2;
    }

    private static double Lerp(double from, double to, double amount) => from + (to - from) * amount;

    /// <summary>Progress may exceed 1 slightly in gaps so the frame keeps drifting instead of freezing.</summary>
    public static TemperaCameraFrame Frame(TemperaShot shot, double progress)
    {
        var clamped = TemperaEasing.Clamp01(progress);
        var eased = clamped * 0.5 + EaseInOutCubic(clamped) * 0.5;
        var overshoot = Math.Max(0, progress - 1) * 0.35;
        var amount = eased + overshoot;
        var start = shot.Camera;
        var end = shot.CameraEnd;
        return new TemperaCameraFrame(Lerp(start.X, end.X, amount), Lerp(start.Y, end.Y, amount), Lerp(start.Zoom, end.Zoom, amount),
            Lerp(start.Rotation, end.Rotation, amount));
    }

    public static TemperaCameraFrame Breath(double time, double phase = 0)
    {
        var tau = time * Math.PI * 2;
        return new TemperaCameraFrame(
            (Math.Sin(tau * 0.13 + phase) * 0.65 + Math.Sin(tau * 0.31 + phase * 1.7) * 0.35) * BreathMaxOffset,
            (Math.Cos(tau * 0.11 + phase * 2.3) * 0.65 + Math.Sin(tau * 0.29 + phase * 0.9) * 0.35) * BreathMaxOffset,
            Math.Sin(tau * 0.09 + phase * 1.3) * BreathMaxScale,
            Math.Sin(tau * 0.07 + phase * 2.9) * BreathMaxRotation);
    }

    /// <summary>Ramps the breath in after the lyric reveal completes, so it never pops in mid-line.</summary>
    public static double BreathWeight(double time, double revealDoneTime, double rampDuration = 1.2) =>
        rampDuration <= 0 ? time >= revealDoneTime ? 1 : 0 : EaseInOutCubic(TemperaEasing.Clamp01((time - revealDoneTime) / rampDuration));
}

/// <summary>Folia TemperaTransitionFrame: the pose of a paragraph scene during a boundary.</summary>
public readonly record struct TemperaTransitionFrame(double X, double Y, double Scale, double Rotation, double Alpha, double Blur,
    double Wipe, double WipeAngle)
{
    public static TemperaTransitionFrame Idle { get; } = new(0, 0, 1, 0, 1, 0, 0, 0);
}

/// <summary>Folia temperaTransitions.ts + temperaSceneFrame.ts: seek-stable paragraph boundary frames along the flow.</summary>
public static class TemperaTransitions
{
    public static TemperaTransitionFrame Effect(TemperaTransitionKind kind, bool exit, double progress, double flowAngle)
    {
        var linear = TemperaEasing.Clamp01(progress);
        var eased = TemperaEasing.InOut(linear);
        var flowX = Math.Cos(flowAngle);
        var flowY = Math.Sin(flowAngle);
        var idle = TemperaTransitionFrame.Idle;
        if (kind == TemperaTransitionKind.BlockWipe)
            // One continuous 0..2 sweep: 0..1 brings the block in, 1..2 carries it off the far side.
            return idle with { Wipe = exit ? eased : 1 + eased, WipeAngle = flowAngle };
        if (kind == TemperaTransitionKind.CameraPan)
        {
            const double travel = 0.5;
            var offset = exit ? eased * travel : -(1 - eased) * travel;
            var alpha = exit ? 1 - TemperaEasing.Clamp01((linear - 0.72) / 0.28) : TemperaEasing.Clamp01(linear / 0.3);
            return idle with
            {
                X = flowX * offset,
                Y = flowY * offset,
                Scale = 1 + (exit ? eased : 1 - eased) * 0.03,
                Alpha = alpha,
                WipeAngle = flowAngle,
            };
        }
        // shape-carry: keeps drifting on the flow while it dilates and softens.
        var drift = (exit ? eased : eased - 1) * 0.09;
        var away = exit ? eased : 1 - eased;
        return idle with
        {
            X = flowX * drift,
            Y = flowY * drift,
            Scale = 1 + away * 0.07,
            Alpha = exit ? 1 - TemperaEasing.Clamp01((linear - 0.55) / 0.45) : TemperaEasing.Clamp01(linear / 0.45),
            Blur = away * 6,
            WipeAngle = flowAngle,
        };
    }

    public static TemperaTransitionFrame Exit(TemperaParagraph paragraph, double time, bool enabled)
    {
        var transition = paragraph.TransitionOut;
        if (!enabled || transition is null || time < transition.StartTime) return TemperaTransitionFrame.Idle;
        var progress = (time - transition.StartTime) / Math.Max(transition.EndTime - transition.StartTime, 0.001);
        var flowAngle = paragraph.Shots.Count > 0 ? paragraph.Shots[^1].FlowAngle : 0;
        return Effect(transition.Kind, true, progress, flowAngle);
    }

    public static TemperaTransitionFrame Enter(TemperaTransitionKind? kind, double timeSinceStart, double duration, bool enabled, double flowAngle)
    {
        if (!enabled || kind is not { } transition || timeSinceStart < 0 || timeSinceStart > duration) return TemperaTransitionFrame.Idle;
        return Effect(transition, false, timeSinceStart / Math.Max(duration, 0.001), flowAngle);
    }

    /// <summary>The frame a visible paragraph scene is posed with: pre-rolled entrance, wipe uncover, or its own exit.</summary>
    public static TemperaTransitionFrame SceneFrame(TemperaProgram program, int index, double time, bool isIncoming,
        TemperaTransition? outgoing, bool enabled)
    {
        var paragraph = program.Paragraphs[index];
        var previous = index > 0 ? program.Paragraphs[index - 1].TransitionOut : null;
        var enterDuration = previous is not null ? Math.Max(0.35, Math.Min(1, previous.EndTime - previous.StartTime)) : 0;
        // Only a wipe still enters after the boundary; everything else was pre-rolled through the outgoing window.
        var entering = enabled && previous is { Kind: TemperaTransitionKind.BlockWipe }
            && time >= paragraph.StartTime && time <= paragraph.StartTime + enterDuration;
        var flow = paragraph.Shots.Count > 0 ? paragraph.Shots[0].FlowAngle : 0;
        if (isIncoming && outgoing is not null)
            return Enter(outgoing.Kind, time - outgoing.StartTime, Math.Max(0.001, outgoing.EndTime - outgoing.StartTime), true, flow);
        if (entering && previous is not null)
            return Enter(previous.Kind, time - paragraph.StartTime, enterDuration, true, flow);
        return Exit(paragraph, time, enabled);
    }
}
