using System;
using UnityEngine;

namespace VRClass.Core
{
    /// <summary>
    /// 空間的強調の拡大率と時間（SPEC 4.3）。
    /// </summary>
    public static class EmphasisMath
    {
        /// <summary>③回転で回す角度（度）。SPEC 4.3「鉛直軸まわりに1回転」。</summary>
        public const float FullTurnDeg = 360f;

        /// <summary>
        /// 拡大率（制限なし）s = 2D × tan(φ/2) / w。
        /// </summary>
        /// <param name="distance">頭から部品までの距離 D（m）</param>
        /// <param name="viewAngleDeg">見かけの大きさの視角 φ（度）</param>
        /// <param name="diameter">部品を包む球の直径 w（m）</param>
        public static float RawScaleFactor(float distance, float viewAngleDeg, float diameter)
        {
            if (diameter <= 0f || float.IsNaN(diameter))
            {
                throw new ArgumentOutOfRangeException(nameof(diameter), diameter, "部品の直径は正の値です。");
            }

            if (viewAngleDeg <= 0f || viewAngleDeg >= 180f)
            {
                throw new ArgumentOutOfRangeException(nameof(viewAngleDeg), viewAngleDeg, "視角は 0〜180 度の間です。");
            }

            double halfRad = viewAngleDeg * 0.5 * Math.PI / 180.0;
            return (float)(2.0 * distance * Math.Tan(halfRad) / diameter);
        }

        /// <summary>
        /// 拡大率 s。1 未満なら 1 に制限する（縮小はしない、2026-11-09 決定）。
        /// </summary>
        public static float ScaleFactor(float distance, float viewAngleDeg, float diameter)
        {
            return Math.Max(1f, RawScaleFactor(distance, viewAngleDeg, diameter));
        }

        /// <summary>
        /// 部品を包む球の直径 w = Bounds の対角線の長さ（Renderer.bounds.size から求める）。
        /// </summary>
        public static float BoundingSphereDiameter(Vector3 boundsSize)
        {
            return (float)Math.Sqrt((double)boundsSize.x * boundsSize.x
                                    + (double)boundsSize.y * boundsSize.y
                                    + (double)boundsSize.z * boundsSize.z);
        }

        /// <summary>③回転の時間 = 360° / ω（秒）。</summary>
        public static float RotationDurationSec(float rotationSpeedDegPerSec)
        {
            if (rotationSpeedDegPerSec <= 0f || float.IsNaN(rotationSpeedDegPerSec))
            {
                throw new ArgumentOutOfRangeException(nameof(rotationSpeedDegPerSec), rotationSpeedDegPerSec, "回転の速さは正の値です。");
            }

            return FullTurnDeg / rotationSpeedDegPerSec;
        }

        /// <summary>③回転の経過時間に対する回転角（度、0〜360）。等速。</summary>
        public static float RotationAngleDeg(float elapsedSec, float rotationSpeedDegPerSec)
        {
            float angle = Math.Max(0f, elapsedSec) * rotationSpeedDegPerSec;
            return Math.Min(angle, FullTurnDeg);
        }

        /// <summary>
        /// Ttotal = T1 + T2 + 回転時間 + (観察時間) + T4（SPEC 4.3）。条件1はこの時間だけ止める。
        /// </summary>
        public static float TotalDurationSec(float pullSec, float scaleSec, float rotationSpeedDegPerSec,
            bool useObservePhase, float observeSec, float returnSec)
        {
            float total = pullSec + scaleSec + RotationDurationSec(rotationSpeedDegPerSec) + returnSec;
            if (useObservePhase)
            {
                total += observeSec;
            }

            return total;
        }
    }
}
