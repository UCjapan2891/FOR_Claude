using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using VRClass.Experiment;
using VRClass.Interaction;

namespace VRClass.Emphasis
{
    /// <summary>
    /// 発表用のデモ：机の上に分解して並べた部品をマウスで掴んで動かし、P を押すと対象の部品にすぐ空間的強調
    /// （① 引き寄せ → ② 拡大 → ③ 回転 →（観察）→ ④ 復帰）をかける。復帰では、強調を始めた場所へ戻る。
    /// 部品は物理で動くので、机やほかの部品をすり抜けない。触れた部品の名前と役割を黒板に出す。
    /// 実験の授業の流れ・ログとは関係しない（Assets/Scenes/EmphasisDemo.unity でだけ使う）。
    ///
    /// 操作：
    /// - 左ドラッグ：部品を掴んで動かす（掴んだ部品が強調の対象）。ホイールで手前・奥、Q E・R F・Z X で回す
    /// - P：強調　R：強調を中止して元に戻す　Tab：対象を替える　K：操作の説明を出す・消す
    /// - 右ドラッグ：見回す　矢印キー：前後左右に動く（DebugCameraControl）
    /// </summary>
    public sealed class EmphasisDemo : MonoBehaviour
    {
        private const float MinDistance = 0.15f;
        private const float MaxDistance = 3f;
        private const float WheelStep = 0.0005f;
        private const float MaxDragSpeed = 2f;     // m/秒。速すぎると机をすり抜けるので抑える
        private const float FallResetY = 0.3f;     // これより下に落ちたら初期位置へ戻す（床に落ちた）

        [SerializeField] private Transform[] targets;
        [SerializeField] private Transform head;
        [SerializeField] private ExperimentConfig config;
        [SerializeField] private PartInfoBoard board;
        [SerializeField] private bool showOverlay = true;

        private int _index;
        private SpatialEmphasis _emphasis;
        private Coroutine _running;
        private GUIStyle _style;
        private Vector3[] _homePositions;
        private Quaternion[] _homeRotations;

        // マウスで掴んでいる部品
        private Rigidbody _held;
        private float _holdDistance;
        private Vector3 _holdOffset;   // 部品の位置 − 光線が当たった点
        private Vector3 _dragTarget;
        private Vector3 _spin;         // 回すキーの入力（度/秒、ワールド軸）

        private Transform Target => targets != null && targets.Length > 0 ? targets[_index % targets.Length] : null;

        private Transform Head => head != null ? head : (Camera.main != null ? Camera.main.transform : null);

        private Camera Cam
        {
            get
            {
                Transform h = Head;
                Camera c = h != null ? h.GetComponent<Camera>() : null;
                return c != null ? c : Camera.main;
            }
        }

        private void Start()
        {
            _homePositions = new Vector3[targets.Length];
            _homeRotations = new Quaternion[targets.Length];
            for (int i = 0; i < targets.Length; i++)
            {
                _homePositions[i] = targets[i].position;
                _homeRotations[i] = targets[i].rotation;
            }

            if (board != null)
            {
                board.ShowDefault();
            }
        }

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
                Drop();
                SetKinematic(Target, true);
                _emphasis = new SpatialEmphasis(Target);
                ShowInfo(Target);
                _running = StartCoroutine(Play());
            }

            if (kb.rKey.wasPressedThisFrame && _running != null)
            {
                StopEmphasis();
            }

            if (kb.kKey.wasPressedThisFrame)
            {
                showOverlay = !showOverlay;
            }

            if (kb.tabKey.wasPressedThisFrame && _running == null && targets.Length > 0)
            {
                _index = (_index + 1) % targets.Length;
                ShowInfo(Target);
            }
        }

        private void FixedUpdate()
        {
            if (_held != null)
            {
                // 目標の位置へ速度で引っぱる（位置を直接書き換えないので、机や部品とぶつかって止まる）
                Vector3 v = (_dragTarget - _held.position) / Time.fixedDeltaTime;
                _held.linearVelocity = Vector3.ClampMagnitude(v, MaxDragSpeed);
                _held.angularVelocity = _spin * Mathf.Deg2Rad;
            }

            // 床に落ちたら机の上の初期位置へ戻す
            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i].position.y < FallResetY && (_running == null || targets[i] != Target))
                {
                    ReturnHome(i);
                }
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
                Drop();
            }

            if (_held == null)
            {
                return;
            }

            float wheel = mouse.scroll.ReadValue().y;
            _holdDistance = Mathf.Clamp(_holdDistance + wheel * WheelStep, MinDistance, MaxDistance);
            _dragTarget = ray.GetPoint(_holdDistance) + _holdOffset;

            _spin = Vector3.zero;
            if (kb == null)
            {
                return;
            }

            float speed = config != null ? config.debugRotateSpeedDegPerSec : 90f;
            float yaw = (kb.eKey.isPressed ? 1f : 0f) - (kb.qKey.isPressed ? 1f : 0f);
            float pitch = (kb.rKey.isPressed ? 1f : 0f) - (kb.fKey.isPressed ? 1f : 0f);
            float roll = (kb.xKey.isPressed ? 1f : 0f) - (kb.zKey.isPressed ? 1f : 0f);
            _spin = (Vector3.up * yaw + cam.transform.right * pitch + cam.transform.forward * roll) * speed;
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
            _held = targets[found].GetComponent<Rigidbody>();
            ShowInfo(targets[found]);
            if (_held == null)
            {
                return;
            }

            _held.isKinematic = false;
            _held.useGravity = false;
            _holdDistance = hit.distance;
            _holdOffset = _held.position - hit.point;
            _dragTarget = _held.position;
        }

        private void Drop()
        {
            if (_held == null)
            {
                return;
            }

            _held.useGravity = true;
            _held.linearVelocity = Vector3.zero; // 投げない
            _held.angularVelocity = Vector3.zero;
            _held = null;
            _spin = Vector3.zero;
        }

        private void ReturnHome(int i)
        {
            if (_held != null && _held.transform == targets[i])
            {
                Drop();
            }

            var rb = targets[i].GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.position = _homePositions[i];
                rb.rotation = _homeRotations[i];
            }

            targets[i].SetPositionAndRotation(_homePositions[i], _homeRotations[i]);
        }

        private void ShowInfo(Transform t)
        {
            if (board != null && t != null)
            {
                board.Show(t.GetComponent<PartDescription>());
            }
        }

        // ---- 強調 ----
        private IEnumerator Play()
        {
            Transform target = Target;
            yield return _emphasis.Play(Head, config, includePull: true);
            SetKinematic(target, false);
            _running = null;
        }

        private void StopEmphasis()
        {
            if (_running != null)
            {
                StopCoroutine(_running);
                _running = null;
            }

            _emphasis?.Restore();
            SetKinematic(Target, false);
        }

        /// <summary>強調の間は物理を止める（重力で落ちたり、ほかの部品に押されたりしないように）。</summary>
        private static void SetKinematic(Transform t, bool kinematic)
        {
            var rb = t != null ? t.GetComponent<Rigidbody>() : null;
            if (rb == null)
            {
                return;
            }

            if (!kinematic)
            {
                rb.isKinematic = false;
                rb.useGravity = true;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            else
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
            }
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
                          "P：強調　R：中止　Tab：対象を替える　K：この表示を消す";
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
