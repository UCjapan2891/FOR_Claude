using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using VRClass.Core;
using VRClass.Experiment;
using VRClass.Interaction;

namespace VRClass.Emphasis
{
    /// <summary>
    /// 発表用のデモ：机の上に分解して並べた部品をマウスで掴んで動かし、P を押すと対象の部品にすぐ空間的強調
    /// （① 引き寄せ → ② 拡大 → ③ 回転 →（観察）→ ④ 復帰）をかける。復帰では、強調を始めた場所へ戻る。
    /// 部品は物理で動くので、机やほかの部品をすり抜けない。触れた部品の名前と役割を黒板に出す。
    /// 掴んでいる間は、置く正しい場所に部品の形を光らせる（吸着できる範囲に入ると緑）。
    /// 置く場所の近く（デモ用の snapDistance 以内）では吸い寄せ、離すとぴったり収まる。
    /// 正しく置いた部品は吸着して固定する（下の部品が固定済みのときだけ。本番と同じ AssemblyLockRules）。
    /// 全部置けたら黒板に「組み立て完了」と出す。
    /// 実験の授業の流れ・ログとは関係しない（Assets/Scenes/EmphasisDemo.unity でだけ使う）。
    ///
    /// 操作：
    /// - 左ドラッグ：部品を掴んで動かす（掴んだ部品が強調の対象）。ホイールで手前・奥、Q E・R F・Z X で回す
    /// - P：強調　R：強調を中止して元に戻す　Tab：対象を替える　K：操作の説明を出す・消す　Backspace：最初からやり直す
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

        [Tooltip("各部品を置く正しい場所（targets と同じ順）")]
        [SerializeField] private Transform[] slots;

        [Tooltip("各部品の回転対称の軸（targets と同じ順）")]
        [SerializeField] private SymmetryAxis[] symmetry;

        [Tooltip("各部品の「先に置くべき部品」の番号（カンマ区切り、targets と同じ順）。例：カバーは \"0,1\"")]
        [SerializeField] private string[] prerequisites;

        [Tooltip("置く場所のガイド（半透明に光る形）")]
        [SerializeField] private HintPresenter guide;

        [Header("デモ用の吸着（実験の値 socketSnapDistance・socketAngleToleranceDeg とは別）")]
        [Tooltip("この距離（m）以内に入ると吸い寄せられ、離すと正しい位置に収まる")]
        [SerializeField] private float snapDistance = 0.12f;

        [Tooltip("向きのずれがこの角度（度）以内なら吸着する")]
        [SerializeField] private float snapAngleDeg = 45f;

        [Tooltip("吸い寄せの強さ（0〜1。大きいほど強く置く場所へ引っぱる）")]
        [Range(0f, 1f)] [SerializeField] private float magnetStrength = 0.6f;
        [SerializeField] private bool showOverlay = true;

        private int _index;
        private SpatialEmphasis _emphasis;
        private Coroutine _running;
        private GUIStyle _style;
        private Vector3[] _homePositions;
        private Quaternion[] _homeRotations;
        private bool[] _placed;
        private HashSet<string> _locked = new HashSet<string>();
        private bool _completed;
        private int _magnetSlot = -1; // 吸い寄せている部品の番号（なければ -1）

        private static readonly Color NearColor = new Color(0.2f, 0.9f, 0.3f, 0.6f);

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

            _placed = new bool[targets.Length];

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

            if (kb.backspaceKey.wasPressedThisFrame && _running == null)
            {
                ResetAll();
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
                if (_magnetSlot >= 0 && _spin == Vector3.zero)
                {
                    _held.angularVelocity = Vector3.zero;
                    _held.MoveRotation(Quaternion.Slerp(_held.rotation, slots[_magnetSlot].rotation, 0.2f));
                }
                else
                {
                    _held.angularVelocity = _spin * Mathf.Deg2Rad;
                }
            }

