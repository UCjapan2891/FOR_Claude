using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using VRClass.Core;

namespace VRClass.Tests
{
    /// <summary>SPEC 3章の状態遷移。</summary>
    public class LessonFlowTests
    {
        private static LessonFlow Make(params bool[] keyPoints)
        {
            var stages = new List<FlowStage>();
            foreach (bool k in keyPoints)
            {
                stages.Add(new FlowStage(CompletionType.Detach, k));
            }

            return new LessonFlow(stages);
        }

        [Test]
        public void Start_GoesToNarrationOfStage1()
        {
            var f = Make(false, false);
            Assert.AreEqual(LessonState.NotStarted, f.State);
            Assert.IsNull(f.StageNumber);

            f.Start();
            Assert.AreEqual(LessonState.Narration, f.State);
            Assert.AreEqual(0, f.StageIndex);
            Assert.AreEqual(1, f.StageNumber);
        }

        [Test]
        public void NormalStage_SkipsKeyPoint()
        {
            var f = Make(false, false);
            f.Start();
            f.NarrationFinished();
            Assert.AreEqual(LessonState.Accepting, f.State);

            f.OperationCompleted();
            Assert.AreEqual(LessonState.Narration, f.State); // 8. まだ最後でない → i+1 で 2 へ
            Assert.AreEqual(2, f.StageNumber);
        }

        [Test]
        public void KeyStage_GoesThroughKeyPoint()
        {
            var f = Make(true, false);
            f.Start();
            f.NarrationFinished();
            f.OperationCompleted();
            Assert.AreEqual(LessonState.KeyPoint, f.State);
            Assert.AreEqual(1, f.StageNumber);

            f.KeyPointFinished();
            Assert.AreEqual(LessonState.Narration, f.State);
            Assert.AreEqual(2, f.StageNumber);
        }

        [Test]
        public void LastStage_GoesToAssemblyCheck_ThenFinished()
        {
            var f = Make(false, true);
            f.Start();
            f.NarrationFinished();
            f.OperationCompleted(); // 段階1
            f.NarrationFinished();
            f.OperationCompleted(); // 段階2（要点）
            Assert.AreEqual(LessonState.KeyPoint, f.State);
            f.KeyPointFinished();
            Assert.AreEqual(LessonState.AssemblyCheck, f.State);
            Assert.AreEqual(2, f.StageNumber);

            f.AssemblyConfirmed();
            Assert.AreEqual(LessonState.Finished, f.State);
        }

        [Test]
        public void FullHddTestSequence_VisitsStatesInOrder()
        {
            // HDD_Test：要点は段階2と4
            var f = Make(false, true, false, true, false, false);
            var visited = new List<string>();
            f.Start();
            while (f.State != LessonState.AssemblyCheck)
            {
                visited.Add($"{f.StageNumber}:{f.State}");
                switch (f.State)
                {
                    case LessonState.Narration: f.NarrationFinished(); break;
                    case LessonState.Accepting: f.OperationCompleted(); break;
                    case LessonState.KeyPoint: f.KeyPointFinished(); break;
                }
            }

            CollectionAssert.AreEqual(new[]
            {
                "1:Narration", "1:Accepting",
                "2:Narration", "2:Accepting", "2:KeyPoint",
                "3:Narration", "3:Accepting",
                "4:Narration", "4:Accepting", "4:KeyPoint",
                "5:Narration", "5:Accepting",
                "6:Narration", "6:Accepting",
            }, visited);
        }

        [Test]
        public void WrongOrder_Throws()
        {
            var f = Make(false);
            Assert.Throws<InvalidOperationException>(() => f.NarrationFinished()); // 開始前
            f.Start();
            Assert.Throws<InvalidOperationException>(() => f.OperationCompleted()); // 音声中は完了できない
            Assert.Throws<InvalidOperationException>(() => f.KeyPointFinished());
            Assert.Throws<InvalidOperationException>(() => f.AssemblyConfirmed());
            Assert.Throws<InvalidOperationException>(() => f.Start()); // 2回目の開始
        }

