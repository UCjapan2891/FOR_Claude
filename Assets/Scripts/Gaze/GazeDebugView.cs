using UnityEngine;
using VRClass.Core;
using VRClass.Experiment;

namespace VRClass.Gaze
{
    /// <summary>
    /// デバッグ用：注視判定の θ と状態を PC の画面（Game ビュー）に出し、Scene ビューに頭から部品への線を引く。
    /// エディタと Development Build でだけ動き、本番のビルドでは何もしない。HMD の中には出ない。
    /// </summary>
    public sealed class GazeDebugView : MonoBehaviour
    {
        [SerializeField] private GazeJudge gazeJudge;
        [SerializeField] private ExperimentConfig config;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private GUIStyle _style;

        private void Update()
        {
            if (gazeJudge == null || !gazeJudge.IsJudging || gazeJudge.Target == null || gazeJudge.Head == null)
            {
                return;
            }

            Transform h = gazeJudge.Head;
            bool looking = gazeJudge.CurrentTheta.HasValue && gazeJudge.CurrentTheta.Value <= config.gazeAngleThresholdDeg;
            Debug.DrawLine(h.position, gazeJudge.Target.WorldCenter, looking ? Color.green : Color.red);
            Debug.DrawRay(h.position, h.forward * 1.0f, Color.cyan);
        }

        private void OnGUI()
        {
            if (gazeJudge == null || gazeJudge.Logic == null || config == null)
            {
                return;
            }

            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 14 };
            }

            GazeJudgeLogic g = gazeJudge.Logic;
            string theta = gazeJudge.CurrentTheta.HasValue ? $"{gazeJudge.CurrentTheta.Value:0.0}°" : "-";
            string state = g.IsFinished ? (g.Result == GazeEvent.Ok ? "見た（gaze_ok）" : "失敗（gaze_fail）") : "判定中";
            string text = $"注視判定：{state}　対象：{(gazeJudge.Target != null ? gazeJudge.Target.PartId : "-")}\n" +
                          $"θ = {theta}（θth {config.gazeAngleThresholdDeg:0}°）\n" +
                          $"保持 {g.HoldElapsed:0.00} / {config.gazeHoldSec:0.00} 秒　経過 {g.WindowElapsed:0.0} / {config.gazeTimeoutSec:0.0} 秒\n" +
                          $"再配置 {g.Relocations} / {config.maxRelocations} 回";
            GUI.Box(new Rect(10, 210, 520, 80), text, _style);
        }
#endif
    }
}
