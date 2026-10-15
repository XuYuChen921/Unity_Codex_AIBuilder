using System;
using UnityEngine;

namespace AIBuilder.CreatureStage
{
    public static class StageSpeedCurve
    {
        // 固定时间格积分，与播放帧率和 Seek 历史无关。负速度按零处理。
        public static double Integral(AnimationCurve curve, double until)
        {
            until = Math.Max(0, Math.Min(1, until));
            if (curve == null || curve.length == 0) return until;
            const int steps = 256;
            double sum = 0;
            for (var i = 0; i < steps && i / (double)steps < until; i++)
            {
                var a = i / (double)steps;
                var b = Math.Min(until, (i + 1) / (double)steps);
                var va = curve.Evaluate((float)a);
                var vb = curve.Evaluate((float)b);
                if (!StageMotionPlan.IsFinite(va) || !StageMotionPlan.IsFinite(vb))
                    throw new InvalidOperationException("速度曲线含无效数值。");
                sum += (Math.Max(0, va) + Math.Max(0, vb)) * 0.5 * (b - a);
            }
            return sum;
        }
    }
}
