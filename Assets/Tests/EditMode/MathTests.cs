using System;
using NUnit.Framework;
using UnityEngine;
using VRClass.Core;

namespace VRClass.Tests
{
    internal static class VecAssert
    {
        public static void Near(Vector3 expected, Vector3 actual, float tol = 1e-5f)
        {
            Assert.AreEqual(expected.x, actual.x, tol, $"x：期待 {expected} 実際 {actual}");
            Assert.AreEqual(expected.y, actual.y, tol, $"y：期待 {expected} 実際 {actual}");
            Assert.AreEqual(expected.z, actual.z, tol, $"z：期待 {expected} 実際 {actual}");
        }
    }

    /// <summary>SPEC 4.1 注視角。</summary>
    public class GazeMathTests
    {
        private const float Tol = 1e-3f;

        [Test]
        public void Orthogonal_Is90()
        {
            Assert.AreEqual(90f, GazeMath.AngleDeg(new Vector3(0, 0, 1), new Vector3(1, 0, 0)), Tol);
            Assert.AreEqual(90f, GazeMath.AngleDeg(new Vector3(0, 0, 1), new Vector3(0, 2, 0)), Tol);
        }

        [Test]
        public void SameDirection_Is0_EvenWithDifferentLengths()
        {
            Assert.AreEqual(0f, GazeMath.AngleDeg(new Vector3(0, 0, 1), new Vector3(0, 0, 5)), Tol);
        }

        [Test]
        public void Opposite_Is180()
        {
            Assert.AreEqual(180f, GazeMath.AngleDeg(new Vector3(0, 0, 1), new Vector3(0, 0, -3)), Tol);
        }

        [Test]
        public void Diagonal_Is45()
        {
            Assert.AreEqual(45f, GazeMath.AngleDeg(new Vector3(1, 0, 0), new Vector3(1, 1, 0)), Tol);
        }

        [Test]
        public void Sqrt3_Is60()
        {
            // cos60° = 1/2：(1,0,0) と (1,√3,0)
            Assert.AreEqual(60f, GazeMath.AngleDeg(new Vector3(1, 0, 0), new Vector3(1, (float)Math.Sqrt(3), 0)), Tol);
        }

        [Test]
        public void ZeroVector_IsNaN_AndNotWithinThreshold()
        {
            float theta = GazeMath.AngleDeg(new Vector3(0, 0, 1), new Vector3(0, 0, 0));
            Assert.IsTrue(float.IsNaN(theta));
            Assert.IsFalse(GazeMath.IsWithinThreshold(theta, 20f));
        }

        [Test]
        public void AngleToTarget_UsesTargetMinusHead()
        {
            // 頭 (0,1.6,0) が +z を向き、部品が (1,1.6,1) にある → 45°
            float theta = GazeMath.AngleToTargetDeg(new Vector3(0, 1.6f, 0), new Vector3(0, 0, 1), new Vector3(1, 1.6f, 1));
            Assert.AreEqual(45f, theta, Tol);
        }

        [TestCase(19.9f, 20f, true)]
        [TestCase(20f, 20f, true)] // θ ≤ θth なので等しいときは見ている
        [TestCase(20.1f, 20f, false)]
        [TestCase(0f, 20f, true)]
        public void IsWithinThreshold(float theta, float threshold, bool expected)
        {
            Assert.AreEqual(expected, GazeMath.IsWithinThreshold(theta, threshold));
        }
    }

    /// <summary>SPEC 4.3 イージング e(t) = 3t² − 2t³。</summary>
    public class EasingTests
    {
        private const float Tol = 1e-6f;

        [Test]
        public void Endpoints()
        {
            Assert.AreEqual(0f, Easing.SmoothStep(0f), Tol);
            Assert.AreEqual(1f, Easing.SmoothStep(1f), Tol);
        }

