using System;
using System.Globalization;
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
    /// 部品を付ける場所（SPEC 5.1：XR Socket Interactor）。
    /// - 正しい部品（acceptedPartId）だけを受け付ける
    /// - 向きのずれが socketAngleToleranceDeg（[15]°）以内、距離が socketSnapDistance（[0.05] m）以内なら吸着
    /// - 吸着したら socket_ok をログに記録する
    /// XRI のソケットは手に持っている部品を選ばないので、判定は手を離した時点で行われる。
    /// </summary>
    [RequireComponent(typeof(XRSocketInteractor))]
    public sealed class PartSocket : MonoBehaviour, IXRSelectFilter, IXRHoverFilter
    {
        [Tooltip("ソケットの識別名（ログの detail に出す）")]
        [SerializeField] private string socketId;

        [Tooltip("受け付ける部品の partId")]
        [SerializeField] private string acceptedPartId;

        [Tooltip("先に置くべきソケットの socketId（この部品の下にある部品）。全部固定済みのときだけ、この部品も固定する")]
        [SerializeField] private string[] prerequisiteSocketIds = new string[0];

        [SerializeField] private ExperimentConfig config;
        [SerializeField] private SessionLog sessionLog;

        private XRSocketInteractor _socket;
        private float _lastAngle = float.NaN;
        private float _lastDistance = float.NaN;

        public string SocketId => socketId;

        public string AcceptedPartId => acceptedPartId;

        public XRSocketInteractor Socket => _socket != null ? _socket : (_socket = GetComponent<XRSocketInteractor>());

        /// <summary>今付いている部品。なければ null。</summary>
        public PartInteraction CurrentPart { get; private set; }

        /// <summary>正しい部品が付いているか。</summary>
        public bool IsFilledCorrectly => CurrentPart != null && CurrentPart.PartId == acceptedPartId;

        public System.Collections.Generic.IReadOnlyList<string> PrerequisiteSocketIds => prerequisiteSocketIds;

        /// <summary>付いている部品を固定しているか（手で外せない）。AssemblyLock が設定する。</summary>
        public bool IsLocked { get; internal set; }

        public event Action<PartSocket, PartInteraction> Attached;

        public event Action<PartSocket, PartInteraction> Detached;

        public bool canProcess => isActiveAndEnabled;

        public bool Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable)
        {
            PartInteraction part = FindPart(interactable);
            if (part == null || part.PartId != acceptedPartId)
            {
                return false;
            }

            // 既に付いている部品は、そのまま付けておく
            if (CurrentPart == part)
            {
                return true;
            }

            Transform attach = Socket.attachTransform != null ? Socket.attachTransform : transform;
            _lastAngle = OrientationMath.AngleDeg(part.transform.rotation, attach.rotation, part.SymmetryAxis);
            _lastDistance = Vector3.Distance(part.transform.position, attach.position);

            float angleTol = config != null ? config.socketAngleToleranceDeg : 15f;
            float distTol = config != null ? config.socketSnapDistance : 0.05f;
            return OrientationMath.IsWithinTolerance(_lastAngle, angleTol) && _lastDistance <= distTol;
        }

        public bool Process(IXRHoverInteractor interactor, IXRHoverInteractable interactable)
        {
            PartInteraction part = FindPart(interactable);
            return part != null && part.PartId == acceptedPartId;
        }

        private static PartInteraction FindPart(IXRInteractable interactable)
        {
            return interactable is Component c ? c.GetComponent<PartInteraction>() : null;
        }

        private void Awake()
        {
            _socket = GetComponent<XRSocketInteractor>();
        }

        private void OnEnable()
        {
            Socket.selectFilters.Add(this);
            Socket.hoverFilters.Add(this);
            Socket.selectEntered.AddListener(OnSelectEntered);
            Socket.selectExited.AddListener(OnSelectExited);
        }

        private void OnDisable()
        {
            if (_socket == null)
            {
                return;
            }

            _socket.selectFilters.Remove(this);
            _socket.hoverFilters.Remove(this);
            _socket.selectEntered.RemoveListener(OnSelectEntered);
            _socket.selectExited.RemoveListener(OnSelectExited);
        }

        private void OnSelectEntered(SelectEnterEventArgs args)
        {
            PartInteraction part = FindPart(args.interactableObject);
            if (part == null)
            {
                return;
            }

            CurrentPart = part;
            part.CurrentSocket = this;

            if (sessionLog != null)
            {
                string detail = "socket=" + socketId
                                + ";angle=" + Format(_lastAngle)
                                + ";dist=" + Format(_lastDistance);
                sessionLog.Log(LogEvent.SocketOk, part.PartId, detail);
            }

            Attached?.Invoke(this, part);
        }

        private void OnSelectExited(SelectExitEventArgs args)
        {
            PartInteraction part = FindPart(args.interactableObject);
            if (part == null)
            {
                return;
            }

            if (CurrentPart == part)
            {
                CurrentPart = null;
                IsLocked = false;
            }

            if (part.CurrentSocket == this)
            {
                part.CurrentSocket = null;
            }

            Detached?.Invoke(this, part);
        }

        private static string Format(float v)
        {
            return float.IsNaN(v) ? "NaN" : v.ToString("F3", CultureInfo.InvariantCulture);
        }
    }
}
