using System;
using UnityEngine;
using UnityEngine.InputSystem;
using VRClass.Core;
using VRClass.Experiment;

namespace VRClass.Interaction
{
    /// <summary>
    /// 部品の操作を受け付ける入力（コントローラーのみ／両方／キーマウのみ）。
    /// 初期値は ExperimentConfig.inputMode。実行中は F1 キーで順に切り替える（設定の asset は書き換えない）。
    /// </summary>
    public sealed class InputModeController : MonoBehaviour
    {
        [SerializeField] private ExperimentConfig config;

        public InputMode Current { get; private set; } = InputMode.ControllerOnly;

        public event Action<InputMode> Changed;

        public bool AllowsController => InputModeRules.AllowsController(Current);

        public bool AllowsKeyboardMouse => InputModeRules.AllowsKeyboardMouse(Current);

        private void Awake()
        {
            if (config != null)
            {
                Current = config.inputMode;
            }

#if !UNITY_EDITOR
            if (Current != InputMode.ControllerOnly)
            {
                Debug.LogWarning($"入力が「{InputModeRules.Label(Current)}」です。本実験では ControllerOnly にしてください。");
            }
#endif
            Debug.Log($"入力：{InputModeRules.Label(Current)}（F1 で切り替え）");
        }

        public void Set(InputMode mode)
        {
            if (mode == Current)
            {
                return;
            }

            Current = mode;
            Debug.Log($"入力を切り替えました：{InputModeRules.Label(Current)}");
            Changed?.Invoke(Current);
        }

        private void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.f1Key.wasPressedThisFrame)
            {
                Set(InputModeRules.Next(Current));
            }
        }
    }
}
