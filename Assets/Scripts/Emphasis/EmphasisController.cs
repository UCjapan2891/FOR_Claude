using System.Collections;
using System.Globalization;
using UnityEngine;
using VRClass.Core;
using VRClass.Experiment;
using VRClass.Interaction;
using VRClass.Lesson;
using VRClass.Logging;

namespace VRClass.Emphasis
{
    /// <summary>
    /// 要点の演出（SPEC 3章 7、4.3、4.4）。**条件1と条件2の違いはこのクラスの中だけで扱う**（CLAUDE.md 3章）。
    /// どちらを行うかは EmphasisPlan（Core）で決める。
    /// - 条件2で注視できた：テロップ＋空間的強調（SpatialEmphasis）
    /// - 条件1、または注視に失敗した（gaze_fail）：テロップのみを Ttotal の間表示して止まる
    /// 両方とも、テロップの表示と解説音声の再生は同じタイミング（開始と同時）。長さはどちらも Ttotal。
    /// 掴みの無効化と手から離すことは LessonController が要点に入るときに行う。
    /// 記録：emphasis_start（mode, gaze, total）、emphasis_phase（phase。空間的強調のときだけ）、emphasis_end（elapsed）。
    /// 解説音声は audio_start / audio_end（detail の先頭に commentary）。
    /// </summary>
    public sealed class EmphasisController : MonoBehaviour
    {
        [SerializeField] private ExperimentConfig config;
        [SerializeField] private SessionLog sessionLog;
        [SerializeField] private TelopView telop;

        [Tooltip("解説音声を鳴らす AudioSource（2D）")]
        [SerializeField] private AudioSource commentarySource;

        [Tooltip("頭の Transform（XR Origin の Main Camera）。空なら Camera.main")]
        [SerializeField] private Transform head;

        private Condition _condition = Condition.TelopOnly;
        private bool _conditionSet;
        private SpatialEmphasis _spatial;
        private PartInteraction _target;
        private float _elapsed;
        private bool _commentaryPlaying;

        /// <summary>この授業の条件。</summary>
        public Condition Condition => _condition;

        /// <summary>演出の最中か。</summary>
        public bool IsRunning { get; private set; }

        /// <summary>今の演出（デバッグ表示用）。</summary>
        public EmphasisMode CurrentMode { get; private set; }

        /// <summary>今の空間的強調（テロップのみのときは null）。デバッグ表示用。</summary>
        public SpatialEmphasis CurrentSpatial => _spatial;

        /// <summary>演出を始めてからの秒。</summary>
        public float Elapsed => _elapsed;

        private Transform Head => head != null ? head : (Camera.main != null ? Camera.main.transform : null);

        /// <summary>授業を始める前に、この授業の条件を設定する（割り当ては ParticipantAssignment）。</summary>
        public void SetCondition(Condition condition)
        {
            _condition = condition;
            _conditionSet = true;
        }

