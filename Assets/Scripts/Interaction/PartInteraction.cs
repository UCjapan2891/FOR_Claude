using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using VRClass.Core;
using VRClass.Experiment;
using VRClass.Logging;

namespace VRClass.Interaction
{
    /// <summary>
    /// 授業で動かす部品（SPEC 5.1：Collider ＋ Rigidbody ＋ XR Grab Interactable）。
    ///
    /// 掴めるかどうかは XRI の選択フィルターで切り替える。XRGrabInteractable を無効にすると
    /// ソケットに付いている部品まで外れて落ちるため、無効化は使わない。
    /// - ソケットからの選択は常に許可する（付いている部品はそのまま）
    /// - 手（コントローラー・デバッグ用マウス）からの選択は、IsGrabbable と入力モードで決める
    /// - 掴めなくしても、既に手に持っている部品はそのまま持っていられる（新しく掴むことだけを止める）。
    ///   releaseHeld = true のときだけ手から離す（強調の開始時など）。
    ///   XRI はフィルターを毎フレーム評価し直すので、false を返した時点で手に持っている部品は自動で離れる。
    /// </summary>
    [RequireComponent(typeof(XRGrabInteractable))]
    public sealed class PartInteraction : MonoBehaviour, IXRSelectFilter, IXRHoverFilter
    {
        [Tooltip("部品の識別名（LessonData の targetPartId と一致させる）。ログの part 列にも使う")]
        [SerializeField] private string partId;

        [Tooltip("回転対称の部品の対称軸。この軸まわりの回転はソケットの向きの判定から外す")]
        [SerializeField] private SymmetryAxis symmetryAxis = SymmetryAxis.None;

        [SerializeField] private InputModeController inputMode;
        [SerializeField] private SessionLog sessionLog;
        [SerializeField] private ExperimentConfig config;

        private XRGrabInteractable _grab;
        private Vector3 _homePosition;
        private Quaternion _homeRotation;

        public string PartId => partId;

        public SymmetryAxis SymmetryAxis => symmetryAxis;

        public XRGrabInteractable Grab => _grab != null ? _grab : (_grab = GetComponent<XRGrabInteractable>());

        /// <summary>手で新しく掴めるか（説明音声・強調の間は false）。</summary>
        public bool IsGrabbable { get; private set; } = true;

        /// <summary>true なら、手に持っている部品も離させる。</summary>
        public bool ReleaseHeld { get; private set; }

        /// <summary>今付いているソケット。付いていなければ null。PartSocket が設定する。</summary>
        public PartSocket CurrentSocket { get; internal set; }

