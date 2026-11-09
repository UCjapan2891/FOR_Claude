using System;
using NUnit.Framework;
using VRClass.Core;

namespace VRClass.Tests
{
    public class LessonClockTests
    {
        [Test]
        public void Tick_Accumulates()
        {
            var c = new LessonClock(excludePause: false);
            c.Tick(0.5);
            c.Tick(0.25);
            Assert.AreEqual(0.75, c.Elapsed, 1e-12);
        }

        [Test]
        public void Pause_Included_WhenExcludePauseIsFalse()
        {
            var c = new LessonClock(excludePause: false);
            c.Tick(1.0);
            c.Pause();
            c.Tick(2.0);
            c.Resume();
            c.Tick(1.0);
            Assert.AreEqual(4.0, c.Elapsed, 1e-12);
        }

        [Test]
        public void Pause_Excluded_WhenExcludePauseIsTrue()
        {
            var c = new LessonClock(excludePause: true);
            c.Tick(1.0);
            c.Pause();
            c.Tick(2.0);
            c.Resume();
            c.Tick(1.0);
            Assert.AreEqual(2.0, c.Elapsed, 1e-12);
        }

        [Test]
        public void Reset_ClearsTimeAndPause()
        {
            var c = new LessonClock(excludePause: true);
            c.Tick(3.0);
            c.Pause();
            c.Reset();
            Assert.AreEqual(0.0, c.Elapsed);
            Assert.IsFalse(c.IsPaused);
            c.Tick(1.0);
            Assert.AreEqual(1.0, c.Elapsed, 1e-12);
        }

        [Test]
        public void Tick_Negative_Throws()
        {
            var c = new LessonClock(false);
            Assert.Throws<ArgumentOutOfRangeException>(() => c.Tick(-0.1));
            Assert.Throws<ArgumentOutOfRangeException>(() => c.Tick(double.NaN));
        }
    }

    public class IntervalTimerTests
    {
        [Test]
        public void TenHz_FiresEveryTenthSecond()
        {
            var t = new IntervalTimer(10);
            Assert.AreEqual(0.1, t.IntervalSeconds, 1e-12);

            int fired = 0;
            // 90 fps で 1 秒（実機のフレームに近い刻み）
            for (int i = 0; i < 90; i++)
            {
                if (t.Tick(1.0 / 90.0))
                {
                    fired++;
                }
            }

            Assert.AreEqual(10, fired);
        }

        [Test]
        public void LongFrame_FiresOnlyOnce()
        {
            var t = new IntervalTimer(10);
            Assert.IsTrue(t.Tick(0.55)); // 0.5 秒以上止まっても1回だけ
            Assert.IsFalse(t.Tick(0.04)); // 余り 0.05 + 0.04 = 0.09
            Assert.IsTrue(t.Tick(0.02));
        }

        [Test]
        public void InvalidFrequency_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new IntervalTimer(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new IntervalTimer(-10));
        }
    }
}
