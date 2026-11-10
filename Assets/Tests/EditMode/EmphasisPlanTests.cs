using System.Collections.Generic;
using NUnit.Framework;
using VRClass.Core;

namespace VRClass.Tests
{
    /// <summary>SPEC 3章 7・4.1・4.3：要点の演出の選び方と、条件1・2の合計時間。</summary>
    public class EmphasisPlanTests
    {
        private const float Tol = 1e-4f;

        // SPEC の初期値：T1=1.0, T2=0.5, ω=45（8秒）, T_obs=3, T4=1.0
        private static EmphasisTiming Timing(bool observe = false) =>
            new EmphasisTiming(1.0f, 0.5f, 45f, observe, 3f, 1.0f);

        [Test]
        public void ChooseMode_Condition2AndGazeOk_IsSpatial()
        {
            Assert.AreEqual(EmphasisMode.TelopAndSpatial, EmphasisPlan.ChooseMode(Condition.TelopWithEmphasis, true));
        }

        [Test]
        public void ChooseMode_Condition2AndGazeFail_IsTelopOnly()
        {
            Assert.AreEqual(EmphasisMode.TelopOnly, EmphasisPlan.ChooseMode(Condition.TelopWithEmphasis, false));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ChooseMode_Condition1_IsAlwaysTelopOnly(bool gazeOk)
        {
            Assert.AreEqual(EmphasisMode.TelopOnly, EmphasisPlan.ChooseMode(Condition.TelopOnly, gazeOk));
        }

        [TestCase(false, 10.5f)]
        [TestCase(true, 13.5f)]
        public void Timing_Total_MatchesEmphasisMath(bool observe, float expected)
        {
            EmphasisTiming t = Timing(observe);
            Assert.AreEqual(expected, t.TotalSec, Tol);
            Assert.AreEqual(EmphasisMath.TotalDurationSec(1.0f, 0.5f, 45f, observe, 3f, 1.0f), t.TotalSec, Tol);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Duration_SameForAllModes(bool observe)
        {
            EmphasisTiming t = Timing(observe);
            float spatial = EmphasisPlan.DurationSec(EmphasisMode.TelopAndSpatial, t);
            float telop = EmphasisPlan.DurationSec(EmphasisMode.TelopOnly, t);
            Assert.AreEqual(t.TotalSec, spatial, Tol);
            Assert.AreEqual(t.TotalSec, telop, Tol);
        }

        [Test]
        public void Timeline_PhasesInOrder_WithoutObserve()
        {
            var tl = new EmphasisTimeline(Timing());
            CollectionAssert.AreEqual(
                new[] { EmphasisPhase.Pull, EmphasisPhase.Scale, EmphasisPhase.Rotate, EmphasisPhase.Return },
                Phases(tl));
        }

        [Test]
        public void Timeline_PhasesInOrder_WithObserve()
        {
            var tl = new EmphasisTimeline(Timing(true));
            CollectionAssert.AreEqual(
                new[] { EmphasisPhase.Pull, EmphasisPhase.Scale, EmphasisPhase.Rotate, EmphasisPhase.Observe, EmphasisPhase.Return },
                Phases(tl));
            Assert.AreEqual(8f, tl.DurationAt(2), Tol);
            Assert.AreEqual(3f, tl.DurationAt(3), Tol);
        }

        [Test]
        public void Timeline_WithoutPull_StartsWithScale()
        {
            var tl = new EmphasisTimeline(Timing(), includePull: false);
            Assert.AreEqual(EmphasisPhase.Scale, tl.PhaseAt(0));
            Assert.AreEqual(9.5f, tl.TotalSec, Tol);
        }

        [Test]
        public void Locate_FindsPhaseAndProgress()
        {
            var tl = new EmphasisTimeline(Timing());
            float p;
            Assert.AreEqual(0, tl.Locate(0f, out p));
            Assert.AreEqual(0f, p, Tol);
            Assert.AreEqual(0, tl.Locate(0.5f, out p));
            Assert.AreEqual(0.5f, p, Tol);
            Assert.AreEqual(1, tl.Locate(1.25f, out p)); // 拡大の半分
            Assert.AreEqual(0.5f, p, Tol);
            Assert.AreEqual(2, tl.Locate(5.5f, out p));  // 回転 1.5〜9.5 の半分
            Assert.AreEqual(0.5f, p, Tol);
            Assert.AreEqual(3, tl.Locate(10f, out p));   // 復帰 9.5〜10.5 の半分
            Assert.AreEqual(0.5f, p, Tol);
            Assert.AreEqual(4, tl.Locate(10.5f, out p)); // 終わり
            Assert.AreEqual(1f, p, Tol);
        }

        [Test]
        public void Locate_BoundaryBelongsToNextPhase()
        {
            var tl = new EmphasisTimeline(Timing());
            Assert.AreEqual(1, tl.Locate(1.0f, out float p));
            Assert.AreEqual(0f, p, Tol);
        }

        [Test]
        public void Locate_ZeroLengthPhaseIsSkipped()
        {
            var tl = new EmphasisTimeline(new EmphasisTiming(1f, 0f, 45f, false, 0f, 1f));
            Assert.AreEqual(2, tl.Locate(1.0f, out _)); // 拡大（0 秒）を飛ばして回転
        }

        [Test]
        public void Locate_NegativeOrNaN_IsStart()
        {
            var tl = new EmphasisTimeline(Timing());
            Assert.AreEqual(0, tl.Locate(-1f, out _));
            Assert.AreEqual(0, tl.Locate(float.NaN, out _));
        }

        [Test]
        public void Clock_IgnoresBadDelta()
        {
            var c = new EmphasisClock(1f);
            c.Tick(-1f);
            c.Tick(float.NaN);
            c.Tick(float.PositiveInfinity);
            Assert.AreEqual(0f, c.Elapsed);
            Assert.IsFalse(c.IsFinished);
            c.Tick(1f);
            Assert.IsTrue(c.IsFinished);
        }

        [Test]
        public void Clock_ZeroTotal_FinishedAtOnce()
        {
            Assert.IsTrue(new EmphasisClock(0f).IsFinished);
        }

        /// <summary>
        /// 同じフレームの刻みなら、条件1（Ttotal 待つ）と条件2（強調の段階を順に）が同じフレーム数で終わる。
        /// </summary>
        [TestCase(1f / 72f, false)]
        [TestCase(1f / 90f, false)]
        [TestCase(1f / 90f, true)]
        [TestCase(0.033f, true)]
        public void Condition1And2_FinishOnSameFrame(float dt, bool observe)
        {
            EmphasisTiming t = Timing(observe);
            int telopFrames = CountFrames(new EmphasisClock(EmphasisPlan.DurationSec(EmphasisMode.TelopOnly, t)), dt);

            var tl = new EmphasisTimeline(t);
            var clock = new EmphasisClock(tl.TotalSec);
            int spatialFrames = 0;
            int lastIndex = -1;
            while (tl.Locate(clock.Elapsed, out _) < tl.Count)
            {
                int i = tl.Locate(clock.Elapsed, out _);
                Assert.GreaterOrEqual(i, lastIndex, "段階が逆戻りしない");
                lastIndex = i;
                spatialFrames++;
                clock.Tick(dt);
            }

            Assert.AreEqual(telopFrames, spatialFrames);
            Assert.AreEqual(t.TotalSec, clock.Elapsed, dt + Tol);
        }

        [Test]
        public void Names_ForLog()
        {
            Assert.AreEqual("spatial", EmphasisPlan.ModeName(EmphasisMode.TelopAndSpatial));
            Assert.AreEqual("telop", EmphasisPlan.ModeName(EmphasisMode.TelopOnly));
            Assert.AreEqual("pull", EmphasisPlan.PhaseName(EmphasisPhase.Pull));
            Assert.AreEqual("scale", EmphasisPlan.PhaseName(EmphasisPhase.Scale));
            Assert.AreEqual("rotate", EmphasisPlan.PhaseName(EmphasisPhase.Rotate));
            Assert.AreEqual("observe", EmphasisPlan.PhaseName(EmphasisPhase.Observe));
            Assert.AreEqual("return", EmphasisPlan.PhaseName(EmphasisPhase.Return));
        }

        private static int CountFrames(EmphasisClock clock, float dt)
        {
            int frames = 0;
            while (!clock.IsFinished)
            {
                frames++;
                clock.Tick(dt);
            }

            return frames;
        }

        private static List<EmphasisPhase> Phases(EmphasisTimeline tl)
        {
            var list = new List<EmphasisPhase>();
            for (int i = 0; i < tl.Count; i++)
            {
                list.Add(tl.PhaseAt(i));
            }

            return list;
        }
    }
}