        /// <summary>手（コントローラーかデバッグ用マウス）で持っているか。</summary>
        public bool IsHeldByHand
        {
            get
            {
                foreach (IXRSelectInteractor interactor in Grab.interactorsSelecting)
                {
                    if (!(interactor is XRSocketInteractor))
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>授業開始時の位置（SPEC 5.2 のリセットで戻す先）。</summary>
        public Vector3 HomePosition => _homePosition;

        public Quaternion HomeRotation => _homeRotation;

        /// <summary>手で掴んだ（ソケットからの選択は含まない）。</summary>
        public event Action<PartInteraction> Grabbed;

        /// <summary>手から離れた。</summary>
        public event Action<PartInteraction> Released;

        /// <summary>初期位置へ戻した（床に落ちた・リセット）。</summary>
        public event Action<PartInteraction> ReturnedHome;

        /// <summary>正しい位置に固定されているか（手で掴めない）。</summary>
        public bool IsLocked => CurrentSocket != null && CurrentSocket.IsLocked;

        /// <summary>プログラムが動かしている最中か（再配置・強調）。重力を切って空中に止め、床からの復帰もしない。</summary>
        public bool IsFloating { get; private set; }

        /// <summary>部品の見た目の中心（Renderer の bounds の中心）。注視角の d に使う。</summary>
        public Vector3 WorldCenter
        {
            get
            {
                Renderer r = GetComponentInChildren<Renderer>();
                return r != null ? r.bounds.center : transform.position;
            }
        }

        /// <summary>
        /// 空中に止める（true）／物理に戻す（false）。true の間は Rigidbody を kinematic・重力なしに保つ。
        /// 戻すときは、浮かせる前の kinematic・重力の設定に戻し、速度を 0 にする。
        /// </summary>
        public void SetFloating(bool floating)
        {
            var rb = GetComponent<Rigidbody>();
            if (floating == IsFloating || rb == null)
            {
                IsFloating = floating;
                return;
            }

            if (floating)
            {
                _floatSavedKinematic = rb.isKinematic;
                _floatSavedGravity = rb.useGravity;
                rb.isKinematic = true;
                rb.useGravity = false;
            }
            else
            {
                rb.isKinematic = _floatSavedKinematic;
                rb.useGravity = _floatSavedGravity;
                if (!rb.isKinematic)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
            }

            IsFloating = floating;
        }

        private bool _floatSavedKinematic;
        private bool _floatSavedGravity = true;

        // IXRSelectFilter / IXRHoverFilter
        public bool canProcess => isActiveAndEnabled;

        /// <param name="grabbable">手で新しく掴めるか</param>
        /// <param name="releaseHeld">掴めなくするとき、手に持っている部品も離させるか</param>
        public void SetGrabbable(bool grabbable, bool releaseHeld = false)
        {
            IsGrabbable = grabbable;
            ReleaseHeld = !grabbable && releaseHeld;
        }

        /// <summary>今の位置・向きを、リセットで戻す先として覚える。</summary>
        public void RememberHome()
        {
            _homePosition = transform.position;
            _homeRotation = transform.rotation;
        }

        public bool Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable)
        {
            return IsAllowed(interactor, interactor.IsSelecting(interactable));
        }

        public bool Process(IXRHoverInteractor interactor, IXRHoverInteractable interactable)
        {
            return IsAllowed(interactor, interactor is IXRSelectInteractor s && s.IsSelecting(Grab));
        }

        private bool IsAllowed(IXRInteractor interactor, bool alreadyHolding)
        {
            if (interactor is XRSocketInteractor)
            {
                return true;
            }

            if (IsLocked)
            {
                return false; // 正しい位置に固定済み
            }

            if (!IsGrabbable)
            {
                return alreadyHolding && !ReleaseHeld;
            }

            InputMode mode = inputMode != null ? inputMode.Current : InputMode.Both;
            return interactor is DebugMouseInteractor
                ? InputModeRules.AllowsKeyboardMouse(mode)
                : InputModeRules.AllowsController(mode);
        }

        private void Awake()
        {
            _grab = GetComponent<XRGrabInteractable>();
            RememberHome();
        }

        /// <summary>
        /// 机の上の初期位置へ戻す（床に落ちたとき、SPEC 5.2 のリセット）。
        /// 手に持っているとき・ソケットに付いているときは何もしない。reset をログに記録する。
        /// </summary>
        /// <returns>戻したら true</returns>
        public bool ReturnHome(string reason)
        {
            if (Grab.isSelected || IsFloating)
            {
                return false;
            }

            var rb = GetComponent<Rigidbody>();
            if (rb != null && !rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            if (rb != null)
            {
                rb.position = _homePosition;
                rb.rotation = _homeRotation;
            }

            transform.SetPositionAndRotation(_homePosition, _homeRotation);

            if (sessionLog != null)
            {
                sessionLog.Log(LogEvent.Reset, partId, "reason=" + reason);
            }

            ReturnedHome?.Invoke(this);
            return true;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (collision.collider.GetComponentInParent<FloorZone>() != null)
            {
                ReturnHome("floor");
            }
        }

        private void FixedUpdate()
        {
            if (IsFloating)
            {
                // XRI がソケットから外したときに物理の設定を戻すことがあるので、浮かせている間は保ち続ける
                var rb = GetComponent<Rigidbody>();
                if (rb != null && !Grab.isSelected && !rb.isKinematic)
                {
                    rb.isKinematic = true;
                    rb.useGravity = false;
                }

                return;
            }

            float limit = config != null ? config.fallResetY : -1f;
            if (transform.position.y < limit)
            {
                ReturnHome("fell");
            }
        }

        private void OnEnable()
        {
            Grab.selectFilters.Add(this);
            Grab.hoverFilters.Add(this);
            Grab.selectEntered.AddListener(OnSelectEntered);
            Grab.selectExited.AddListener(OnSelectExited);
        }

        private void OnDisable()
        {
            if (_grab == null)
            {
                return;
            }

            _grab.selectFilters.Remove(this);
            _grab.hoverFilters.Remove(this);
            _grab.selectEntered.RemoveListener(OnSelectEntered);
            _grab.selectExited.RemoveListener(OnSelectExited);
        }

        private void OnSelectEntered(SelectEnterEventArgs args)
        {
            if (args.interactorObject is XRSocketInteractor)
            {
                return;
            }

            if (sessionLog != null)
            {
                sessionLog.Log(LogEvent.Grab, partId, "input=" + InputName(args.interactorObject));
            }

            Grabbed?.Invoke(this);
        }

        private void OnSelectExited(SelectExitEventArgs args)
        {
            if (args.interactorObject is XRSocketInteractor)
            {
                return;
            }

            if (sessionLog != null)
            {
                string detail = "input=" + InputName(args.interactorObject) + (args.isCanceled ? ";canceled" : string.Empty);
                sessionLog.Log(LogEvent.Release, partId, detail);
            }

            Released?.Invoke(this);
        }

        private static string InputName(IXRInteractor interactor)
        {
            return interactor is DebugMouseInteractor ? "mouse" : "controller";
        }
    }
}
