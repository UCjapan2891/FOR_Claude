using System;
using System.Collections.Generic;

namespace VRClass.Core
{
    /// <summary>固定の判定に使うソケットの状態。</summary>
    public readonly struct SocketLockInput
    {
        public readonly string SocketId;

        /// <summary>正しい部品が付いているか。</summary>
        public readonly bool FilledCorrectly;

        /// <summary>先に置くべきソケット（この部品の下にある部品）の id。</summary>
        public readonly IReadOnlyList<string> Prerequisites;

        public SocketLockInput(string socketId, bool filledCorrectly, IReadOnlyList<string> prerequisites)
        {
            SocketId = socketId;
            FilledCorrectly = filledCorrectly;
            Prerequisites = prerequisites ?? Array.Empty<string>();
        }
    }

    /// <summary>
    /// 正しい位置に置いた部品を固定するかの判定（2026-11-10 決定）。
    /// - 正しい部品が付いていて、先に置くべきソケットが全部固定済みなら固定する
    /// - 先に置くべき部品が足りなければ固定しない（上の部品を先に置いても外せる。詰み回避）
    /// - 下の部品が後から固定されたら、その上に付いている部品も固定する（くり返し評価する）
    /// 存在しない id を先に置くべきソケットに指定すると、その部品は固定されない。
    /// </summary>
    public static class AssemblyLockRules
    {
        /// <summary>固定するソケットの id を返す。lockEnabled = false なら空。</summary>
        public static HashSet<string> ComputeLocked(IReadOnlyList<SocketLockInput> sockets, bool lockEnabled)
        {
            var locked = new HashSet<string>();
            if (!lockEnabled || sockets == null)
            {
                return locked;
            }

            bool changed = true;
            while (changed)
            {
                changed = false;
                foreach (SocketLockInput s in sockets)
                {
                    if (!s.FilledCorrectly || locked.Contains(s.SocketId))
                    {
                        continue;
                    }

                    bool prerequisitesLocked = true;
                    foreach (string p in s.Prerequisites)
                    {
                        if (!locked.Contains(p))
                        {
                            prerequisitesLocked = false;
                            break;
                        }
                    }

                    if (prerequisitesLocked)
                    {
                        locked.Add(s.SocketId);
                        changed = true;
                    }
                }
            }

            return locked;
        }

        /// <summary>先に置くべきソケットに、存在しない id がないか調べる（設定ミスの検出）。</summary>
        public static List<string> FindUnknownPrerequisites(IReadOnlyList<SocketLockInput> sockets)
        {
            var ids = new HashSet<string>();
            foreach (SocketLockInput s in sockets)
            {
                ids.Add(s.SocketId);
            }

            var unknown = new List<string>();
            foreach (SocketLockInput s in sockets)
            {
                foreach (string p in s.Prerequisites)
                {
                    if (!ids.Contains(p))
                    {
                        unknown.Add($"{s.SocketId} → {p}");
                    }
                }
            }

            return unknown;
        }
    }
}