        [Test]
        public void HandComputedValues()
        {
            Assert.AreEqual(0.5f, Easing.SmoothStep(0.5f), Tol);     // 3/4 − 2/8
            Assert.AreEqual(0.15625f, Easing.SmoothStep(0.25f), Tol); // 3/16 − 2/64 = 10/64
            Assert.AreEqual(0.84375f, Easing.SmoothStep(0.75f), Tol); // 27/16 − 54/64 = 54/64
        }

        [Test]
        public void DerivativeIsZeroAtBothEnds()
        {
            // 片側差分：e'(0) ≈ (e(h) − e(0)) / h = 3h − 2h² → 0
            const float h = 1e-3f;
            float d0 = (Easing.SmoothStep(h) - Easing.SmoothStep(0f)) / h;
            float d1 = (Easing.SmoothStep(1f) - Easing.SmoothStep(1f - h)) / h;
            Assert.AreEqual(0f, d0, 1e-2f);
            Assert.AreEqual(0f, d1, 1e-2f);

            // 中央の傾きは e'(0.5) = 6t − 6t² = 1.5（差分が 0 に近いのが端だけであることの確認）
            float dMid = (Easing.SmoothStep(0.5f + h) - Easing.SmoothStep(0.5f - h)) / (2 * h);
            Assert.AreEqual(1.5f, dMid, 1e-2f);
        }

        [Test]
        public void MonotonicIncreasing()
        {
            float prev = Easing.SmoothStep(0f);
            for (int i = 1; i <= 100; i++)
            {
                float v = Easing.SmoothStep(i / 100f);
                Assert.GreaterOrEqual(v, prev);
                prev = v;
            }
        }

        [Test]
        public void OutOfRange_IsClamped()
        {
            Assert.AreEqual(0f, Easing.SmoothStep(-0.5f), Tol);
            Assert.AreEqual(1f, Easing.SmoothStep(1.5f), Tol);
        }

        [TestCase(0f, 1f, 0f)]
        [TestCase(0.5f, 1f, 0.5f)]
        [TestCase(1f, 1f, 1f)]
        [TestCase(2f, 1f, 1f)]
        [TestCase(0.25f, 0.5f, 0.5f)]
        [TestCase(0.1f, 0f, 1f)] // 所要時間 0 なら即完了
        public void Progress(float elapsed, float duration, float expected)
        {
            Assert.AreEqual(expected, Easing.Progress(elapsed, duration), Tol);
        }

        [Test]
        public void EasedLerp_ScaleFrom1To3()
        {
            Assert.AreEqual(1f, Easing.EasedLerp(1f, 3f, 0f), Tol);
            Assert.AreEqual(2f, Easing.EasedLerp(1f, 3f, 0.5f), Tol);
            Assert.AreEqual(3f, Easing.EasedLerp(1f, 3f, 1f), Tol);
        }
    }

    /// <summary>SPEC 4.2 再配置、4.3① 引き寄せ。</summary>
    public class RelocationMathTests
    {
        [Test]
        public void HorizontalDirection_DropsY()
        {
            // 斜め下 45° を見ている：(0,−1,1) → 水平は (0,0,1)
            Assert.IsTrue(RelocationMath.TryHorizontalDirection(new Vector3(0, -1, 1), new Vector3(0, 1, 1), out Vector3 dir));
            VecAssert.Near(new Vector3(0, 0, 1), dir);
        }

        [Test]
        public void HorizontalDirection_IsNormalized()
        {
            Assert.IsTrue(RelocationMath.TryHorizontalDirection(new Vector3(3, 2, 4), new Vector3(0, 1, 0), out Vector3 dir));
            VecAssert.Near(new Vector3(0.6f, 0, 0.8f), dir); // (3,4) / 5
        }

        [Test]
        public void HorizontalDirection_LookingStraightDown_UsesUp()
        {
            // 真下を見ていると、HMD の上方向が顔の前（+z）を指す
            Assert.IsTrue(RelocationMath.TryHorizontalDirection(new Vector3(0, -1, 0), new Vector3(0, 0, 1), out Vector3 dir));
            VecAssert.Near(new Vector3(0, 0, 1), dir);
        }

