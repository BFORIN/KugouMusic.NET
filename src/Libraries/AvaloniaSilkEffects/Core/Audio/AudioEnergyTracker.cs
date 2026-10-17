using System;

// 统一的音频能量跟踪器：非对称指数平滑低通滤波（Attack 快、Decay 慢），保证高帧率和切歌定位时的平滑性

namespace AvaloniaSilkEffects;

/// <summary>
/// 非对称音频能量跟踪平滑器。
/// </summary>
public struct AudioEnergyTracker
{
    private float _current;

    public AudioEnergyTracker(float initialValue = 0f)
    {
        _current = initialValue;
    }

    public float Current => _current;

    /// <summary>
    /// 重置平滑器状态（切歌或跳转时使用，避免动画拖尾残影）。
    /// </summary>
    public void Reset(float value = 0f) => _current = value;

    /// <summary>
    /// 根据当前目标能量与时间步长计算平滑后的能量值。
    /// </summary>
    public float Update(float target, float deltaSeconds, float attackRate = 10f, float decayRate = 4f)
    {
        if (!float.IsFinite(deltaSeconds) || deltaSeconds <= 0)
            return _current;

        var rate = target >= _current ? attackRate : decayRate;
        var factor = 1f - MathF.Exp(-rate * deltaSeconds);
        _current += (target - _current) * factor;
        return _current;
    }
}
