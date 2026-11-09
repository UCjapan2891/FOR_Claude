using UnityEngine;
using VRClass.Core;
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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private GUIStyle _style;

        private void OnGUI()
        {
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 14, wordWrap = true };
            }

            string mode = inputMode != null ? InputModeRules.Label(inputMode.Current) : "-";
            string text = $"入力：{mode}（F1 で切り替え）\n";

            if (controller != null && controller.Flow != null)
            {
                LessonFlow flow = controller.Flow;
                StageData stage = controller.CurrentStage;
                text += $"状態：{StateLabel(flow.State)}\n";
                if (stage != null && flow.State != LessonState.AssemblyCheck && flow.State != LessonState.Finished)
                {
                    text += $"段階 {flow.StageNumber}/{flow.StageCount}：{stage.stageName}\n";
                    text += $"対象：{stage.targetPartId}　完了条件：{CompletionLabel(stage.completion)}"
                            + (stage.isKeyPoint ? "　【要点】" : string.Empty) + "\n";
                }
            }
            else
            {
                text += "状態：開始前\n";
            }

            if (inputMode == null || inputMode.AllowsKeyboardMouse)
            {
                text += "左ドラッグ：掴む／ホイール：前後／Q E・R F・Z X：回す\n右ドラッグ：見回す／矢印：移動（HMD なしのとき）";
            }

            GUI.Box(new Rect(10, 10, 460, 130), text, _style);
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
