using System.Collections.Generic;
using UnityEngine;
using VRClass.Core;
using VRClass.Experiment;

namespace VRClass.Interaction
{
    /// <summary>
    /// 正しい位置に置いた部品を固定する（2026-11-10 決定）。判定は AssemblyLockRules（Core）。
    /// ソケットに部品が付いた・外れたたびに、全ソケットの固定を計算し直す。
    /// 先に置くべき部品（下の部品）が固定済みでなければ固定しないので、上の部品を先に置いても外せる。
    /// </summary>
    public sealed class AssemblyLock : MonoBehaviour
    {
        [SerializeField] private ExperimentConfig config;

        private readonly List<PartSocket> _sockets = new List<PartSocket>();
        private readonly List<SocketLockInput> _inputs = new List<SocketLockInput>();

        private void Start()
        {
            foreach (PartSocket s in FindObjectsByType<PartSocket>(FindObjectsSortMode.None))
            {
                _sockets.Add(s);
                s.Attached += OnChanged;
                s.Detached += OnChanged;
            }

            BuildInputs();
            foreach (string u in AssemblyLockRules.FindUnknownPrerequisites(_inputs))
            {
                Debug.LogError($"先に置くべきソケットの id が見つかりません：{u}", this);
            }

            Recompute();
        }

        private void OnDestroy()
        {
            foreach (PartSocket s in _sockets)
            {
                if (s == null) continue;
                s.Attached -= OnChanged;
                s.Detached -= OnChanged;
            }
        }

        private void OnChanged(PartSocket socket, PartInteraction part)
        {
            Recompute();
        }

        /// <summary>全ソケットの固定を計算し直す。</summary>
        public void Recompute()
        {
            BuildInputs();
            bool enabledLock = config == null || config.lockPlacedParts;
            HashSet<string> locked = AssemblyLockRules.ComputeLocked(_inputs, enabledLock);
            foreach (PartSocket s in _sockets)
            {
                s.IsLocked = locked.Contains(s.SocketId);
            }
        }

        private void BuildInputs()
        {
            _inputs.Clear();
            foreach (PartSocket s in _sockets)
            {
                _inputs.Add(new SocketLockInput(s.SocketId, s.IsFilledCorrectly, s.PrerequisiteSocketIds));
            }
        }
    }
}
