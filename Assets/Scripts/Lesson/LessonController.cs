using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VRClass.Core;
using VRClass.Experiment;
using VRClass.Interaction;
using VRClass.Logging;

namespace VRClass.Lesson
{
    /// <summary>
    /// LessonData を SPEC 3章の流れで進める。状態遷移そのものは LessonFlow（Core）に任せ、
    /// ここでは音声・部品の掴み・ログを動かす。条件による分岐は書かない（CLAUDE.md 3章）。
    /// </summary>
    public sealed class LessonController : MonoBehaviour
    {
        public const string RemoveHmdMessage = "授業は終わりです。HMD を外してください";

        [SerializeField] private ExperimentConfig config;
        [SerializeField] private SessionLog sessionLog;

        [Tooltip("説明音声を流す AudioSource（2D にする）")]
        [SerializeField] private AudioSource narrationSource;

        [SerializeField] private LessonMessageView messageView;

        private readonly Dictionary<string, PartInteraction> _parts = new Dictionary<string, PartInteraction>();
        private readonly List<PartSocket> _sockets = new List<PartSocket>();
        private IKeyPointHandler _keyPointHandler = new NoOpKeyPointHandler();
        private LessonData _data;
        private Coroutine _running;

        public LessonFlow Flow { get; private set; }

        public LessonData Data => _data;

        /// <summary>現在の段階のデータ。開始前は null。</summary>
        public StageData CurrentStage => Flow != null && Flow.StageIndex >= 0 ? _data.stages[Flow.StageIndex] : null;

        public IReadOnlyCollection<PartInteraction> Parts => _parts.Values;

        public IReadOnlyList<PartSocket> Sockets => _sockets;

        /// <summary>授業が終わった（lesson_end を書いた後）。</summary>
        public event Action Finished;

        /// <summary>要点の処理を差し替える（段階5・6で注視判定と強調を入れる）。</summary>
        public void SetKeyPointHandler(IKeyPointHandler handler)
        {
            _keyPointHandler = handler ?? new NoOpKeyPointHandler();
        }

        /// <summary>1. 授業開始：授業データを読み込み、段階1の説明音声から始める。</summary>
        public void StartLesson(LessonData data, string startDetail = null)
        {
            if (Flow != null && Flow.State != LessonState.Finished)
            {
                throw new InvalidOperationException("授業が既に進行中です。");
            }

            _data = data != null ? data : throw new ArgumentNullException(nameof(data));
            Flow = new LessonFlow(data.ToFlowStages());
            CollectSceneObjects();
            ValidateData();

            if (narrationSource != null)
            {
                narrationSource.spatialBlend = 0f; // SPEC 4.5：説明音声は 2D
                narrationSource.playOnAwake = false;
            }

            if (messageView != null)
            {
                messageView.Hide();
            }

            foreach (PartInteraction p in _parts.Values)
            {
                p.RememberHome();
            }

            sessionLog.CurrentStage = null;
            sessionLog.Log(LogEvent.LessonStart, null, startDetail);
            Flow.Start();
            EnterNarration();
        }

        private void CollectSceneObjects()
        {
            Unsubscribe();
            _parts.Clear();
            _sockets.Clear();

            foreach (PartInteraction p in FindObjectsByType<PartInteraction>(FindObjectsSortMode.None))
            {
                if (string.IsNullOrEmpty(p.PartId))
                {
                    Debug.LogError($"partId が空の部品があります：{p.name}", p);
                    continue;
                }

                if (_parts.ContainsKey(p.PartId))
                {
                    Debug.LogError($"partId が重複しています：{p.PartId}", p);
                    continue;
                }

                _parts.Add(p.PartId, p);
                p.Grabbed += OnPartGrabbed;
            }

            foreach (PartSocket s in FindObjectsByType<PartSocket>(FindObjectsSortMode.None))
            {
                _sockets.Add(s);
                s.Attached += OnSocketAttached;
                s.Detached += OnSocketDetached;
            }
        }

        private void ValidateData()
        {
            for (int i = 0; i < _data.stages.Count; i++)
            {
                StageData s = _data.stages[i];
                bool needsTarget = s.completion != CompletionType.None || s.isKeyPoint;
                if (needsTarget && !_parts.ContainsKey(s.targetPartId ?? string.Empty))
                {
                    Debug.LogError($"段階{i + 1}「{s.stageName}」の対象部品「{s.targetPartId}」がシーンにありません。", this);
                }
            }
        }

        // ---- 2. 説明音声 ----
        private void EnterNarration()
        {
            sessionLog.CurrentStage = Flow.StageNumber;
            SetAllGrabbable(false);
            Run(NarrationRoutine(CurrentStage));
        }

        private IEnumerator NarrationRoutine(StageData stage)
        {
            AudioClip clip = stage.narrationClip;
            float length = clip != null ? clip.length : config.missingAudioPlaceholderSec;
            sessionLog.Log(LogEvent.AudioStart, stage.targetPartId, clip != null ? "clip=" + clip.name : "placeholder");

            if (clip != null && narrationSource != null)
            {
                narrationSource.clip = clip;
                narrationSource.Play();
            }

            float elapsed = 0f;
            while (elapsed < length)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            sessionLog.Log(LogEvent.AudioEnd, stage.targetPartId);
            _running = null;
            Flow.NarrationFinished();
            EnterAccepting();
        }

