using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using VRClass.Experiment;

namespace VRClass.Emphasis
{
    /// <summary>
    /// 発表用のデモ：部品をマウスで掴んで動かし、P を押すと対象の部品にすぐ空間的強調
    /// （① 引き寄せ → ② 拡大 → ③ 回転 →（観察）→ ④ 復帰）をかける。復帰では、強調を始めた場所へ戻る。
    /// 実験の授業の流れ・ログとは関係しない（Assets/Scenes/EmphasisDemo.unity でだけ使う）。
    ///
    /// 操作：
    /// - 左ドラッグ：部品を掴んで動かす（掴んだ部品が強調の対象になる）。ホイールで手前・奥、Q E・R F・Z X で回す
    /// - P：強調　R：中止して元に戻す　Tab：対象を替える　H：画面の説明を消す・出す
    /// - 右ドラッグ：見回す　矢印キー：前後左右に動く（DebugCameraControl）
    /// </summary>
    public sealed class EmphasisDemo : MonoBehaviour
    {
        private const float MinDistance = 0.15f;
        private const float MaxDistance = 3f;
        private const float WheelStep = 0.0005f;

        [SerializeField] private Transform[] targets;
        [SerializeField] private Transform head;
        [SerializeField] private ExperimentConfig config;
        [SerializeField] private bool showOverlay = true;

        private int _index;
        private SpatialEmphasis _emphasis;
        private Coroutine _running;
        private GUIStyle _style;

        // マウスで掴んでいる部品
        private Transform _held;
        private float _holdDistance;
        private Vector3 _holdOffset; // 部品の位置 − 光線が当たった点

        private Transform Target => targets != null && targets.Length > 0 ? targets[_index % targets.Length] : null;

        private Camera Cam
        {
            get
            {
                Transform h = Head;
                Camera c = h != null ? h.GetComponent<Camera>() : null;
                return c != null ? c : Camera.main;
            }
        }

        private Transform Head => head != null ? head : (Camera.main != null ? Camera.main.transform : null);

        private void Update()
        {
            Keyboard kb = Keyboard.current;
            Mouse mouse = Mouse.current;

            if (_running == null && mouse != null)
            {
                UpdateDrag(mouse, kb);
            }

            if (kb == null)
            {
                return;
            }

            if (kb.pKey.wasPressedThisFrame && _running == null && Target != null)
            {
                _held = null;
                _emphasis = new SpatialEmphasis(Target);
                _running = StartCoroutine(Play());
            }

            if (kb.rKey.wasPressedThisFrame && _running != null)
            {
                Stop();
            }

            if (kb.hKey.wasPressedThisFrame)
            {
                showOverlay = !showOverlay;
            }

            if (kb.tabKey.wasPressedThisFrame && _running == null && targets != null && targets.Length > 0)
            {
                _index = (_index + 1) % targets.Length;
            }
        }

        // ---- マウスで掴んで動かす ----
        private void UpdateDrag(Mouse mouse, Keyboard kb)
        {
            Camera cam = Cam;
            if (cam == null)
            {
                return;
            }

            Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());

            if (mouse.leftButton.wasPressedThisFrame)
            {
                TryPick(ray);
            }
            else if (!mouse.leftButton.isPressed)
            {
                _held = null;
            }

            if (_held == null)
            {
                return;
            }

            float wheel = mouse.scroll.ReadValue().y;
            _holdDistance = Mathf.Clamp(_holdDistance + wheel * WheelStep, MinDistance, MaxDistance);
            _held.position = ray.GetPoint(_holdDistance) + _holdOffset;

            if (kb == null)
            {
                return;
            }

            float speed = (config != null ? config.debugRotateSpeedDegPerSec : 90f) * Time.deltaTime;
            float yaw = (kb.eKey.isPressed ? 1f : 0f) - (kb.qKey.isPressed ? 1f : 0f);
            float pitch = (kb.rKey.isPressed ? 1f : 0f) - (kb.fKey.isPressed ? 1f : 0f);
            float roll = (kb.xKey.isPressed ? 1f : 0f) - (kb.zKey.isPressed ? 1f : 0f);
            if (yaw != 0f) _held.Rotate(Vector3.up, yaw * speed, Space.World);
            if (pitch != 0f) _held.Rotate(cam.transform.right, pitch * speed, Space.World);
            if (roll != 0f) _held.Rotate(cam.transform.forward, roll * speed, Space.World);
        }

        private void TryPick(Ray ray)
        {
            if (!Physics.Raycast(ray, out RaycastHit hit, MaxDistance, ~0, QueryTriggerInteraction.Ignore))
            {
                return;
            }

            int found = System.Array.IndexOf(targets, hit.collider.transform);
            if (found < 0)
            {
                return; // 机や床は動かさない
            }

            _index = found; // 掴んだ部品を強調の対象にする
            _held = targets[found];
            _holdDistance = hit.distance;
            _holdOffset = _held.position - hit.point;
        }

        // ---- 強調 ----
        private IEnumerator Play()
        {
            yield return _emphasis.Play(Head, config, includePull: true);
            _running = null;
        }

        private void Stop()
        {
            if (_running != null)
            {
                StopCoroutine(_running);
                _running = null;
            }

            _emphasis?.Restore();
        }

        private void OnGUI()
        {
            if (!showOverlay)
            {
                return;
            }

            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 18, wordWrap = true };
            }

            string phase = _emphasis == null || _running == null ? (_held != null ? "掴んでいる" : "待機中") : PhaseLabel(_emphasis.Phase);
            string detail = _emphasis != null && _running != null
                ? $"　{_emphasis.PhaseElapsed:0.0} / {_emphasis.PhaseDuration:0.0} 秒　拡大率 s = {_emphasis.ScaleFactor:0.00}"
                : string.Empty;
            string text = $"空間的強調のデモ　対象：{(Target != null ? Target.name : "-")}\n" +
                          $"状態：{phase}{detail}\n" +
                          "左ドラッグ：部品を動かす（ホイール：前後、Q E・R F・Z X：回す）　右ドラッグ：見回す　矢印：移動\n" +
                          "P：強調　R：中止　Tab：対象を替える　H：この表示を消す";
            GUI.Box(new Rect(10, 10, 860, 110), text, _style);
        }

        private static string PhaseLabel(EmphasisPhase p)
        {
            switch (p)
            {
                case EmphasisPhase.Pull: return "① 引き寄せ";
                case EmphasisPhase.Scale: return "② 拡大";
                case EmphasisPhase.Rotate: return "③ 回転";
                case EmphasisPhase.Observe: return "（観察）";
                case EmphasisPhase.Return: return "④ 復帰";
                case EmphasisPhase.Done: return "完了";
                default: return "待機中";
            }
        }
    }
}
