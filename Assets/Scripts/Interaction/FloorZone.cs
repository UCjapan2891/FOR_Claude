using UnityEngine;

namespace VRClass.Interaction
{
    /// <summary>
    /// 床の印。部品がこの Collider に触れたら、机の上の初期位置へ戻す（PartInteraction が見る）。
    /// スキャンした部屋では、メッシュではなく、重ねた平らな床の Collider に付ける（SPEC 1.1）。
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class FloorZone : MonoBehaviour
    {
    }
}
