using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using VRClass.Core;

namespace VRClass.Tests
{
    /// <summary>SPEC 4.1 注視判定（θth 20°、t_hold 0.3 秒、t_timeout 3 秒、N 2 回）。</summary>
    public class GazeJudgeLogicTests
    {
        private static GazeJudgeLogic Make() => new GazeJudgeLogic(20f, 0.3, 3.0, 2);

        [Test]
        public void LookingFor0_3Sec_IsOk()
        {
            var g = Make();
            Assert.AreEqual(GazeEvent.None, g.Tick(0.1, 10f));
            Assert.AreEqual(GazeEvent.None, g.Tick(0.1, 10f));
            Assert.AreEqual(GazeEvent.Ok, g.Tick(0.1, 10f)); // 0.3 秒
            Assert.IsTrue(g.IsFinished);
            Assert.AreEqual(GazeEvent.None, g.Tick(0.1, 10f));
        }

        [Test]
        public void ThresholdIsInclusive()
        {
            var g = Make();
            Assert.AreEqual(GazeEvent.Ok, g.Tick(0.3, 20f)); // θ = θth は見ている
        }

        [Test]
        public void LookingAway_ResetsHold()
        {
            var g = Make();
            g.Tick(0.2, 10f);
            g.Tick(0.1, 30f); // 外れた → 数え直し
            Assert.AreEqual(0, g.HoldElapsed, 1e-9);
            Assert.AreEqual(GazeEvent.None, g.Tick(0.2, 10f));
            Assert.AreEqual(GazeEvent.Ok, g.Tick(0.1, 10f));
        }

        [Test]
        public void NaN_IsNotLooking()
        {
            var g = Make();
            Assert.AreEqual(GazeEvent.None, g.Tick(1.0, float.NaN));
            Assert.AreEqual(0, g.HoldElapsed, 1e-9);
        }

        [Test]
        public void NotLooking_TimeoutTwice_ThenFail()
        {
            var g = Make();
            var events = new List<GazeEvent>();
            for (int i = 0; i < 100 && !g.IsFinished; i++)
            {
                GazeEvent e = g.Tick(0.1, 90f);
                if (e != GazeEvent.None) events.Add(e);
            }

            // 3 秒で再配置 1 回目、6 秒で 2 回目、9 秒で失敗
            CollectionAssert.AreEqual(new[] { GazeEvent.Timeout, GazeEvent.Timeout, GazeEvent.Fail }, events);
            Assert.AreEqual(2, g.Relocations);
            Assert.AreEqual(9.0, g.TotalElapsed, 1e-6);
            Assert.AreEqual(GazeEvent.Fail, g.Result);
        }

        [Test]
        public void LookAfterRelocation_IsOk()
        {
            var g = Make();
            Assert.AreEqual(GazeEvent.Timeout, g.Tick(3.0, 90f));
            Assert.AreEqual(0, g.WindowElapsed, 1e-9);
            Assert.AreEqual(GazeEvent.Ok, g.Tick(0.3, 5f));
            Assert.AreEqual(1, g.Relocations);
        }

        [Test]
        public void HoldInProgress_StillTimesOut()
        {
            // 2.8 秒目から見始めても、3 秒で時間切れ（2026-11-10 決定）
            var g = Make();
            g.Tick(2.8, 90f);
            Assert.AreEqual(GazeEvent.None, g.Tick(0.1, 5f));
            Assert.AreEqual(GazeEvent.Timeout, g.Tick(0.1, 5f));
        }

        [Test]
        public void ZeroRelocations_FailsAtFirstTimeout()
        {
            var g = new GazeJudgeLogic(20f, 0.3, 3.0, 0);
            Assert.AreEqual(GazeEvent.Fail, g.Tick(3.0, 90f));
        }
    }

    public class FindFreePositionTests
    {
        [Test]
        public void FreeAtBase_NoShift()
        {
            Vector3 p = RelocationMath.FindFreePosition(new Vector3(0, 1.6f, 0.6f), new Vector3(0, 0, 1), 0.1f, 10,
                _ => false, out int shifts, out bool exhausted);
            VecAssert.Near(new Vector3(0, 1.6f, 0.6f), p);
            Assert.AreEqual(0, shifts);
            Assert.IsFalse(exhausted);
        }

        [Test]
        public void Occupied_ShiftsBackUntilFree()
        {
            // z < 0.85 は他の部品がある → 0.6, 0.7, 0.8 は重なり、0.9 で空く（3 回ずらす）
            Vector3 p = RelocationMath.FindFreePosition(new Vector3(0, 1.6f, 0.6f), new Vector3(0, 0, 1), 0.1f, 10,
                q => q.z < 0.85f, out int shifts, out bool exhausted);
            VecAssert.Near(new Vector3(0, 1.6f, 0.9f), p);
            Assert.AreEqual(3, shifts);
            Assert.IsFalse(exhausted);
        }

        [Test]
        public void AlwaysOccupied_ReturnsLastAndExhausted()
        {
            Vector3 p = RelocationMath.FindFreePosition(new Vector3(0, 1.6f, 0.6f), new Vector3(0, 0, 1), 0.1f, 10,
                _ => true, out int shifts, out bool exhausted);
            VecAssert.Near(new Vector3(0, 1.6f, 1.6f), p); // 0.6 + 10 × 0.1
            Assert.AreEqual(10, shifts);
            Assert.IsTrue(exhausted);
        }
    }
}
