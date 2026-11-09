using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using XRCommonUsages = UnityEngine.XR.CommonUsages;
using XRInputDevice = UnityEngine.XR.InputDevice;
using XRInputDevices = UnityEngine.XR.InputDevices;
using XRNode = UnityEngine.XR.XRNode;
using XRSettings = UnityEngine.XR.XRSettings;
using VRClass.Core;
using VRClass.Experiment;

namespace VRClass.Lesson
{
    /// <summary>
    /// HMD を外した・トラッキングを失ったときに、授業を自動で一時停止する（SPEC 5.3）。判定は PresenceDetector（Core）。
    /// 使う情報（OpenXR 1.17.1）：
    /// - 頭のデバイスの XRCommonUsages.userPresence（装着センサー。XR_EXT_user_presence に対応したランタイムで取れる）
    /// - 頭のデバイスの XRCommonUsages.isTracked
    /// - アプリが前面か（OnApplicationFocus）。HMD が動いているときだけ使う（エディタでほかの窓を触って止まらないように）
    /// 再開は自動にせず、実験者が P を押す。
    /// デバッグ用：F2 で「HMD を外した」状態をまねる（エディタと Development Build のみ）。
    /// </summary>
    public sealed class PresenceMonitor : MonoBehaviour
    {
        [SerializeField] private LessonController controller;
        [SerializeField] private ExperimentConfig config;

        private readonly List<XRInputDevice> _devices = new List<XRInputDevice>();
        private PresenceDetector _detector;
        private bool _focused = true;
        private bool _simulateRemoved;

        public bool SimulatingRemoved => _simulateRemoved;

        private void Awake()
        {
            _detector = new PresenceDetector(config != null ? config.presenceGraceSec : 0.5f);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            _focused = hasFocus;
        }

        private void OnApplicationPause(bool paused)
        {
            _focused = !paused;
        }

        private void Update()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.f2Key.wasPressedThisFrame)
            {
                _simulateRemoved = !_simulateRemoved;
                Debug.Log(_simulateRemoved ? "[デバッグ] HMD を外した状態をまねています（F2 で戻す）" : "[デバッグ] HMD を付けた状態に戻しました");
            }
#endif
            bool? present = null;
            bool? tracked = null;
            XRInputDevice head = GetHead();
            if (head.isValid)
            {
                if (head.TryGetFeatureValue(XRCommonUsages.userPresence, out bool p)) present = p;
                if (head.TryGetFeatureValue(XRCommonUsages.isTracked, out bool t)) tracked = t;
            }

            if (_simulateRemoved)
            {
                present = false;
            }

            bool? focused = XRSettings.isDeviceActive ? _focused : (bool?)null;
            PresenceIssue issue = _detector.Update(Time.unscaledDeltaTime, present, tracked, focused);
            if (issue != PresenceIssue.None && controller != null && !controller.IsPaused)
            {
                controller.Pause(PresenceDetector.LogReason(issue));
            }
        }

        private XRInputDevice GetHead()
        {
            _devices.Clear();
            XRInputDevices.GetDevicesAtXRNode(XRNode.Head, _devices);
            return _devices.Count > 0 ? _devices[0] : default;
        }
    }
}
