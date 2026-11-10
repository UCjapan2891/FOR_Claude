using System.Collections;
using UnityEngine;
using VRClass.Emphasis;
using VRClass.Interaction;
using VRClass.Lesson;

namespace VRClass.Gaze
{
    /// <summary>
    /// 要点の処理（SPEC 3章 6〜7）：注視判定 → （見ていなければ）再配置 → 強調 → 元の状態へ戻す。
    /// 強調は EmphasisController が行う（条件による違いはそちらだけ）。ここには条件の分岐を書かない。
    /// </summary>
    public sealed class GazeKeyPointHandler : MonoBehaviour, IKeyPointHandler
    {
        [SerializeField] private GazeJudge gazeJudge;

        [Tooltip("強調（テロップ／空間的強調）。空なら注視判定だけ行って次へ進む")]
        [SerializeField] private EmphasisController emphasis;

        private PartPose _pose;

        /// <summary>直前の要点で、注視できたか（false なら gaze_fail）。</summary>
        public bool LastGazeOk { get; private set; }

        public IEnumerator Run(KeyPointContext context)
        {
            PartInteraction target = context.Target;
            if (target == null || gazeJudge == null)
            {
                yield break;
            }

            _pose = PartPose.Capture(target); // 再配置の前の元の状態（④復帰で戻す先）
            bool ok = false;
            yield return gazeJudge.Judge(target, () => _pose.DetachForMove(), result => ok = result);
            LastGazeOk = ok;

            // 強調（条件1はテロップ＋停止、条件2はテロップ＋空間的強調。gaze_fail ならテロップのみ）
            if (emphasis != null)
            {
                yield return emphasis.Run(context, _pose, ok);
            }

            RestorePose();
        }

        public void Cancel()
        {
            if (gazeJudge != null)
            {
                gazeJudge.Cancel();
            }

            if (emphasis != null)
            {
                emphasis.Cancel();
            }

            RestorePose();
        }

        private void RestorePose()
        {
            if (_pose != null && _pose.IsDetached)
            {
                _pose.Restore();
            }

            _pose = null;
        }
    }
}
