using System;
using UnityEngine;

namespace VRClass.Core
{
    /// <summary>
    /// 再配置と引き寄せ先の位置（SPEC 4.2、4.3①）。
    /// 正面は水平：HMD の正面方向の y 成分を 0 にして正規化したもの（2026-11-09 決定）。
    /// </summary>
    public static class RelocationMath
    {
        /// <summary>水平成分の長さを 0 とみなす閾値。</summary>
        public const float ZeroLength = 1e-4f;

        /// <summary>
        /// HMD の正面方向を水平面に投影し、長さ 1 にする。
        /// 真下・真上を向いていて正面の水平成分がほぼ 0 のときは、HMD の上方向から求める
        /// （真下を見ているとき、HMD の上方向は顔の正面の水平方向を指す。真上なら逆向き）。
        /// どちらも決まらなければ false。
        /// </summary>
        /// <param name="forward">HMD の正面方向（Transform.forward）</param>
        /// <param name="up">HMD の上方向（Transform.up）</param>
        public static bool TryHorizontalDirection(Vector3 forward, Vector3 up, out Vector3 direction)
        {
            if (TryNormalizeHorizontal(forward, out direction))
            {
                return true;
            }

            // forward.y < 0（見下ろし）なら up、forward.y > 0（見上げ）なら −up が前
            Vector3 fallback = forward.y <= 0f ? up : -1f * up;
            return TryNormalizeHorizontal(fallback, out direction);
        }

        /// <summary>
        /// 再配置の位置 p_new = p_h + D × f / |f|（SPEC 4.2）。
        /// f は <see cref="TryHorizontalDirection"/> で求めた水平の正面方向。高さは頭の高さになる。
        /// </summary>
        public static Vector3 RelocatePosition(Vector3 headPosition, Vector3 horizontalDirection, float distance)
        {
            Vector3 dir = NormalizeOrThrow(horizontalDirection);
            return headPosition + distance * dir;
        }

        /// <summary>
        /// 他の部品と重なるとき、奥へ shiftCount 回ずらした位置（SPEC 4.2：[0.1] m ずつ）。
        /// shiftCount = 0 なら basePosition のまま。
        /// </summary>
        public static Vector3 ShiftedPosition(Vector3 basePosition, Vector3 horizontalDirection, float step, int shiftCount)
        {
            if (shiftCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(shiftCount), shiftCount, "ずらす回数は0以上です。");
            }

            Vector3 dir = NormalizeOrThrow(horizontalDirection);
            return basePosition + (step * shiftCount) * dir;
        }

        /// <summary>
        /// 引き寄せ先 pt（SPEC 4.3①）：頭から水平の正面へ D の位置。
        /// 頭から dmin 以上離す（D &lt; dmin のときは dmin の位置）。
        /// </summary>
        public static Vector3 PullTarget(Vector3 headPosition, Vector3 horizontalDirection, float distance, float minHeadDistance)
        {
            return RelocatePosition(headPosition, horizontalDirection, PullDistance(distance, minHeadDistance));
        }

        /// <summary>引き寄せ先までの距離 = max(D, dmin)。拡大率の計算にもこの距離を使う。</summary>
        public static float PullDistance(float distance, float minHeadDistance)
        {
            return Math.Max(distance, minHeadDistance);
        }

        /// <summary>引き寄せ中の位置 p(t) = p0 + (pt − p0) × e(t)（SPEC 4.3①）。t は 0〜1。</summary>
        public static Vector3 PullPosition(Vector3 p0, Vector3 pt, float t)
        {
            return p0 + Easing.SmoothStep(t) * (pt - p0);
        }

        private static bool TryNormalizeHorizontal(Vector3 v, out Vector3 direction)
        {
            double len = Math.Sqrt((double)v.x * v.x + (double)v.z * v.z);
            if (len < ZeroLength)
            {
                direction = new Vector3(0f, 0f, 0f);
                return false;
            }

            direction = new Vector3((float)(v.x / len), 0f, (float)(v.z / len));
            return true;
        }

        private static Vector3 NormalizeOrThrow(Vector3 v)
        {
            double len = Math.Sqrt((double)v.x * v.x + (double)v.y * v.y + (double)v.z * v.z);
            if (len < ZeroLength)
            {
                throw new ArgumentException("方向ベクトルの長さが 0 です。", nameof(v));
            }

            return new Vector3((float)(v.x / len), (float)(v.y / len), (float)(v.z / len));
        }
    }
}
