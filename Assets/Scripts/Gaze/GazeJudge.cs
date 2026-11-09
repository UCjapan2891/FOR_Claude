using System;
using System.Collections;
using System.Globalization;
using UnityEngine;
using VRClass.Core;
using VRClass.Experiment;
using VRClass.Interaction;
using VRClass.Logging;

namespace VRClass.Gaze
{
    /// <summary>
    /// 注視判定（SPEC 4.1）。頭（HMD のカメラ。HMD がなければマウスで動かすカメラ）の向きで推定する。視線追跡は使わない。
    /// 判定の状態の移り変わりは GazeJudgeLogic（Core）。ここでは θ の計算・再配置・ログを行う。
    /// 記録：gaze_ok（theta, hold, total, relocations）、gaze_timeout（n）、relocate（Relocator）、gaze_fail。
    /// 判定中は θ を head.csv に出す（それ以外は空欄）。条件による分岐はない。
    /// </summary>
    public sealed class GazeJudge : MonoBehaviour
    {
        [Tooltip("頭の Transform（XR Origin の Main Camera）。空なら Camera.main")]
        [SerializeField] private Transform head;

        [SerializeField] private ExperimentConfig config;
        [SerializeField] private SessionLog sessionLog;
        [SerializeField] private Relocator relocator;
        [SerializeField] private HeadLogger headLogger;

        private GazeJudgeLogic _logic;
        private PartInteraction _target;

        /// <summary>判定中か。</summary>
        public bool IsJudging => _logic != null && !_logic.IsFinished;

        /// <summary>判定中の θ（度）。判定していなければ null。</summary>
        public float? CurrentTheta { get; private set; }

        /// <summary>今の判定（デバッグ表示用）。判定していなければ null。</summary>
        public GazeJudgeLogic Logic => _logic;

        public PartInteraction Target => _target;

        public Transform Head => head != null ? head : (Camera.main != null ? Camera.main.transform : null);

        private void Awake()
        {
            if (headLogger != null)
            {
                headLogger.ThetaProvider = () => CurrentTheta;
            }
        }

        /// <summary>
        /// 対象部品の注視判定を行う。再配置の前には onBeforeRelocate を呼ぶ（部品を空中に止める）。
        /// 終わったら onDone(true = 見た / false = 失敗)。
        /// </summary>
        public IEnumerator Judge(PartInteraction target, Action onBeforeRelocate, Action<bool> onDone)
        {
            _target = target;
            _logic = new GazeJudgeLogic(config.gazeAngleThresholdDeg, config.gazeHoldSec, config.gazeTimeoutSec, config.maxRelocations);

            while (!_logic.IsFinished)
            {
                Transform h = Head;
                float theta = h != null ? GazeMath.AngleToTargetDeg(h.position, h.forward, target.WorldCenter) : float.NaN;
                CurrentTheta = float.IsNaN(theta) ? (float?)null : theta;

                switch (_logic.Tick(Time.deltaTime, theta))
                {
                    case GazeEvent.Ok:
                        Log(LogEvent.GazeOk, string.Format(CultureInfo.InvariantCulture,
                            "theta={0:F2};hold={1:F3};total={2:F3};relocations={3}",
                            theta, _logic.HoldElapsed, _logic.TotalElapsed, _logic.Relocations));
                        break;

                    case GazeEvent.Timeout:
                        Log(LogEvent.GazeTimeout, "n=" + _logic.Relocations.ToString(CultureInfo.InvariantCulture));
                        onBeforeRelocate?.Invoke();
                        if (relocator != null)
                        {
                            relocator.Relocate(target, h, _logic.Relocations);
                        }

                        break;

                    case GazeEvent.Fail:
                        Log(LogEvent.GazeTimeout, "n=" + (_logic.Relocations + 1).ToString(CultureInfo.InvariantCulture) + ";limit");
                        Log(LogEvent.GazeFail, string.Format(CultureInfo.InvariantCulture,
                            "total={0:F3};relocations={1}", _logic.TotalElapsed, _logic.Relocations));
                        break;
                }

                if (_logic.IsFinished)
                {
                    break;
                }

                yield return null;
            }

            bool ok = _logic.Result == GazeEvent.Ok;
            CurrentTheta = null;
            onDone?.Invoke(ok);
        }

        /// <summary>判定を打ち切る（一時停止・中断）。コルーチンは呼び出し側が止める。</summary>
        public void Cancel()
        {
            CurrentTheta = null;
            _logic = null;
            _target = null;
        }

        private void Log(string eventName, string detail)
        {
            if (sessionLog != null)
            {
                sessionLog.Log(eventName, _target != null ? _target.PartId : null, detail);
            }
        }
    }
}
