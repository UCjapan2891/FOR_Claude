using System.Collections;
using UnityEngine;
using VRClass.Interaction;
using VRClass.Lesson;

namespace VRClass.Gaze
{
    /// <summary>
    /// 要点の処理（SPEC 3章 6〜7）：注視判定 → （見ていなければ）再配置 → 強調。
    /// 段階5の時点では強調がないので、判定が終わったら部品を元の状態へ戻して次へ進む。
    /// 段階6で、ここから EmphasisController（条件による違いはそちらだけ）を呼ぶ。
    /// </summary>
    public sealed class GazeKeyPointHandler : MonoBehaviour, IKeyPointHandler
    {
        [SerializeField] private GazeJudge gazeJudge;

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

            // 段階6：ここで強調（条件1はテロップ＋停止、条件2はテロップ＋空間的強調。gaze_fail ならテロップのみ）

            RestorePose();
        }

        public void Cancel()
        {
            if (gazeJudge != null)
            {
                gazeJudge.Cancel();
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
