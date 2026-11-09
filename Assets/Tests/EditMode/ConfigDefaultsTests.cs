using NUnit.Framework;
using UnityEngine;
using VRClass.Core;
using VRClass.Experiment;

namespace VRClass.Tests
{
    /// <summary>ExperimentConfig の初期値が SPEC のとおりで、そこから Ttotal が求まることを確かめる。</summary>
    public class ConfigDefaultsTests
    {
        [Test]
        public void TotalDuration_FromDefaultConfig_Is10_5()
        {
            var c = ScriptableObject.CreateInstance<ExperimentConfig>();
            try
            {
                float total = EmphasisMath.TotalDurationSec(c.pullDurationSec, c.scaleDurationSec,
                    c.rotationSpeedDegPerSec, c.useObservePhase, c.observeDurationSec, c.returnDurationSec);
                Assert.AreEqual(10.5f, total, 1e-4f);

                c.useObservePhase = true;
                total = EmphasisMath.TotalDurationSec(c.pullDurationSec, c.scaleDurationSec,
                    c.rotationSpeedDegPerSec, c.useObservePhase, c.observeDurationSec, c.returnDurationSec);
                Assert.AreEqual(13.5f, total, 1e-4f);
            }
            finally
            {
                Object.DestroyImmediate(c);
            }
        }

        [Test]
        public void SpecDefaults()
        {
            var c = ScriptableObject.CreateInstance<ExperimentConfig>();
            try
            {
                Assert.AreEqual(20f, c.gazeAngleThresholdDeg);
                Assert.AreEqual(0.3f, c.gazeHoldSec);
                Assert.AreEqual(3f, c.gazeTimeoutSec);
                Assert.AreEqual(2, c.maxRelocations);
                Assert.AreEqual(0.6f, c.frontDistance);
                Assert.AreEqual(0.1f, c.relocateShiftStep);
                Assert.AreEqual(1.0f, c.pullDurationSec);
                Assert.AreEqual(0.4f, c.minHeadDistance);
                Assert.AreEqual(0.5f, c.scaleDurationSec);
                Assert.AreEqual(30f, c.targetViewAngleDeg);
                Assert.AreEqual(45f, c.rotationSpeedDegPerSec);
                Assert.IsFalse(c.useObservePhase);
                Assert.AreEqual(3f, c.observeDurationSec);
                Assert.AreEqual(1.0f, c.returnDurationSec);
                Assert.AreEqual(15f, c.socketAngleToleranceDeg);
                Assert.AreEqual(10f, c.headLogHz);
            }
            finally
            {
                Object.DestroyImmediate(c);
            }
        }
    }
}
