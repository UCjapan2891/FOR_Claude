using System;
using System.Collections;
using UnityEngine;
using VRClass.Core;
using VRClass.Experiment;

namespace VRClass.Emphasis
{
    /// <summary>空間的強調の段階（SPEC 4.3）。</summary>
    public enum EmphasisPhase
    {
        None,
        Pull,     // ① 引き寄せ
        Scale,    // ② 拡大
        Rotate,   // ③ 回転
        Observe,  // （観察）
        Return,   // ④ 復帰
        Done,
    }

    /// <summary>
    /// 空間的強調の動き（SPEC 4.3）：① 引き寄せ → ② 拡大 → ③ 回転 →（観察）→ ④ 復帰 を1つずつ順に行う。
    /// 数式は Core（RelocationMath・Easing・EmphasisMath）、値は ExperimentConfig から読む。
    /// 位置は部品の見た目の中心（Renderer の bounds の中心）で扱う（再配置と同じ）。
    /// 開始前の位置・回転・大きさを保存し、<see cref="Restore"/> ですぐ元に戻せる（一時停止用）。
    /// 条件による違いはここには書かない（条件1・2の切り替えは段階6の EmphasisController で行う）。
    /// 部品の物理（Rigidbody）とソケットの扱いは呼び出し側（PartPose）が行う。
    /// </summary>
    public sealed class SpatialEmphasis
    {
        private readonly Transform _target;
        private readonly Vector3 _p0;
        private readonly Quaternion _r0;
        private readonly Vector3 _s0;
        private readonly Vector3 _centerLocal;
        private readonly float _diameter;

        public SpatialEmphasis(Transform target)
        {
            _target = target != null ? target : throw new ArgumentNullException(nameof(target));
            _p0 = target.position;
            _r0 = target.rotation;
            _s0 = target.localScale;

            Bounds b = WorldBounds(target);
            _centerLocal = target.InverseTransformPoint(b.center);
            _diameter = Mathf.Max(EmphasisMath.BoundingSphereDiameter(b.size), 1e-4f);
        }

        public EmphasisPhase Phase { get; private set; } = EmphasisPhase.None;

        /// <summary>今の段階の経過秒。</summary>
        public float PhaseElapsed { get; private set; }

        /// <summary>今の段階の長さ（秒）。</summary>
        public float PhaseDuration { get; private set; }

        /// <summary>計算した拡大率 s（1 未満は 1）。</summary>
        public float ScaleFactor { get; private set; } = 1f;

        /// <summary>段階が変わったとき（emphasis_phase の記録に使う）。</summary>
        public event Action<EmphasisPhase> PhaseStarted;

        /// <summary>
        /// 強調を最後まで行うコルーチン。終わると元の位置・回転・大きさにぴったり戻る。
        /// </summary>
        /// <param name="head">頭（カメラ）。引き寄せ先は頭の水平の正面 D</param>
        /// <param name="includePull">false なら引き寄せず、その場で拡大・回転する（デモ用）</param>
        public IEnumerator Play(Transform head, ExperimentConfig config, bool includePull = true)
        {
            if (head == null || config == null)
            {
                yield break;
            }

            Vector3 c0 = Center();
            Vector3 pt = c0;
            float viewDistance = Vector3.Distance(head.position, c0);
            if (includePull && RelocationMath.TryHorizontalDirection(head.forward, head.up, out Vector3 dir))
            {
                pt = RelocationMath.PullTarget(head.position, dir, config.frontDistance, config.minHeadDistance);
                viewDistance = RelocationMath.PullDistance(config.frontDistance, config.minHeadDistance);
            }

            ScaleFactor = EmphasisMath.ScaleFactor(viewDistance, config.targetViewAngleDeg, _diameter);

            // ① 引き寄せ：p(t) = p0 + (pt − p0) × e(t)
            if (includePull)
            {
                foreach (float t in Run(EmphasisPhase.Pull, config.pullDurationSec))
                {
                    SetCenter(RelocationMath.PullPosition(c0, pt, t));
                    yield return null;
                }

                SetCenter(pt);
            }

            // ② 拡大：1 → s を e(t) で
            foreach (float t in Run(EmphasisPhase.Scale, config.scaleDurationSec))
            {
                SetScale(Easing.EasedLerp(1f, ScaleFactor, t), pt);
                yield return null;
            }

            SetScale(ScaleFactor, pt);

            // ③ 回転：鉛直軸まわりに1回転（ω 度/秒で等速）
            float rotateDuration = EmphasisMath.RotationDurationSec(config.rotationSpeedDegPerSec);
            float applied = 0f;
            foreach (float t in Run(EmphasisPhase.Rotate, rotateDuration))
            {
                float angle = EmphasisMath.RotationAngleDeg(t * rotateDuration, config.rotationSpeedDegPerSec);
                _target.RotateAround(Center(), Vector3.up, angle - applied);
                applied = angle;
                yield return null;
            }

            _target.rotation = _r0; // 1回転で元の向き
            SetScale(ScaleFactor, pt);

            // （観察）拡大したまま止める
            if (config.useObservePhase)
            {
                foreach (float _ in Run(EmphasisPhase.Observe, config.observeDurationSec))
                {
                    yield return null;
                }
            }

            // ④ 復帰：逆の動きで元の位置・大きさへ
            foreach (float t in Run(EmphasisPhase.Return, config.returnDurationSec))
            {
                float e = Easing.SmoothStep(t);
                SetScale(Mathf.Lerp(ScaleFactor, 1f, e), Vector3.Lerp(pt, c0, e));
                yield return null;
            }

            Restore();
            Begin(EmphasisPhase.Done, 0f);
        }

        /// <summary>保存した位置・回転・大きさへすぐ戻す。</summary>
        public void Restore()
        {
            _target.localScale = _s0;
            _target.SetPositionAndRotation(_p0, _r0);
        }

        // ---- 内部 ----

        /// <summary>段階を始め、毎フレームの進み具合 t（0〜1、最後は必ず 1）を返す。</summary>
        private System.Collections.Generic.IEnumerable<float> Run(EmphasisPhase phase, float duration)
        {
            Begin(phase, duration);
            if (duration <= 0f)
            {
                yield return 1f;
                yield break;
            }

            while (PhaseElapsed < duration)
            {
                PhaseElapsed += Time.deltaTime;
                yield return Easing.Progress(PhaseElapsed, duration);
            }
        }

        private void Begin(EmphasisPhase phase, float duration)
        {
            Phase = phase;
            PhaseElapsed = 0f;
            PhaseDuration = duration;
            PhaseStarted?.Invoke(phase);
        }

        private Vector3 Center() => _target.TransformPoint(_centerLocal);

        private void SetCenter(Vector3 worldCenter)
        {
            _target.position += worldCenter - Center();
        }

        private void SetScale(float factor, Vector3 worldCenter)
        {
            _target.localScale = _s0 * factor;
            SetCenter(worldCenter);
        }

        private static Bounds WorldBounds(Transform t)
        {
            Renderer[] renderers = t.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                return new Bounds(t.position, Vector3.one * 0.1f);
            }

            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                b.Encapsulate(renderers[i].bounds);
            }

            return b;
        }
    }
}