        [Test]
        public void RestartStage_ReturnsToNarrationOfSameStage()
        {
            var f = Make(true, false);
            f.Start();
            f.NarrationFinished();
            f.OperationCompleted();
            Assert.AreEqual(LessonState.KeyPoint, f.State);

            f.RestartStage();
            Assert.AreEqual(LessonState.Narration, f.State);
            Assert.AreEqual(1, f.StageNumber);
        }

        [Test]
        public void EmptyStages_Throws()
        {
            Assert.Throws<ArgumentException>(() => new LessonFlow(new List<FlowStage>()));
        }
    }

    /// <summary>SPEC 5.1 向きのずれ。</summary>
    public class OrientationMathTests
    {
        private const float Tol = 1e-3f;

        // 鉛直軸（Y）まわりに deg 度回す四元数
        private static Quaternion AroundY(float deg)
        {
            double h = deg * Math.PI / 360.0;
            return new Quaternion(0f, (float)Math.Sin(h), 0f, (float)Math.Cos(h));
        }

        // X 軸まわりに deg 度回す四元数
        private static Quaternion AroundX(float deg)
        {
            double h = deg * Math.PI / 360.0;
            return new Quaternion((float)Math.Sin(h), 0f, 0f, (float)Math.Cos(h));
        }

        private static readonly Quaternion Identity = new Quaternion(0f, 0f, 0f, 1f);

        [TestCase(0f)]
        [TestCase(10f)]
        [TestCase(15f)]
        [TestCase(90f)]
        [TestCase(180f)]
        public void AngleAroundY_EqualsRotation(float deg)
        {
            Assert.AreEqual(deg, OrientationMath.AngleDeg(Identity, AroundY(deg)), Tol);
        }

        [Test]
        public void SignOfQuaternion_DoesNotMatter()
        {
            // q と −q は同じ回転
            Quaternion q = AroundY(30f);
            var negated = new Quaternion(-q.x, -q.y, -q.z, -q.w);
            Assert.AreEqual(0f, OrientationMath.AngleDeg(q, negated), Tol);
        }

        [Test]
        public void Rotate_90AroundY_MapsXToMinusZ()
        {
            Vector3 v = OrientationMath.Rotate(AroundY(90f), new Vector3(1, 0, 0));
            VecAssert.Near(new Vector3(0, 0, -1), v);
        }

        [Test]
        public void SymmetryY_IgnoresRotationAroundY()
        {
            // 円盤（対称軸 Y）を Y まわりに 90° 回しても、ずれは 0°
            Assert.AreEqual(0f, OrientationMath.AngleDeg(Identity, AroundY(90f), SymmetryAxis.Y), Tol);
            // 対称軸でない X を指定すると 90° のまま
            Assert.AreEqual(90f, OrientationMath.AngleDeg(Identity, AroundY(90f), SymmetryAxis.X), Tol);
        }

        [Test]
        public void SymmetryY_StillDetectsTilt()
        {
            // 円盤を X まわりに 20° 傾けると、Y 軸が 20° ずれる
            Assert.AreEqual(20f, OrientationMath.AngleDeg(Identity, AroundX(20f), SymmetryAxis.Y), Tol);
        }

        [TestCase(14.9f, true)]
        [TestCase(15f, true)]
        [TestCase(15.1f, false)]
        public void Tolerance15(float angle, bool expected)
        {
            Assert.AreEqual(expected, OrientationMath.IsWithinTolerance(angle, 15f));
        }
    }

    public class InputModeRulesTests
    {
        [TestCase(InputMode.ControllerOnly, true, false)]
        [TestCase(InputMode.Both, true, true)]
        [TestCase(InputMode.KeyboardMouseOnly, false, true)]
        public void Allows(InputMode mode, bool controller, bool keyboardMouse)
        {
            Assert.AreEqual(controller, InputModeRules.AllowsController(mode));
            Assert.AreEqual(keyboardMouse, InputModeRules.AllowsKeyboardMouse(mode));
        }

        [Test]
        public void Next_Cycles()
        {
            Assert.AreEqual(InputMode.Both, InputModeRules.Next(InputMode.ControllerOnly));
            Assert.AreEqual(InputMode.KeyboardMouseOnly, InputModeRules.Next(InputMode.Both));
            Assert.AreEqual(InputMode.ControllerOnly, InputModeRules.Next(InputMode.KeyboardMouseOnly));
        }
    }
}
