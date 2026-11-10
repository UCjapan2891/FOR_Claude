using UnityEngine;

namespace VRClass.Interaction
{
    /// <summary>
    /// 部品の名前と役割（黒板に表示する説明）。
    /// 文言は教材の内容なので、本番の授業では教材の原稿に合わせて直す。
    /// </summary>
    public sealed class PartDescription : MonoBehaviour
    {
        [Tooltip("部品の名前（例：プラッタ（円盤））")]
        public string displayName;

        [Tooltip("部品の役割の説明")]
        [TextArea(2, 6)] public string role;
    }
}
