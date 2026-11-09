using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using VRClass.Experiment;

namespace VRClass.Interaction
{
    /// <summary>
    /// デバッグ用：マウスで部品を掴む XRI の Interactor。
    /// コントローラーと同じ XRI の仕組み（選択・フィルター・ソケット）を通るので、ログや吸着の判定も同じになる。
    ///
    /// 操作：
    /// - 左ボタンを押している間、マウスの下の部品を掴む（離すと放す）
    /// - ホイール：手前・奥へ動かす
    /// - Q / E：鉛直軸まわりに回す、R / F：左右の軸まわりに傾ける、Z / X：前後の軸まわりに回す
    /// 入力モードがキーマウを許すときだけ動く（PartInteraction のフィルターでも確認する）。
    /// </summary>
    public sealed class DebugMouseInteractor : XRBaseInteractor
    {
        private const float MinDistance = 0.15f;
        private const float MaxDistance = 3f;
        private const float WheelStep = 0.0005f; // ホイール 1 目盛り（120）で約 6 cm

        [SerializeField] private InputModeController inputMode;
        [SerializeField] private ExperimentConfig config;

        [Tooltip("マウスの位置から光線を出すカメラ。空なら Camera.main")]
        [SerializeField] private Camera rayCamera;

        private IXRSelectInteractable _target;
        private bool _wantSelect;
        private float _distance;

        /// <summary>左ボタンを押している間だけ選択を有効にする。</summary>
        public override bool isSelectActive => base.isSelectActive && _wantSelect;

        public override void GetValidTargets(List<IXRInteractable> targets)
        {
            targets.Clear();
            if (_wantSelect && _target != null)
            {
                targets.Add(_target);
            }
        }

        private void Update()
        {
            Mouse mouse = Mouse.current;
            Camera cam = rayCamera != null ? rayCamera : Camera.main;
            bool allowed = inputMode == null || inputMode.AllowsKeyboardMouse;
            if (mouse == null || cam == null || !allowed)
            {
                Drop();
                return;
            }

            Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());

            if (mouse.leftButton.wasPressedThisFrame)
            {
                TryPick(ray);
            }
            else if (!mouse.leftButton.isPressed)
            {
                Drop();
            }

            // 掴めなくなった（フィルターで外された）ら追わない
            if (_wantSelect && !hasSelection && _target != null && Time.frameCount > _pickFrame + 1)
            {
                Drop();
            }

            if (!_wantSelect)
            {
                return;
            }

            float wheel = mouse.scroll.ReadValue().y;
            _distance = Mathf.Clamp(_distance + wheel * WheelStep, MinDistance, MaxDistance);
            transform.position = ray.GetPoint(_distance);
            RotateByKeys(cam.transform);
        }

        private int _pickFrame;

        private void TryPick(Ray ray)
        {
            if (!Physics.Raycast(ray, out RaycastHit hit, MaxDistance, ~0, QueryTriggerInteraction.Ignore))
            {
                return;
            }

            var part = hit.collider.GetComponentInParent<PartInteraction>();
            if (part == null)
            {
                return;
            }

            _target = part.Grab;
            _distance = hit.distance;
            transform.SetPositionAndRotation(hit.point, part.transform.rotation);
            _wantSelect = true;
            _pickFrame = Time.frameCount;
        }

        private void Drop()
        {
            _wantSelect = false;
            _target = null;
        }

        private void RotateByKeys(Transform cam)
        {
            Keyboard kb = Keyboard.current;
            if (kb == null)
            {
                return;
            }

            float speed = (config != null ? config.debugRotateSpeedDegPerSec : 90f) * Time.deltaTime;
            float yaw = (kb.eKey.isPressed ? 1f : 0f) - (kb.qKey.isPressed ? 1f : 0f);
            float pitch = (kb.rKey.isPressed ? 1f : 0f) - (kb.fKey.isPressed ? 1f : 0f);
            float roll = (kb.xKey.isPressed ? 1f : 0f) - (kb.zKey.isPressed ? 1f : 0f);
            if (yaw != 0f) transform.Rotate(Vector3.up, yaw * speed, Space.World);
            if (pitch != 0f) transform.Rotate(cam.right, pitch * speed, Space.World);
            if (roll != 0f) transform.Rotate(cam.forward, roll * speed, Space.World);
        }
    }
}