        [Test]
        public void HorizontalDirection_LookingStraightUp_UsesMinusUp()
        {
            // 真上を見ていると、HMD の上方向は後ろ（−z）を指す → 前は +z
            Assert.IsTrue(RelocationMath.TryHorizontalDirection(new Vector3(0, 1, 0), new Vector3(0, 0, -1), out Vector3 dir));
            VecAssert.Near(new Vector3(0, 0, 1), dir);
        }

        [Test]
        public void HorizontalDirection_Undetermined_ReturnsFalse()
        {
            Assert.IsFalse(RelocationMath.TryHorizontalDirection(new Vector3(0, -1, 0), new Vector3(0, 1, 0), out _));
        }

        [Test]
        public void RelocatePosition_HeadPlusDTimesDirection()
        {
            // p_h = (1, 1.6, 2)、f = +x、D = 0.6 → (1.6, 1.6, 2)
            Vector3 p = RelocationMath.RelocatePosition(new Vector3(1, 1.6f, 2), new Vector3(1, 0, 0), 0.6f);
            VecAssert.Near(new Vector3(1.6f, 1.6f, 2), p);
        }

        [Test]
        public void RelocatePosition_NormalizesDirection()
        {
            // 長さ 5 の f でも D の位置：(0,0,0) + 0.6 × (3,0,4)/5 = (0.36, 0, 0.48)
            Vector3 p = RelocationMath.RelocatePosition(new Vector3(0, 0, 0), new Vector3(3, 0, 4), 0.6f);
            VecAssert.Near(new Vector3(0.36f, 0, 0.48f), p);
        }

        [Test]
        public void ShiftedPosition_StepsFurther()
        {
            Vector3 basePos = new Vector3(0, 1.6f, 0.6f);
            Vector3 dir = new Vector3(0, 0, 1);
            VecAssert.Near(basePos, RelocationMath.ShiftedPosition(basePos, dir, 0.1f, 0));
            VecAssert.Near(new Vector3(0, 1.6f, 0.7f), RelocationMath.ShiftedPosition(basePos, dir, 0.1f, 1));
            VecAssert.Near(new Vector3(0, 1.6f, 0.9f), RelocationMath.ShiftedPosition(basePos, dir, 0.1f, 3));
            Assert.Throws<ArgumentOutOfRangeException>(() => RelocationMath.ShiftedPosition(basePos, dir, 0.1f, -1));
        }

        [Test]
        public void PullTarget_UsesDWhenAtLeastDmin()
        {
            // D = 0.6 ≥ dmin = 0.4 → 0.6 の位置
            Vector3 pt = RelocationMath.PullTarget(new Vector3(0, 1.5f, 0), new Vector3(0, 0, 1), 0.6f, 0.4f);
            VecAssert.Near(new Vector3(0, 1.5f, 0.6f), pt);
        }

        [Test]
        public void PullTarget_UsesDminWhenDIsSmaller()
        {
            // D = 0.3 < dmin = 0.4 → 0.4 の位置
            Vector3 pt = RelocationMath.PullTarget(new Vector3(0, 1.5f, 0), new Vector3(0, 0, 1), 0.3f, 0.4f);
            VecAssert.Near(new Vector3(0, 1.5f, 0.4f), pt);
            Assert.AreEqual(0.4f, RelocationMath.PullDistance(0.3f, 0.4f), 1e-6f);
        }

        [Test]
        public void PullPosition_FollowsEasing()
        {
            Vector3 p0 = new Vector3(0, 1, 0);
            Vector3 pt = new Vector3(0, 1.5f, 1);
            VecAssert.Near(p0, RelocationMath.PullPosition(p0, pt, 0f));
            VecAssert.Near(pt, RelocationMath.PullPosition(p0, pt, 1f));
            VecAssert.Near(new Vector3(0, 1.25f, 0.5f), RelocationMath.PullPosition(p0, pt, 0.5f));          // e = 0.5
            VecAssert.Near(new Vector3(0, 1.078125f, 0.15625f), RelocationMath.PullPosition(p0, pt, 0.25f)); // e = 0.15625
        }
    }

