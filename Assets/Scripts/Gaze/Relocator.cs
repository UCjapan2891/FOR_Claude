using System.Globalization;
using UnityEngine;
using VRClass.Core;
using VRClass.Experiment;
using VRClass.Interaction;
using VRClass.Logging;

namespace VRClass.Gaze
{
    /// <summary>
    /// 再配置（SPEC 4.2）：p_new = p_h + D × f / |f|。f は HMD の正面を水平に投影した向き、高さは頭の高さ。
    /// 部品の中心が p_new に来るように置き、向きは変えない。他の部品と重なれば [0.1] m ずつ奥へ（上限 [10] 回）。
    /// relocate を記録する。部品は PartPose.DetachForMove で空中に止めておく（呼び出し側）。
    /// </summary>
    public sealed class Relocator : MonoBehaviour
    {
        [SerializeField] private ExperimentConfig config;
        [SerializeField] private SessionLog sessionLog;

        private static readonly Collider[] Hits = new Collider[32];

        /// <summary>部品を頭の正面へ動かす。動かせたら true。</summary>
        /// <param name="count">何回目の再配置か（ログ用）</param>
        public bool Relocate(PartInteraction part, Transform head, int count)
        {
            if (part == null || head == null)
            {
                return false;
            }

            if (!RelocationMath.TryHorizontalDirection(head.forward, head.up, out Vector3 dir))
            {
                Debug.LogWarning("頭の正面の水平方向が決まらないため、再配置できませんでした。", this);
                return false;
            }

            Vector3 centerOffset = part.WorldCenter - part.transform.position;
            Vector3 halfExtents = HalfExtents(part);
            Vector3 basePos = RelocationMath.RelocatePosition(head.position, dir, config.frontDistance);

            Vector3 center = RelocationMath.FindFreePosition(basePos, dir, config.relocateShiftStep, config.relocateMaxShifts,
                c => IsOccupied(part, c, halfExtents), out int shifts, out bool exhausted);

            Vector3 pivot = center - centerOffset;
            part.transform.position = pivot;
            var rb = part.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.position = pivot;
            }

            if (sessionLog != null)
            {
                string detail = string.Format(CultureInfo.InvariantCulture,
                    "n={0};shifts={1};x={2:F3};y={3:F3};z={4:F3}{5}",
                    count, shifts, center.x, center.y, center.z, exhausted ? ";exhausted" : string.Empty);
                sessionLog.Log(LogEvent.Relocate, part.PartId, detail);
            }

            return true;
        }

        /// <summary>中心 c に置いたとき、他の部品の当たり判定と重なるか（実験台・パネルなどは見ない）。</summary>
        private static bool IsOccupied(PartInteraction self, Vector3 c, Vector3 halfExtents)
        {
            int n = Physics.OverlapBoxNonAlloc(c, halfExtents, Hits, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var other = Hits[i].GetComponentInParent<PartInteraction>();
                if (other != null && other != self)
                {
                    return true;
                }
            }

            return false;
        }

        private static Vector3 HalfExtents(PartInteraction part)
        {
            Collider[] colliders = part.GetComponentsInChildren<Collider>();
            bool has = false;
            var bounds = new Bounds(part.transform.position, Vector3.zero);
            foreach (Collider c in colliders)
            {
                if (c.isTrigger) continue;
                if (!has) { bounds = c.bounds; has = true; }
                else bounds.Encapsulate(c.bounds);
            }

            return has ? bounds.extents : Vector3.one * 0.05f;
        }
    }
}
