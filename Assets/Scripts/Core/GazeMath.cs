using System;
using UnityEngine;

namespace VRClass.Core
{
    /// <summary>
    /// 注視判定の式（SPEC 4.1）。頭の向きで推定し、視線追跡は使わない。
    /// θ = arccos( (f・d) / (|f||d|) )
    /// </summary>
    public static class GazeMath
    {
        /// <summary>長さを 0 とみなす閾値（m）。</summary>
        public const float ZeroLength = 1e-6f;

        /// <summary>
        /// f と d のなす角 θ（度、0〜180）。
        /// f か d の長さが 0 なら角度が決まらないので NaN を返す（<see cref="IsWithinThreshold"/> は false になる）。
        /// Unity の Vector3.Angle と同じ式だが、長さ 0 のときに 0° を返さないよう自前で計算する。
        /// </summary>
        /// <param name="f">HMD の正面方向</param>
        /// <param name="d">頭の位置から対象部品の中心へのベクトル</param>
        public static float AngleDeg(Vector3 f, Vector3 d)
        {
            double fLen = Math.Sqrt((double)f.x * f.x + (double)f.y * f.y + (double)f.z * f.z);
            double dLen = Math.Sqrt((double)d.x * d.x + (double)d.y * d.y + (double)d.z * d.z);
            if (fLen < ZeroLength || dLen < ZeroLength)
            {
                return float.NaN;
            }

            double dot = (double)f.x * d.x + (double)f.y * d.y + (double)f.z * d.z;
            double cos = dot / (fLen * dLen);
            if (cos > 1.0) cos = 1.0;     // 丸め誤差で arccos が NaN にならないように
            if (cos < -1.0) cos = -1.0;
            return (float)(Math.Acos(cos) * 180.0 / Math.PI);
        }

        /// <summary>頭の位置・正面方向・対象の中心から θ（度）を求める。d = target − head。</summary>
        public static float AngleToTargetDeg(Vector3 headPosition, Vector3 headForward, Vector3 targetCenter)
        {
            return AngleDeg(headForward, targetCenter - headPosition);
        }

        /// <summary>θ ≤ θth か。θ が NaN なら false。</summary>
        public static bool IsWithinThreshold(float thetaDeg, float thresholdDeg)
        {
            return !float.IsNaN(thetaDeg) && thetaDeg <= thresholdDeg;
        }
    }
}
