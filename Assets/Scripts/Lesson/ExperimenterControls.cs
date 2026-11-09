using UnityEngine;
using UnityEngine.InputSystem;
using VRClass.Experiment;

namespace VRClass.Lesson
{
    /// <summary>
    /// 実験者の操作（SPEC 5.3、PC のキーボード）。入力モードに関係なく常に受け付ける。
    /// - S：スキップ（そのパートを自動で完了）
    /// - P：一時停止／再開（再開はパートの最初から）
    /// - A：中断（abortDoublePressSec 秒以内に2回押したときだけ）
    /// 本実験は Quest Link で PC 上のアプリを動かす前提（PC のキーボードが使える）。
    /// </summary>
    public sealed class ExperimenterControls : MonoBehaviour
    {
        [SerializeField] private LessonController controller;
        [SerializeField] private ExperimentConfig config;

        private float _lastAbortPress = float.NegativeInfinity;

        private void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null || controller == null)
            {
                return;
            }

            if (kb.sKey.wasPressedThisFrame)
            {
                controller.Skip("experimenter");
            }

            if (kb.pKey.wasPressedThisFrame)
            {
                if (controller.IsPaused)
                {
                    controller.Resume();
                }
                else
                {
                    controller.Pause("experimenter");
                }
            }

            if (kb.aKey.wasPressedThisFrame)
            {
                float window = config != null ? config.abortDoublePressSec : 2f;
                float now = Time.unscaledTime;
                if (now - _lastAbortPress <= window)
                {
                    _lastAbortPress = float.NegativeInfinity;
                    controller.Abort("experimenter");
                }
                else
                {
                    _lastAbortPress = now;
                    Debug.Log($"中断するには、{window:0.#} 秒以内にもう一度 A を押してください。");
                }
            }
        }
    }
}
