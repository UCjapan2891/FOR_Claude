using UnityEngine;
using VRClass.Core;
using VRClass.Emphasis;
using VRClass.Interaction;

namespace VRClass.Lesson
{
    /// <summary>
    /// デバッグ用：PC の画面（Game ビュー）の左上に、入力モードと授業の状態を表示する。
    /// エディタと Development Build でだけ表示し、本番のビルドでは何もしない。HMD の中には出ない。
    /// </summary>
    public sealed class LessonDebugOverlay : MonoBehaviour
    {
        [SerializeField] private LessonController controller;
        [SerializeField] private InputModeController inputMode;
        [SerializeField] private EmphasisController emphasis;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private GUIStyle _style;

        private void OnGUI()
        {
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 14, wordWrap = true };
            }

            string mode = inputMode != null ? InputModeRules.Label(inputMode.Current) : "-";
            string text = $"入力：{mode}（F1 で切り替え）"
                          + (emphasis != null ? $"　条件{(int)emphasis.Condition}" : string.Empty) + "\n";

            if (controller != null && controller.Flow != null)
            {
                LessonFlow flow = controller.Flow;
                StageData stage = controller.CurrentStage;
                text += $"状態：{StateLabel(flow.State)}"
                        + (controller.IsPaused ? "【一時停止中】" : string.Empty)
                        + (controller.IsAborted ? "【中断】" : string.Empty) + "\n";
                if (stage != null && flow.State != LessonState.AssemblyCheck && flow.State != LessonState.Finished)
                {
                    text += $"段階 {flow.StageNumber}/{flow.StageCount}：{stage.stageName}\n";
                    text += $"対象：{stage.targetPartId}　完了条件：{CompletionLabel(stage.completion)}"
                            + (stage.isKeyPoint ? "　【要点】" : string.Empty) + "\n";
                    if (emphasis != null && emphasis.IsRunning)
                    {
                        SpatialEmphasis sp = emphasis.CurrentSpatial;
                        string phase = sp != null ? EmphasisPlan.PhaseName(sp.Phase) : "telop";
                        text += $"強調：{EmphasisPlan.ModeName(emphasis.CurrentMode)}（{phase}）{emphasis.Elapsed:0.0} 秒\n";
                    }

                    if (flow.State == LessonState.Accepting)
                    {
                        text += $"受付 {controller.AcceptingSeconds:0} 秒／ヒント {controller.HintCount} 回\n";
                    }
                }
            }
            else
            {
                text += "状態：開始前\n";
            }

            if (inputMode == null || inputMode.AllowsKeyboardMouse)
            {
                text += "左ドラッグ：掴む／ホイール：前後／Q E・R F・Z X：回す\n右ドラッグ：見回す／矢印：移動（HMD なしのとき）\n1：リセット／2：ヒント\n";
            }

            text += "【実験者】S：スキップ／P：一時停止・再開／A×2：中断／F2：HMD を外したまね";
            GUI.Box(new Rect(10, 10, 520, 210), text, _style);
        }

        private static string StateLabel(LessonState s)
        {
            switch (s)
            {
                case LessonState.Narration: return "説明音声（掴めない）";
                case LessonState.Accepting: return "操作受付（掴める）";
                case LessonState.KeyPoint: return "要点";
                case LessonState.AssemblyCheck: return "組み立て確認（全部のソケットが埋まるまで待つ）";
                case LessonState.Finished: return "終了";
                default: return s.ToString();
            }
        }

        private static string CompletionLabel(CompletionType c)
        {
            switch (c)
            {
                case CompletionType.Detach: return "外す";
                case CompletionType.Attach: return "付ける";
                case CompletionType.Touch: return "掴む";
                default: return "なし";
            }
        }
#endif
    }
}