        // ---- 3〜4. 操作受付・完了判定 ----
        private void EnterAccepting()
        {
            SetAllGrabbable(true);
            StageData stage = CurrentStage;

            if (stage.completion == CompletionType.None)
            {
                CompleteOperation("completion=none");
                return;
            }

            // 前の段階の間に、先に済ませていた場合（例：既に外してある）
            if (IsAlreadySatisfied(stage))
            {
                CompleteOperation("already");
            }
        }

        private bool IsAlreadySatisfied(StageData stage)
        {
            if (!_parts.TryGetValue(stage.targetPartId ?? string.Empty, out PartInteraction part))
            {
                return false;
            }

            switch (stage.completion)
            {
                case CompletionType.Detach:
                    return part.CurrentSocket == null && !part.IsHeldByHand;
                case CompletionType.Attach:
                    return part.CurrentSocket != null && part.CurrentSocket.IsFilledCorrectly;
                default:
                    return false; // 触れる：受付が始まってから掴む必要がある
            }
        }

        private void OnPartGrabbed(PartInteraction part)
        {
            if (IsAcceptingFor(part, CompletionType.Touch))
            {
                CompleteOperation(null);
            }
        }

        private void OnSocketAttached(PartSocket socket, PartInteraction part)
        {
            if (IsAcceptingFor(part, CompletionType.Attach) && socket.IsFilledCorrectly)
            {
                CompleteOperation("socket=" + socket.SocketId);
                return;
            }

            if (Flow != null && Flow.State == LessonState.AssemblyCheck)
            {
                TryConfirmAssembly();
            }
        }

        private void OnSocketDetached(PartSocket socket, PartInteraction part)
        {
            if (IsAcceptingFor(part, CompletionType.Detach))
            {
                CompleteOperation("socket=" + socket.SocketId);
            }
        }

        private bool IsAcceptingFor(PartInteraction part, CompletionType completion)
        {
            return Flow != null
                   && Flow.State == LessonState.Accepting
                   && CurrentStage.completion == completion
                   && part.PartId == CurrentStage.targetPartId;
        }

        /// <summary>
        /// 操作完了（op_complete）。実験者のスキップ（段階4）からも呼ぶ。
        /// </summary>
        public void CompleteOperation(string detail)
        {
            if (Flow == null || Flow.State != LessonState.Accepting)
            {
                return;
            }

            sessionLog.Log(LogEvent.OpComplete, CurrentStage.targetPartId, detail);
            Flow.OperationCompleted();
            if (Flow.State == LessonState.KeyPoint)
            {
                EnterKeyPoint();
            }
            else
            {
                AfterStage();
            }
        }

        // ---- 5〜7. 要点 ----
        private void EnterKeyPoint()
        {
            StageData stage = CurrentStage;
            _parts.TryGetValue(stage.targetPartId ?? string.Empty, out PartInteraction target);
            var context = new KeyPointContext(stage, Flow.StageNumber ?? 0, target);
            Run(KeyPointRoutine(context));
        }

        private IEnumerator KeyPointRoutine(KeyPointContext context)
        {
            yield return _keyPointHandler.Run(context);
            _running = null;
            Flow.KeyPointFinished();
            AfterStage();
        }

        // ---- 8. 最後のパートか？ ----
        private void AfterStage()
        {
            switch (Flow.State)
            {
                case LessonState.Narration:
                    EnterNarration();
                    break;
                case LessonState.AssemblyCheck:
                    EnterAssemblyCheck();
                    break;
            }
        }

        // ---- 9. 組み立て確認 ----
        private void EnterAssemblyCheck()
        {
            sessionLog.CurrentStage = null;
            SetAllGrabbable(true);
            TryConfirmAssembly();
        }

        private void TryConfirmAssembly()
        {
            if (config.requireAllSocketsFilled && !AllSocketsFilled())
            {
                return; // 全部埋まるまで受付のまま待つ
            }

            Flow.AssemblyConfirmed();
            SetAllGrabbable(false);
            sessionLog.Log(LogEvent.LessonEnd, null, "assembled=" + (AllSocketsFilled() ? "true" : "false"));
            if (messageView != null)
            {
                messageView.Show(RemoveHmdMessage);
            }

            Finished?.Invoke();
        }

        public bool AllSocketsFilled()
        {
            foreach (PartSocket s in _sockets)
            {
                if (!s.IsFilledCorrectly)
                {
                    return false;
                }
            }

            return true;
        }

        // ---- 共通 ----
        private void SetAllGrabbable(bool grabbable)
        {
            foreach (PartInteraction p in _parts.Values)
            {
                p.SetGrabbable(grabbable);
            }
        }

        private void Run(IEnumerator routine)
        {
            if (_running != null)
            {
                StopCoroutine(_running);
            }

            _running = StartCoroutine(routine);
        }

        private void Unsubscribe()
        {
            foreach (PartInteraction p in _parts.Values)
            {
                if (p != null) p.Grabbed -= OnPartGrabbed;
            }

            foreach (PartSocket s in _sockets)
            {
                if (s == null) continue;
                s.Attached -= OnSocketAttached;
                s.Detached -= OnSocketDetached;
            }
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }
    }
}
