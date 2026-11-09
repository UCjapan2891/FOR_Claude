using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace VRClass.Interaction
{
    /// <summary>
    /// 学習者パネル（SPEC 5.2）：VR 内のボタン「リセット」「ヒント」。
    /// コントローラーの光線・マウスのクリックで押せる。キーボードでは 1 = リセット、2 = ヒント（キーマウを許す入力モードのとき）。
    /// ボタンが押されたことを知らせるだけで、何をするかは LessonController が決める。
    /// </summary>
    public sealed class LearnerPanel : MonoBehaviour
    {
        [SerializeField] private Button resetButton;
        [SerializeField] private Button hintButton;
        [SerializeField] private InputModeController inputMode;

        /// <summary>リセットが押された（引数は "button" か "key"）。</summary>
        public event Action<string> ResetRequested;

        /// <summary>ヒントが押された（引数は "button" か "key"）。</summary>
        public event Action<string> HintRequested;

        /// <summary>ボタンを押せるか（一時停止中・授業の外では押せない）。</summary>
        public void SetInteractable(bool interactable)
        {
            if (resetButton != null) resetButton.interactable = interactable;
            if (hintButton != null) hintButton.interactable = interactable;
        }

        private void OnEnable()
        {
            if (resetButton != null) resetButton.onClick.AddListener(OnResetClicked);
            if (hintButton != null) hintButton.onClick.AddListener(OnHintClicked);
        }

        private void OnDisable()
        {
            if (resetButton != null) resetButton.onClick.RemoveListener(OnResetClicked);
            if (hintButton != null) hintButton.onClick.RemoveListener(OnHintClicked);
        }

        private void OnResetClicked() => ResetRequested?.Invoke("button");

        private void OnHintClicked() => HintRequested?.Invoke("button");

        private void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null || (inputMode != null && !inputMode.AllowsKeyboardMouse))
            {
                return;
            }

            if (kb.digit1Key.wasPressedThisFrame && (resetButton == null || resetButton.interactable))
            {
                ResetRequested?.Invoke("key");
            }

            if (kb.digit2Key.wasPressedThisFrame && (hintButton == null || hintButton.interactable))
            {
                HintRequested?.Invoke("key");
            }
        }
    }
}