    /// <summary>SPEC 4.3 拡大率と時間。</summary>
    public class EmphasisMathTests
    {
        private const float Tol = 1e-4f;

        [Test]
        public void RawScale_Phi90()
        {
            // tan45° = 1：s = 2 × 0.6 × 1 / 0.3 = 4
            Assert.AreEqual(4f, EmphasisMath.RawScaleFactor(0.6f, 90f, 0.3f), Tol);
        }

        [Test]
        public void RawScale_Phi60()
        {
            // tan30° = 1/√3：D = √3、w = 1 → s = 2 × √3 / √3 / 1 = 2
            Assert.AreEqual(2f, EmphasisMath.RawScaleFactor((float)Math.Sqrt(3), 60f, 1f), Tol);
        }

        [Test]
        public void RawScale_SpecDefaults()
        {
            // D = 0.6、φ = 30°、w = 0.1：2 × 0.6 × tan15° / 0.1 = 12 × 0.267949 = 3.21539
            Assert.AreEqual(3.21539f, EmphasisMath.RawScaleFactor(0.6f, 30f, 0.1f), Tol);
        }

        [Test]
        public void Scale_ClampedToAtLeastOne()
        {
            // w = 2.4：2 × 0.6 × 1 / 2.4 = 0.5 → 1 に制限
            Assert.AreEqual(0.5f, EmphasisMath.RawScaleFactor(0.6f, 90f, 2.4f), Tol);
            Assert.AreEqual(1f, EmphasisMath.ScaleFactor(0.6f, 90f, 2.4f), Tol);
            Assert.AreEqual(4f, EmphasisMath.ScaleFactor(0.6f, 90f, 0.3f), Tol);
        }

        [Test]
        public void Scale_InvalidInputs_Throw()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => EmphasisMath.RawScaleFactor(0.6f, 30f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => EmphasisMath.RawScaleFactor(0.6f, 0f, 0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => EmphasisMath.RawScaleFactor(0.6f, 180f, 0.1f));
        }

        [Test]
        public void BoundingSphereDiameter_IsDiagonal()
        {
            Assert.AreEqual(5f, EmphasisMath.BoundingSphereDiameter(new Vector3(3, 4, 0)), Tol);
            Assert.AreEqual(7f, EmphasisMath.BoundingSphereDiameter(new Vector3(2, 3, 6)), Tol);
        }

        [Test]
        public void RotationDuration_360OverOmega()
        {
            Assert.AreEqual(8f, EmphasisMath.RotationDurationSec(45f), Tol);
            Assert.AreEqual(4f, EmphasisMath.RotationDurationSec(90f), Tol);
            Assert.Throws<ArgumentOutOfRangeException>(() => EmphasisMath.RotationDurationSec(0f));
        }

        [Test]
        public void RotationAngle_IsLinearAndCapped()
        {
            Assert.AreEqual(0f, EmphasisMath.RotationAngleDeg(0f, 45f), Tol);
            Assert.AreEqual(90f, EmphasisMath.RotationAngleDeg(2f, 45f), Tol);
            Assert.AreEqual(360f, EmphasisMath.RotationAngleDeg(8f, 45f), Tol);
            Assert.AreEqual(360f, EmphasisMath.RotationAngleDeg(9f, 45f), Tol);
        }

        [Test]
        public void TotalDuration_WithoutObserve()
        {
            // SPEC の初期値：1.0 + 0.5 + 360/45 + 1.0 = 10.5 秒
            Assert.AreEqual(10.5f, EmphasisMath.TotalDurationSec(1.0f, 0.5f, 45f, false, 3f, 1.0f), Tol);
        }

        [Test]
        public void TotalDuration_WithObserve()
        {
            // 観察 3 秒を足して 13.5 秒
            Assert.AreEqual(13.5f, EmphasisMath.TotalDurationSec(1.0f, 0.5f, 45f, true, 3f, 1.0f), Tol);
        }
    }
}
