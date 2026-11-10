using TMPro;
using UnityEngine;
using VRClass.Experiment;
using VRClass.Interaction;

namespace VRClass.Emphasis
{
    /// <summary>
    /// テロップ（SPEC 4.4）。World Space の Canvas ＋ TextMeshPro で、対象部品のすぐ上に出し、常に受講者の方を向かせる。
    /// 強調中に部品が動くとき、テロップを部品について動かすかは telopFollowsPart で切り替える
    /// （false なら、表示を始めた位置に止めておく）。
    /// Canvas の RectTransform の pivot は下端の中央（0.5, 0）にしておく（部品の上に下端をそろえるため）。
    /// 条件による違いはここには書かない（表示するかどうか・いつまでかは EmphasisController が決める）。
    /// </summary>
    public sealed class TelopView : MonoBehaviour
    {
        [SerializeField] private TMP_Text text;

        [Tooltip("表示・非表示を切り替えるもの（Canvas）。空ならこの GameObject")]
        [SerializeField] private GameObject root;

        [SerializeField] private ExperimentConfig config;

        [Tooltip("頭の Transform（XR Origin の Main Camera）。空なら Camera.main")]
        [SerializeField] private Transform head;

        private PartInteraction _target;
        private Vector3 _fixedPosition;

        public bool IsVisible { get; private set; }

        /// <summary>今表示している文言。表示していなければ空。</summary>
        public string CurrentText { get; private set; } = string.Empty;

        private Transform Head => head != null ? head : (Camera.main != null ? Camera.main.transform : null);

        private void Awake()
        {
            if (!IsVisible)
            {
                SetVisible(false);
            }
        }

        /// <summary>対象部品の上にテロップを出す。</summary>
        public void Show(string message, PartInteraction target)
        {
            _target = target;
            CurrentText = message ?? string.Empty;
            if (text != null)
            {
                text.text = CurrentText;
            }

            _fixedPosition = AnchorPosition();
            IsVisible = true;
            SetVisible(true);
            UpdatePlacement();
        }

        public void Hide()
        {
            IsVisible = false;
            CurrentText = string.Empty;
            _target = null;
            SetVisible(false);
        }

        private void LateUpdate()
        {
            if (IsVisible)
            {
                UpdatePlacement();
            }
        }

        private void UpdatePlacement()
        {
            bool follow = config == null || config.telopFollowsPart;
            Transform t = RootTransform();
            t.position = follow ? AnchorPosition() : _fixedPosition;

            // World Space の UI は、Canvas の正面（forward）が受講者から離れる向きのとき読める
            Transform h = Head;
            if (h != null)
            {
                Vector3 away = t.position - h.position;
                if (away.sqrMagnitude > 1e-8f)
                {
                    t.rotation = Quaternion.LookRotation(away, Vector3.up);
                }
            }
        }

        /// <summary>部品の上端の中央 ＋ すき間。部品がなければ今の位置。</summary>
        private Vector3 AnchorPosition()
        {
            if (_target == null)
            {
                return RootTransform().position;
            }

            Renderer[] renderers = _target.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                return _target.transform.position;
            }

            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                b.Encapsulate(renderers[i].bounds);
            }

            float gap = config != null ? config.telopGapAbovePart : 0f;
            return new Vector3(b.center.x, b.max.y + gap, b.center.z);
        }

        private Transform RootTransform() => root != null ? root.transform : transform;

        private void SetVisible(bool visible)
        {
            GameObject target = root != null ? root : gameObject;
            if (target == gameObject)
            {
                // 自分自身を非表示にすると LateUpdate が止まるので、子（Canvas の中身）だけを切り替える
                foreach (Transform child in transform)
                {
                    child.gameObject.SetActive(visible);
                }

                return;
            }

            target.SetActive(visible);
        }
    }
}