            // 床に落ちたら机の上の初期位置へ戻す
            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i].position.y < FallResetY && !_placed[i] && (_running == null || targets[i] != Target))
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

            // 置く場所の近くでは吸い寄せる（位置は置く場所の方へ寄せ、向きは FixedUpdate でそろえる）
            int heldIndex = System.Array.IndexOf(targets, _held.transform);
            _magnetSlot = heldIndex >= 0 && IsNearSlot(heldIndex) ? heldIndex : -1;
            if (_magnetSlot >= 0)
            {
                _dragTarget = Vector3.Lerp(_dragTarget, slots[_magnetSlot].position, magnetStrength);
            }

            UpdateGuide();

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
            ShowInfo(targets[found]);
            if (IsLocked(found))
            {
                return; // 正しい位置に固定済み
            }

            _held = targets[found].GetComponent<Rigidbody>();
            if (_held == null)
            {
                return;
            }

            if (_placed[found])
            {
                _placed[found] = false; // 固定されていない（上の部品を先に置いた）ので外せる
                RecomputeLocks();
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

            int i = System.Array.IndexOf(targets, _held.transform);
            _held.useGravity = true;
            _held.linearVelocity = Vector3.zero; // 投げない
            _held.angularVelocity = Vector3.zero;
            _held = null;
            _spin = Vector3.zero;
            _magnetSlot = -1;
            HideGuide();

            if (i >= 0 && IsNearSlot(i))
            {
                Place(i);
            }
        }

        // ---- 置く場所のガイドと吸着 ----
        private void UpdateGuide()
        {
            int i = System.Array.IndexOf(targets, _held.transform);
            if (guide == null || i < 0 || slots == null || i >= slots.Length || slots[i] == null)
            {
                return;
            }

            guide.Show(targets[i], slots[i]);
            guide.SetColor(IsNearSlot(i) ? NearColor : (Color?)null);
        }

        private void HideGuide()
        {
            if (guide != null)
            {
                guide.SetColor(null);
                guide.Hide();
            }
        }

        /// <summary>正しい場所の近く（デモ用の snapDistance・snapAngleDeg 以内）か。</summary>
        private bool IsNearSlot(int i)
        {
            if (slots == null || i >= slots.Length || slots[i] == null)
            {
                return false;
            }

            float distTol = snapDistance;
            float angleTol = snapAngleDeg;
            SymmetryAxis axis = symmetry != null && i < symmetry.Length ? symmetry[i] : SymmetryAxis.None;
            float dist = Vector3.Distance(targets[i].position, slots[i].position);
            float angle = OrientationMath.AngleDeg(targets[i].rotation, slots[i].rotation, axis);
            return dist <= distTol && OrientationMath.IsWithinTolerance(angle, angleTol);
        }

        /// <summary>正しい場所に吸着させる（物理を止めて、位置と向きをぴったり合わせる）。</summary>
        private void Place(int i)
        {
            var rb = targets[i].GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
            }

            targets[i].SetPositionAndRotation(slots[i].position, slots[i].rotation);
            _placed[i] = true;
            RecomputeLocks();
            CheckCompleted();
        }

        private bool IsLocked(int i) => _locked.Contains(i.ToString());

        private void RecomputeLocks()
        {
            bool lockEnabled = config == null || config.lockPlacedParts;
            var inputs = new List<SocketLockInput>();
            for (int i = 0; i < targets.Length; i++)
            {
                string pre = prerequisites != null && i < prerequisites.Length ? prerequisites[i] : string.Empty;
                string[] list = string.IsNullOrEmpty(pre) ? new string[0] : pre.Split(',');
                for (int k = 0; k < list.Length; k++) list[k] = list[k].Trim();
                inputs.Add(new SocketLockInput(i.ToString(), _placed[i], list));
            }

            _locked = AssemblyLockRules.ComputeLocked(inputs, lockEnabled);
        }

        private void CheckCompleted()
        {
            for (int i = 0; i < _placed.Length; i++)
            {
                if (!_placed[i])
                {
                    return;
                }
            }

            _completed = true;
            if (board != null)
            {
                board.ShowMessage("組み立て完了！", "すべての部品が正しい位置に置けました。\nBackspace でやり直せます。");
            }
        }

        /// <summary>全部の部品を最初の位置（分解した状態）に戻す。</summary>
        private void ResetAll()
        {
            Drop();
            for (int i = 0; i < targets.Length; i++)
            {
                var rb = targets[i].GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = false;
                    rb.useGravity = true;
                }

                _placed[i] = false;
                ReturnHome(i);
            }

            _locked.Clear();
            _completed = false;
            if (board != null)
            {
                board.ShowDefault();
            }
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
            ReleaseAfterEmphasis(target);
            _running = null;
        }

        /// <summary>強調が終わったら物理に戻す。正しく置いた部品は固定のまま。</summary>
        private void ReleaseAfterEmphasis(Transform target)
        {
            int i = System.Array.IndexOf(targets, target);
            if (i >= 0 && _placed[i])
            {
                return;
            }

            SetKinematic(target, false);
        }

        private void StopEmphasis()
        {
            if (_running != null)
            {
                StopCoroutine(_running);
                _running = null;
            }

            _emphasis?.Restore();
            ReleaseAfterEmphasis(Target);
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

            int placedCount = 0;
            foreach (bool b in _placed ?? new bool[0]) if (b) placedCount++;
            string phase = _emphasis == null || _running == null ? (_held != null ? "掴んでいる" : "待機中") : PhaseLabel(_emphasis.Phase);
            string detail = _emphasis != null && _running != null
                ? $"　{_emphasis.PhaseElapsed:0.0} / {_emphasis.PhaseDuration:0.0} 秒　拡大率 s = {_emphasis.ScaleFactor:0.00}"
                : string.Empty;
            string text = $"空間的強調のデモ　対象：{(Target != null ? Target.name : "-")}　置けた部品 {placedCount}/{targets.Length}{(_completed ? "（完了）" : string.Empty)}\n" +
                          $"状態：{phase}{detail}\n" +
                          "左ドラッグ：部品を動かす（ホイール：前後、Q E・R F・Z X：回す）　右ドラッグ：見回す　矢印：移動\n" +
                          "P：強調　R：中止　Tab：対象を替える　Backspace：やり直す　K：この表示を消す";
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
