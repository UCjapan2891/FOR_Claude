using System;
using UnityEngine;

namespace VRClass.Core
{
    /// <summary>
    /// ソケットに付けるときの向きのずれ（SPEC 5.1：Quaternion.Angle が [15]° 以内なら吸着）。
    /// 回転対称の部品は、対称軸まわりの回転を判定から外せる（ignoreSymmetryAxis、2026-11-09 決定）。
    /// </summary>
    public static class OrientationMath
    {
        /// <summary>2つの回転のずれ（度、0〜180）。Unity の Quaternion.Angle と同じ式。</summary>
        public static float AngleDeg(Quaternion a, Quaternion b)
        {
            double dot = (double)a.x * b.x + (double)a.y * b.y + (double)a.z * b.z + (double)a.w * b.w;
            double lenSq = ((double)a.x * a.x + (double)a.y * a.y + (double)a.z * a.z + (double)a.w * a.w)
                           * ((double)b.x * b.x + (double)b.y * b.y + (double)b.z * b.z + (double)b.w * b.w);
            if (lenSq <= 0)
            {
                return float.NaN;
            }

            double c = Math.Abs(dot) / Math.Sqrt(lenSq);
            if (c > 1.0) c = 1.0;
            return (float)(2.0 * Math.Acos(c) * 180.0 / Math.PI);
        }

        /// <summary>
        /// 向きのずれ（度）。symmetryAxis が None 以外なら、その軸の向きのずれだけを見る
        /// （軸まわりにいくら回っていても 0°）。
        /// </summary>
        public static float AngleDeg(Quaternion a, Quaternion b, SymmetryAxis symmetryAxis)
        {
            if (symmetryAxis == SymmetryAxis.None)
            {
                return AngleDeg(a, b);
            }

            Vector3 local = LocalAxis(symmetryAxis);
            return GazeMath.AngleDeg(Rotate(a, local), Rotate(b, local));
        }

        /// <summary>ずれが上限以内か。NaN なら false。</summary>
        public static bool IsWithinTolerance(float angleDeg, float toleranceDeg)
        {
            return !float.IsNaN(angleDeg) && angleDeg <= toleranceDeg;
        }

        public static Vector3 LocalAxis(SymmetryAxis axis)
        {
            switch (axis)
            {
                case SymmetryAxis.X: return new Vector3(1f, 0f, 0f);
                case SymmetryAxis.Y: return new Vector3(0f, 1f, 0f);
                case SymmetryAxis.Z: return new Vector3(0f, 0f, 1f);
                default: throw new ArgumentOutOfRangeException(nameof(axis), axis, "軸が指定されていません。");
            }
        }

        /// <summary>ベクトル v を回転 q で回す（q * v と同じ）。q は正規化されている前提。</summary>
        public static Vector3 Rotate(Quaternion q, Vector3 v)
        {
            // v' = v + 2w (u × v) + 2 u × (u × v)、u = (x, y, z)
            double ux = q.x, uy = q.y, uz = q.z, w = q.w;
            double cx = uy * v.z - uz * v.y;
            double cy = uz * v.x - ux * v.z;
            double cz = ux * v.y - uy * v.x;
            double ccx = uy * cz - uz * cy;
            double ccy = uz * cx - ux * cz;
            double ccz = ux * cy - uy * cx;
            return new Vector3(
                (float)(v.x + 2.0 * (w * cx + ccx)),
                (float)(v.y + 2.0 * (w * cy + ccy)),
                (float)(v.z + 2.0 * (w * cz + ccz)));
        }
    }
}
