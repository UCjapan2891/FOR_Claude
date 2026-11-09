using UnityEngine;

namespace VRClass.Interaction
{
    /// <summary>
    /// ヒント（SPEC 5.2）：正しい場所に、部品と同じ形を半透明に光らせて出す。
    /// 「触れる」の段階では、部品そのものに重ねて光らせる。
    /// 消すのは呼び出し側（段階の完了・一時停止で Hide）。
    /// </summary>
    public sealed class HintPresenter : MonoBehaviour
    {
        [Tooltip("半透明の光る材質（URP Unlit・Transparent）")]
        [SerializeField] private Material hintMaterial;

        [Tooltip("点滅の速さ（回/秒）")]
        [SerializeField] private float pulseHz = 1.5f;

        [Tooltip("部品より少し大きく出す倍率")]
        [SerializeField] private float inflate = 1.06f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private GameObject _ghost;
        private MeshRenderer _renderer;
        private MaterialPropertyBlock _block;
        private Transform _follow;
        private Color _baseColor = new Color(1f, 0.85f, 0.1f, 0.5f);

        public bool IsShowing => _ghost != null && _ghost.activeSelf;

        /// <summary>部品の形を、場所 at（ソケットなど）に出す。at が null なら部品に重ねて追いかける。</summary>
        public void Show(PartInteraction part, Transform at)
        {
            if (part == null)
            {
                return;
            }

            var meshFilter = part.GetComponentInChildren<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                return;
            }

            EnsureGhost();
            _ghost.GetComponent<MeshFilter>().sharedMesh = meshFilter.sharedMesh;
            _follow = at != null ? at : meshFilter.transform;
            Vector3 scale = meshFilter.transform.lossyScale * inflate;
            _ghost.transform.localScale = scale;
            Place();
            _ghost.SetActive(true);
        }

        public void Hide()
        {
            if (_ghost != null)
            {
                _ghost.SetActive(false);
            }

            _follow = null;
        }

        private void LateUpdate()
        {
            if (!IsShowing)
            {
                return;
            }

            Place();
            float a = Mathf.Lerp(0.15f, _baseColor.a, 0.5f + 0.5f * Mathf.Sin(Time.time * pulseHz * 2f * Mathf.PI));
            _block.SetColor(BaseColorId, new Color(_baseColor.r, _baseColor.g, _baseColor.b, a));
            _renderer.SetPropertyBlock(_block);
        }

        private void Place()
        {
            if (_follow != null)
            {
                _ghost.transform.SetPositionAndRotation(_follow.position, _follow.rotation);
            }
        }

        private void EnsureGhost()
        {
            if (_ghost != null)
            {
                return;
            }

            _ghost = new GameObject("Hint Ghost");
            _ghost.transform.SetParent(transform, false);
            _ghost.AddComponent<MeshFilter>();
            _renderer = _ghost.AddComponent<MeshRenderer>();
            _renderer.sharedMaterial = hintMaterial;
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _block = new MaterialPropertyBlock();
            if (hintMaterial != null && hintMaterial.HasProperty(BaseColorId))
            {
                _baseColor = hintMaterial.GetColor(BaseColorId);
            }
        }
    }
}