        /// <summary>
        /// 要点の演出を行う。終わると部品は復帰先（再配置する前の位置）にあり、テロップと解説音声は止まっている。
        /// ソケットへの付け直しと物理の復帰は呼び出し側（PartPose.Restore）が行う。
        /// </summary>
        /// <param name="context">要点の情報（テロップの文言・解説音声・対象部品）</param>
        /// <param name="pose">再配置する前の元の状態</param>
        /// <param name="gazeOk">注視できたか（false なら gaze_fail）</param>
        public IEnumerator Run(KeyPointContext context, PartPose pose, bool gazeOk)
        {
            if (config == null || context == null || context.Target == null)
            {
                Debug.LogError("EmphasisController：設定か対象部品がないため、演出をせずに進みます。", this);
                yield break;
            }

            if (!_conditionSet)
            {
                Debug.LogWarning("EmphasisController：条件が設定されていません。条件1（テロップのみ）として扱います。", this);
            }

            _target = context.Target;
            EmphasisTiming timing = config.EmphasisTiming;
            CurrentMode = EmphasisPlan.ChooseMode(_condition, gazeOk);
            float total = EmphasisPlan.DurationSec(CurrentMode, timing);
            _elapsed = 0f;
            IsRunning = true;

            Log(LogEvent.EmphasisStart, string.Format(CultureInfo.InvariantCulture,
                "mode={0};gaze={1};total={2:F3}", EmphasisPlan.ModeName(CurrentMode), gazeOk ? "ok" : "fail", total));

            if (telop != null)
            {
                telop.Show(context.Stage != null ? context.Stage.telopText : string.Empty, _target);
            }

            PlayCommentary(context.Stage != null ? context.Stage.commentaryClip : null);

            if (CurrentMode == EmphasisMode.TelopAndSpatial)
            {
                // 条件2：空間的強調。p0 は今の位置（再配置後のこともある）、④復帰は再配置する前の元の位置へ
                pose?.DetachForMove();
                _spatial = new SpatialEmphasis(_target.transform, pose != null ? pose.Position : (Vector3?)null);
                _spatial.PhaseStarted += OnPhaseStarted;
                IEnumerator play = _spatial.Play(Head, config);
                while (play.MoveNext())
                {
                    _elapsed = _spatial.Elapsed;
                    yield return play.Current;
                }

                _elapsed = _spatial.Elapsed;
                _spatial.PhaseStarted -= OnPhaseStarted;
                _spatial = null;
            }
            else
            {
                // 条件1（または注視の失敗）：テロップを出したまま Ttotal 止まる
                var clock = new EmphasisClock(total);
                while (!clock.IsFinished)
                {
                    yield return null;
                    clock.Tick(Time.deltaTime);
                    _elapsed = clock.Elapsed;
                }
            }

            Finish(cancelled: false);
        }

        /// <summary>一時停止・中断：部品をすぐ戻し、テロップと解説音声を止める。コルーチンは呼び出し側が止める。</summary>
        public void Cancel()
        {
            if (_spatial != null)
            {
                _spatial.PhaseStarted -= OnPhaseStarted;
                _spatial.Restore();
                _spatial = null;
            }

            if (IsRunning)
            {
                Finish(cancelled: true);
            }
        }

        private void Update()
        {
            // 解説音声が Ttotal より前に終わったときの audio_end
            if (_commentaryPlaying && commentarySource != null && !commentarySource.isPlaying)
            {
                _commentaryPlaying = false;
                Log(LogEvent.AudioEnd, "commentary");
            }
        }

        private void Finish(bool cancelled)
        {
            StopCommentary(cancelled ? "commentary;cancelled" : "commentary;cut");
            if (telop != null)
            {
                telop.Hide();
            }

            Log(LogEvent.EmphasisEnd, string.Format(CultureInfo.InvariantCulture,
                "elapsed={0:F3}{1}", _elapsed, cancelled ? ";cancelled" : string.Empty));
            IsRunning = false;
            _target = null;
        }

        private void OnPhaseStarted(EmphasisPhase phase)
        {
            if (phase != EmphasisPhase.Done)
            {
                Log(LogEvent.EmphasisPhase, "phase=" + EmphasisPlan.PhaseName(phase));
            }
        }

        private void PlayCommentary(AudioClip clip)
        {
            if (clip == null || commentarySource == null)
            {
                return;
            }

            commentarySource.spatialBlend = 0f; // SPEC 4.5：2D
            commentarySource.clip = clip;
            commentarySource.Play();
            _commentaryPlaying = true;
            Log(LogEvent.AudioStart, "commentary;clip=" + clip.name);
        }

        /// <summary>まだ鳴っていれば止めて audio_end を書く（Ttotal より長い音声は切る）。</summary>
        private void StopCommentary(string detail)
        {
            if (!_commentaryPlaying)
            {
                return;
            }

            _commentaryPlaying = false;
            bool stillPlaying = commentarySource != null && commentarySource.isPlaying;
            if (stillPlaying)
            {
                commentarySource.Stop();
            }

            Log(LogEvent.AudioEnd, stillPlaying ? detail : "commentary");
        }

        private void Log(string eventName, string detail)
        {
            if (sessionLog != null)
            {
                sessionLog.Log(eventName, _target != null ? _target.PartId : null, detail);
            }
        }
    }
}
