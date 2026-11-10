using System;
using System.Collections;
using UnityEngine;
using VRClass.Core;
using VRClass.Experiment;

namespace VRClass.Emphasis
{
    /// <summary>
    /// 空間的強調の動き（SPEC 4.3）：① 引き寄せ → ② 拡大 → ③ 回転 →（観察）→ ④ 復帰 を1つずつ順に行う。
    /// 数式は Core（RelocationMath・Easing・EmphasisMath）、値は ExperimentConfig から読む。
    /// 位置は部品の見た目の中心（Renderer の bounds の中心）で扱う（再配置と同じ）。
    /// 開始前の位置・回転・大きさを保存し、<see cref="Restore"/> ですぐ元に戻せる（一時停止用）。
    /// 段階の切り替えは EmphasisTimeline（Core）で、開始からの経過時間1本で決める（条件1の停止 Ttotal と同じ長さになる）。
    /// 条件による違いはここには書かない（条件1・2の切り替えは EmphasisController で行う）。
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
        private readonly Vector3 _home;
        private Vector3 _c0;
        private Vector3 _cHome;
        private Vector3 _pt;
        private float _rotationSpeed = 1f;

        /// <param name="target">動かす部品</param>
        /// <param name="homePosition">
        /// ④復帰で戻す位置（Transform の位置）。再配置した部品は、再配置する前の元の位置を渡す（SPEC 4.2）。
        /// 省略すると、強調を始めたときの位置へ戻す。向き・大きさは再配置で変わらないので、開始時のものへ戻す。
        /// </param>
        public SpatialEmphasis(Transform target, Vector3? homePosition = null)
        {
            _target = target != null ? target : throw new ArgumentNullException(nameof(target));
            _p0 = target.position;
            _home = homePosition ?? _p0;
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

        /// <summary>強調を始めてからの秒（Time.deltaTime の積算）。</summary>
        public float Elapsed { get; private set; }

        /// <summary>計算した拡大率 s（1 未満は 1）。</summary>
        public float ScaleFactor { get; private set; } = 1f;

        /// <summary>段階が変わったとき（emphasis_phase の記録に使う）。</summary>
        public event Action<EmphasisPhase> PhaseStarted;

        /// <summary>
        /// 強調を最後まで行うコルーチン。終わると復帰先の位置と元の回転・大きさにぴったり戻る。
        /// 長さは EmphasisTimeline の合計（Ttotal）。
        /// </summary>
        /// <param name="head">頭（カメラ）。引き寄せ先は頭の水平の正面 D</param>
        /// <param name="includePull">false なら引き寄せず、その場で拡大・回転する（デモ用）</param>
        public IEnumerator Play(Transform head, ExperimentConfig config, bool includePull = true)
        {
            Elapsed = 0f;
            if (head == null || config == null)
            {
                Debug.LogError("SpatialEmphasis：頭（カメラ）か設定がないため、強調をしません。");
                yield break;
            }

            _c0 = Center();
            _cHome = _c0 + (_home - _target.position);
            _pt = _c0;
            float viewDistance = Vector3.Distance(head.position, _c0);
            if (includePull && RelocationMath.TryHorizontalDirection(head.forward, head.up, out Vector3 dir))
            {
                _pt = RelocationMath.PullTarget(head.position, dir, config.frontDistance, config.minHeadDistance);
                viewDistance = RelocationMath.PullDistance(config.frontDistance, config.minHeadDistance);
            }

            ScaleFactor = EmphasisMath.ScaleFactor(viewDistance, config.targetViewAngleDeg, _diameter);
            _rotationSpeed = config.rotationSpeedDegPerSec;

            var timeline = new EmphasisTimeline(config.EmphasisTiming, includePull);
            var clock = new EmphasisClock(timeline.TotalSec);
            int current = -1;
            while (true)
            {
                int index = timeline.Locate(clock.Elapsed, out float t);

                // 段階が進んだら、前の段階を最後の状態にしてから次を始める（1フレームで飛んだ段階も順に）
                while (current < index)
                {
                    if (current >= 0)
                    {
                        Apply(timeline.PhaseAt(current), 1f, timeline.DurationAt(current));
                    }

                    current++;
                    if (current < timeline.Count)
                    {
                        Begin(timeline.PhaseAt(current), timeline.DurationAt(current));
                    }
                }

                if (index >= timeline.Count)
                {
                    break;
                }

                PhaseElapsed = clock.Elapsed - timeline.StartAt(index);
                Apply(timeline.PhaseAt(index), t, timeline.DurationAt(index));
                yield return null;
                clock.Tick(Time.deltaTime);
                Elapsed = clock.Elapsed;
            }

            Restore();
            Begin(EmphasisPhase.Done, 0f);
        }

        /// <summary>復帰先の位置と、保存した回転・大きさへすぐ戻す。</summary>
        public void Restore()
        {
            _target.localScale = _s0;
            _target.SetPositionAndRotation(_home, _r0);
        }

        // ---- 内部 ----

        /// <summary>段階 phase の進み具合 t（0〜1）の状態にする。</summary>
        private void Apply(EmphasisPhase phase, float t, float duration)
        {
            switch (phase)
            {
                case EmphasisPhase.Pull:
                    // ① 引き寄せ：p(t) = p0 + (pt − p0) × e(t)
                    SetCenter(RelocationMath.PullPosition(_c0, _pt, t));
                    break;

                case EmphasisPhase.Scale:
                    // ② 拡大：1 → s を e(t) で
                    SetScale(Easing.EasedLerp(1f, ScaleFactor, t), _pt);
                    break;

                case EmphasisPhase.Rotate:
                    // ③ 回転：鉛直軸まわりに1回転（ω 度/秒で等速、360° で止める）
                    float angle = EmphasisMath.RotationAngleDeg(t * duration, _rotationSpeed);
                    _target.rotation = Quaternion.AngleAxis(angle, Vector3.up) * _r0;
                    SetScale(ScaleFactor, _pt);
                    break;

                case EmphasisPhase.Observe:
                    // （観察）拡大したまま止める
                    _target.rotation = _r0;
                    SetScale(ScaleFactor, _pt);
                    break;

                case EmphasisPhase.Return:
                    // ④ 復帰：逆の動きで元の位置・大きさへ
                    _target.rotation = _r0;
                    float e = Easing.SmoothStep(t);
                    SetScale(Mathf.Lerp(ScaleFactor, 1f, e), Vector3.Lerp(_pt, _cHome, e));
                    break;
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
