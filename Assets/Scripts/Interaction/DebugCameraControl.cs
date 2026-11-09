using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using VRClass.Experiment;

namespace VRClass.Interaction
{
    /// <summary>
    /// デバッグ用：HMD がないとき、キーボード・マウスで視点を動かす。
    /// - 右ボタンを押しながらマウスを動かす：見回す
    /// - 矢印キー：前後左右に動く（実験者の操作キー S / P / A と重ならないよう WASD は使わない）
    /// HMD が動いているとき（XRSettings.isDeviceActive）や、入力モードがキーマウを許さないときは何もしない。
    /// </summary>
    public sealed class DebugCameraControl : MonoBehaviour
    {
        [Tooltip("動かすカメラ（XR Origin の Main Camera）")]
        [SerializeField] private Transform cameraTransform;

        [Tooltip("矢印キーで動かす対象（XR Origin）")]
        [SerializeField] private Transform rigRoot;

        [SerializeField] private InputModeController inputMode;
        [SerializeField] private ExperimentConfig config;

        private float _yaw;
        private float _pitch;
        private bool _initialized;

        private void LateUpdate()
        {
            bool allowed = inputMode == null || inputMode.AllowsKeyboardMouse;
            if (!allowed || XRSettings.isDeviceActive || cameraTransform == null)
            {
                return;
            }

            if (!_initialized)
            {
                Vector3 e = cameraTransform.localEulerAngles;
                _yaw = e.y;
                _pitch = e.x > 180f ? e.x - 360f : e.x;
                _initialized = true;
            }

            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.rightButton.isPressed)
            {
                float sens = config != null ? config.debugLookSensitivity : 0.15f;
                Vector2 delta = mouse.delta.ReadValue();
                _yaw += delta.x * sens;
                _pitch = Mathf.Clamp(_pitch - delta.y * sens, -85f, 85f);
            }

            float eye = config != null ? config.debugEyeHeight : 1.6f;
            cameraTransform.localRotation = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 p = cameraTransform.localPosition;
            cameraTransform.localPosition = new Vector3(p.x, eye, p.z);

            Keyboard kb = Keyboard.current;
            if (kb == null || rigRoot == null)
            {
                return;
            }

            float forward = (kb.upArrowKey.isPressed ? 1f : 0f) - (kb.downArrowKey.isPressed ? 1f : 0f);
            float right = (kb.rightArrowKey.isPressed ? 1f : 0f) - (kb.leftArrowKey.isPressed ? 1f : 0f);
            if (forward == 0f && right == 0f)
            {
                return;
            }

            Vector3 f = cameraTransform.forward;
            f.y = 0f;
            f.Normalize();
            Vector3 r = cameraTransform.right;
            r.y = 0f;
            r.Normalize();
            float speed = (config != null ? config.debugMoveSpeed : 1f) * Time.deltaTime;
            rigRoot.position += (f * forward + r * right) * speed;
        }
    }
}
