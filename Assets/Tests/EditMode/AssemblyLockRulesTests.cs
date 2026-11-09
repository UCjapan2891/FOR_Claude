using System.Collections.Generic;
using NUnit.Framework;
using VRClass.Core;

namespace VRClass.Tests
{
    /// <summary>正しい位置に置いた部品の固定と、詰み回避。</summary>
    public class AssemblyLockRulesTests
    {
        // HDD_Test：プラッタ → アーム → カバー（カバーの下にプラッタとアーム）
        private static List<SocketLockInput> Hdd(bool platter, bool arm, bool cover)
        {
            return new List<SocketLockInput>
            {
                new SocketLockInput("platter", platter, new string[0]),
                new SocketLockInput("arm", arm, new[] { "platter" }),
                new SocketLockInput("cover", cover, new[] { "platter", "arm" }),
            };
        }

        [Test]
        public void NothingPlaced_NothingLocked()
        {
            CollectionAssert.IsEmpty(AssemblyLockRules.ComputeLocked(Hdd(false, false, false), true));
        }

        [Test]
        public void BottomPart_LocksImmediately()
        {
            CollectionAssert.AreEquivalent(new[] { "platter" }, AssemblyLockRules.ComputeLocked(Hdd(true, false, false), true));
        }

        [Test]
        public void TopPartFirst_IsNotLocked()
        {
            // カバーを先に置いても固定しない（外してアームを置ける）
            CollectionAssert.AreEquivalent(new[] { "platter" }, AssemblyLockRules.ComputeLocked(Hdd(true, false, true), true));
        }

        [Test]
        public void TopPartBeforeAnything_IsNotLocked()
        {
            CollectionAssert.IsEmpty(AssemblyLockRules.ComputeLocked(Hdd(false, false, true), true));
        }

        [Test]
        public void ArmWithoutPlatter_IsNotLocked()
        {
            CollectionAssert.IsEmpty(AssemblyLockRules.ComputeLocked(Hdd(false, true, false), true));
        }

        [Test]
        public void LaterPrerequisite_LocksPartsAbove()
        {
            // アームとカバーが先に付いていて、最後にプラッタを置くと全部固定
            CollectionAssert.AreEquivalent(new[] { "platter", "arm", "cover" },
                AssemblyLockRules.ComputeLocked(Hdd(true, true, true), true));
        }

        [Test]
        public void Disabled_LocksNothing()
        {
            CollectionAssert.IsEmpty(AssemblyLockRules.ComputeLocked(Hdd(true, true, true), false));
        }

        [Test]
        public void UnknownPrerequisite_NeverLocks_AndIsReported()
        {
            var sockets = new List<SocketLockInput>
            {
                new SocketLockInput("a", true, new[] { "typo" }),
            };
            CollectionAssert.IsEmpty(AssemblyLockRules.ComputeLocked(sockets, true));
            CollectionAssert.AreEqual(new[] { "a → typo" }, AssemblyLockRules.FindUnknownPrerequisites(sockets));
        }

        [Test]
        public void Cycle_NeverLocks()
        {
            var sockets = new List<SocketLockInput>
            {
                new SocketLockInput("a", true, new[] { "b" }),
                new SocketLockInput("b", true, new[] { "a" }),
            };
            CollectionAssert.IsEmpty(AssemblyLockRules.ComputeLocked(sockets, true));
        }
    }
}
