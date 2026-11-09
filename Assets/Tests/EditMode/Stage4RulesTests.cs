using NUnit.Framework;
using VRClass.Core;

namespace VRClass.Tests
{
    /// <summary>SPEC 5.3 自動タイマー。</summary>
    public class AutoTimerTests
    {
        [Test]
        public void Disabled_DoesNothing()
        {
            var t = new AutoTimer(false, 60, 120);
            Assert.AreEqual(AutoTimerAction.None, t.Tick(500));
        }

        [Test]
        public void HintAt60_CompleteAt120_EachOnce()
        {
            var t = new AutoTimer(true, 60, 120);
            Assert.AreEqual(AutoTimerAction.None, t.Tick(59.9));
            Assert.AreEqual(AutoTimerAction.Hint, t.Tick(0.1));    // 60.0 秒
            Assert.AreEqual(AutoTimerAction.None, t.Tick(30));     // ヒントは1回だけ
            Assert.AreEqual(AutoTimerAction.None, t.Tick(29.9));
            Assert.AreEqual(AutoTimerAction.Complete, t.Tick(0.1)); // 120.0 秒
            Assert.AreEqual(AutoTimerAction.None, t.Tick(100));    // 完了も1回だけ
        }

        [Test]
        public void BothCrossedInOneTick_ReturnsComplete()
        {
            var t = new AutoTimer(true, 60, 120);
            Assert.AreEqual(AutoTimerAction.Complete, t.Tick(130));
            Assert.IsTrue(t.HintFired);
        }

        [Test]
        public void Restart_ClearsState()
        {
            var t = new AutoTimer(true, 1, 2);
            t.Tick(1.5);
            t.Restart();
            Assert.AreEqual(0, t.Elapsed);
            Assert.AreEqual(AutoTimerAction.Hint, t.Tick(1.0));
        }
    }

    /// <summary>SPEC 5.3 HMD を外した・トラッキングを失ったときの検出。</summary>
    public class PresenceDetectorTests
    {
        [Test]
        public void ReportsAfterGrace_Once()
        {
            var d = new PresenceDetector(0.5);
            Assert.AreEqual(PresenceIssue.None, d.Update(0.3, false, true, true));
            Assert.AreEqual(PresenceIssue.HmdRemoved, d.Update(0.2, false, true, true)); // 0.5 秒
            Assert.AreEqual(PresenceIssue.None, d.Update(1.0, false, true, true));       // 続いていても1回だけ
        }

        [Test]
        public void ShortGlitch_IsIgnored()
        {
            var d = new PresenceDetector(0.5);
            Assert.AreEqual(PresenceIssue.None, d.Update(0.4, true, false, true));
            Assert.AreEqual(PresenceIssue.None, d.Update(0.1, true, true, true)); // 戻った
            Assert.AreEqual(PresenceIssue.None, d.Update(0.4, true, false, true)); // 数え直し
        }

        [Test]
        public void ReArmsAfterRecovery()
        {
            var d = new PresenceDetector(0.5);
            Assert.AreEqual(PresenceIssue.TrackingLost, d.Update(0.6, true, false, true));
            d.Update(0.1, true, true, true);
            Assert.AreEqual(PresenceIssue.FocusLost, d.Update(0.6, true, true, false));
        }

        [Test]
        public void UnknownValues_AreIgnored()
        {
            var d = new PresenceDetector(0.5);
            Assert.AreEqual(PresenceIssue.None, d.Update(10, null, null, null));
        }

        [TestCase(false, false, false, PresenceIssue.HmdRemoved)]
        [TestCase(true, false, false, PresenceIssue.FocusLost)]
        [TestCase(true, false, true, PresenceIssue.TrackingLost)]
        [TestCase(true, true, true, PresenceIssue.None)]
        public void Priority(bool present, bool tracked, bool focused, PresenceIssue expected)
        {
            Assert.AreEqual(expected, PresenceDetector.Classify(present, tracked, focused));
        }
    }
}
