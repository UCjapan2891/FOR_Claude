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
    /// ここでは音声・部品の掴み・ログ・学習者パネル・実験者の操作（SPEC 5.2、5.3）を動かす。
    /// 条件による分岐は書かない（CLAUDE.md 3章）。
    /// </summary>
    public sealed class LessonController : MonoBehaviour
    {
        public const string RemoveHmdMessage = "授業は終わりです。HMD を外してください";
        public const string PausedMessage = "一時停止中です";
        public const string AbortedMessage = "授業を中断しました";

        [SerializeField] private ExperimentConfig config;
        [SerializeField] private SessionLog sessionLog;

        [Tooltip("説明音声を流す AudioSource（2D にする）")]
        [SerializeField] private AudioSource narrationSource;

        [SerializeField] private LessonMessageView messageView;
        [SerializeField] private LearnerPanel learnerPanel;
        [SerializeField] private HintPresenter hintPresenter;

        [Tooltip("要点の処理（IKeyPointHandler を実装したコンポーネント。GazeKeyPointHandler など）。空なら何もしない")]
        [SerializeField] private MonoBehaviour keyPointHandler;

        private readonly Dictionary<string, PartInteraction> _parts = new Dictionary<string, PartInteraction>();
        private readonly List<PartSocket> _sockets = new List<PartSocket>();
        private IKeyPointHandler _keyPointHandler = new NoOpKeyPointHandler();
        private LessonData _data;
        private Coroutine _running;
        private AutoTimer _autoTimer;
        private int _hintCount;
        private string _pendingCompleteDetail;

        public LessonFlow Flow { get; private set; }

        public LessonData Data => _data;

        /// <summary>現在の段階のデータ。開始前は null。</summary>
        public StageData CurrentStage => Flow != null && Flow.StageIndex >= 0 ? _data.stages[Flow.StageIndex] : null;

        public IReadOnlyCollection<PartInteraction> Parts => _parts.Values;

        public IReadOnlyList<PartSocket> Sockets => _sockets;

        /// <summary>一時停止中か。</summary>
        public bool IsPaused { get; private set; }

        /// <summary>中断したか（その授業は分析から除外する）。</summary>
        public bool IsAborted { get; private set; }

        /// <summary>授業が進行中か（開始後、終了・中断の前）。</summary>
        public bool IsRunning => Flow != null && Flow.State != LessonState.NotStarted && Flow.State != LessonState.Finished && !IsAborted;

        /// <summary>今の段階でヒントを押した回数。</summary>
        public int HintCount => _hintCount;

        /// <summary>受付の経過秒（自動タイマー用）。</summary>
        public double AcceptingSeconds => _autoTimer != null ? _autoTimer.Elapsed : 0;

        /// <summary>授業が終わった（lesson_end を書いた後）。</summary>
        public event Action Finished;

        /// <summary>授業を中断した（abort を書いた後）。</summary>
        public event Action Aborted;

        /// <summary>要点の処理を差し替える（段階5・6で注視判定と強調を入れる）。</summary>
        public void SetKeyPointHandler(IKeyPointHandler handler)
        {
            _keyPointHandler = handler ?? new NoOpKeyPointHandler();
        }

        // =====================================================================
        // 1. 授業開始
        // =====================================================================

        /// <summary>授業データを読み込み、段階1の説明音声から始める。</summary>
        public void StartLesson(LessonData data, string startDetail = null)
        {
            if (IsRunning)
            {
                throw new InvalidOperationException("授業が既に進行中です。");
            }

            _data = data != null ? data : throw new ArgumentNullException(nameof(data));
            if (keyPointHandler is IKeyPointHandler handler)
            {
                _keyPointHandler = handler;
            }
            else if (keyPointHandler != null)
            {
                Debug.LogError($"{keyPointHandler.GetType().Name} は IKeyPointHandler ではありません。", this);
            }

            Flow = new LessonFlow(data.ToFlowStages());
            IsPaused = false;
            IsAborted = false;
            _autoTimer = new AutoTimer(config.useAutoTimers, config.autoHintAfterSec, config.autoCompleteAfterSec);
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

            if (learnerPanel != null)
            {
                learnerPanel.ResetRequested += OnResetRequested;
                learnerPanel.HintRequested += OnHintRequested;
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

        // =====================================================================
        // 2. 説明音声
        // =====================================================================

        private void EnterNarration()
        {
            sessionLog.CurrentStage = Flow.StageNumber;
            _hintCount = 0;
            HideHint();
            SetAllGrabbable(false);
            SetPanel(true);
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

        private void StopNarration()
        {
            if (narrationSource != null && narrationSource.isPlaying)
            {
                narrationSource.Stop();
            }
        }

        // =====================================================================
        // 3〜4. 操作受付・完了判定
        // =====================================================================

        private void EnterAccepting()
        {
            SetAllGrabbable(true);
            _autoTimer.Restart();
            StageData stage = CurrentStage;

            if (stage.completion == CompletionType.None)
            {
                CompleteOperation("completion=none");
                return;
            }

            // 前の段階の間に、先に済ませていた場合（例：既に付けてある）
            if (IsAlreadySatisfied(stage))
            {
                CompleteOperation("already");
            }
        }

        private bool IsAlreadySatisfied(StageData stage)
        {
            if (!TryGetTarget(stage, out PartInteraction part))
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

            if (Flow != null && Flow.State == LessonState.AssemblyCheck && !IsPaused)
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
                   && !IsPaused
                   && !IsAborted
                   && Flow.State == LessonState.Accepting
                   && CurrentStage.completion == completion
                   && part.PartId == CurrentStage.targetPartId;
        }

        /// <summary>操作完了（op_complete）。</summary>
        public void CompleteOperation(string detail)
        {
            if (Flow == null || Flow.State != LessonState.Accepting || IsPaused || IsAborted)
            {
                return;
            }

            if (_pendingCompleteDetail != null)
            {
                detail = _pendingCompleteDetail;
                _pendingCompleteDetail = null;
            }

            HideHint();
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

        // =====================================================================
        // 5〜7. 要点
        // =====================================================================

        private void EnterKeyPoint()
        {
            // 要点の間（注視判定・強調）は全部の部品を掴めなくし、手に持っている部品も離させる（2026-11-10 決定）
            SetAllGrabbable(false, releaseHeld: true);
            SetPanel(false);
            StageData stage = CurrentStage;
            TryGetTarget(stage, out PartInteraction target);
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

        // =====================================================================
        // 8. 最後のパートか？
        // =====================================================================

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

        // =====================================================================
        // 9. 組み立て確認
        // =====================================================================

        private void EnterAssemblyCheck()
        {
            sessionLog.CurrentStage = null;
            SetAllGrabbable(true);
            SetPanel(false);
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
            SetPanel(false);
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

        // =====================================================================
        // 5.2 学習者パネル
        // =====================================================================

        private void OnResetRequested(string source)
        {
            if (!IsRunning || IsPaused || CurrentStage == null || Flow.State == LessonState.KeyPoint)
            {
                return;
            }

            if (!TryGetTarget(CurrentStage, out PartInteraction part) || part.IsLocked || part.CurrentSocket != null)
            {
                return; // 対象部品がない・付いている部品は動かさない
            }

            part.ReturnHome(source == "key" ? "key" : "button");
        }

        private void OnHintRequested(string source)
        {
            ShowHint(source);
        }

        /// <summary>ヒント：正しい場所を光らせ、hint を回数つきで記録する。受付中だけ。</summary>
        public void ShowHint(string reason)
        {
            if (Flow == null || Flow.State != LessonState.Accepting || IsPaused || IsAborted)
            {
                return;
            }

            StageData stage = CurrentStage;
            if (!TryGetTarget(stage, out PartInteraction part))
            {
                return;
            }

            _hintCount++;
            sessionLog.Log(LogEvent.Hint, stage.targetPartId, $"count={_hintCount};reason={reason}");

            if (hintPresenter == null)
            {
                return;
            }

            Transform at = null;
            if (stage.completion == CompletionType.Attach)
            {
                PartSocket socket = FindSocketFor(part);
                at = socket != null ? (socket.Socket.attachTransform != null ? socket.Socket.attachTransform : socket.transform) : null;
            }

            hintPresenter.Show(part, at); // 「触れる」「外す」は部品そのものを光らせる
        }

        private void HideHint()
        {
            if (hintPresenter != null)
            {
                hintPresenter.Hide();
            }
        }

        // =====================================================================
        // 5.3 実験者の操作
        // =====================================================================

        /// <summary>
        /// スキップ（S、または自動タイマー）：そのパートを自動で完了する。skip を記録する。
        /// 説明音声の途中なら音声を止める。「付ける」は部品を正しい場所に自動で置き、「外す」はソケットから外して机に戻す。
        /// 組み立て確認中なら、足りない部品を全部置いて終える。
        /// </summary>
        public void Skip(string reason)
        {
            if (!IsRunning || IsPaused)
            {
                return;
            }

            if (Flow.State == LessonState.AssemblyCheck)
            {
                sessionLog.Log(LogEvent.Skip, null, "reason=" + reason + ";assembly");
                foreach (PartSocket s in _sockets)
                {
                    if (!s.IsFilledCorrectly && _parts.TryGetValue(s.AcceptedPartId, out PartInteraction p))
                    {
                        s.ForcePlace(p);
                    }
                }

                TryConfirmAssembly();
                return;
            }

            if (Flow.State == LessonState.Narration)
            {
                StopRunning();
                StopNarration();
                sessionLog.Log(LogEvent.AudioEnd, CurrentStage.targetPartId, "skipped");
                Flow.NarrationFinished();
                SetAllGrabbable(true);
                _autoTimer.Restart();
            }

            if (Flow.State != LessonState.Accepting)
            {
                return; // 要点の最中は飛ばさない
            }

            StageData stage = CurrentStage;
            sessionLog.Log(LogEvent.Skip, stage.targetPartId, "reason=" + reason);
            _pendingCompleteDetail = "skip;reason=" + reason;

            if (TryGetTarget(stage, out PartInteraction part))
            {
                if (stage.completion == CompletionType.Attach)
                {
                    PartSocket socket = FindSocketFor(part);
                    if (socket != null)
                    {
                        socket.ForcePlace(part); // 付いた時点で op_complete まで進む
                    }
                }
                else if (stage.completion == CompletionType.Detach && part.CurrentSocket != null)
                {
                    part.CurrentSocket.Release(part); // 外れた時点で op_complete まで進む
                }
            }

            CompleteOperation("skip;reason=" + reason); // まだ完了していなければここで完了
            _pendingCompleteDetail = null;
        }

        /// <summary>
        /// 一時停止（P、または HMD を外した・トラッキングを失った）。pause を記録する。
        /// 音声を止め、手に持っている部品を離させ、強調中なら部品を元に戻す。再開はパートの最初から。
        /// </summary>
        public void Pause(string reason)
        {
            if (!IsRunning || IsPaused)
            {
                return;
            }

            IsPaused = true;
            StopRunning();
            StopNarration();
            _keyPointHandler.Cancel();
            HideHint();
            SetAllGrabbable(false, releaseHeld: true);
            SetPanel(false);
            sessionLog.Clock?.Pause();
            sessionLog.Log(LogEvent.Pause, CurrentStage?.targetPartId, "reason=" + reason + ";state=" + Flow.State);
            if (messageView != null)
            {
                messageView.Show(PausedMessage);
            }
        }

        /// <summary>再開（P）。resume を記録し、そのパートを説明音声から始め直す（組み立て確認中ならそのまま続ける）。</summary>
        public void Resume()
        {
            if (!IsPaused || IsAborted)
            {
                return;
            }

            IsPaused = false;
            sessionLog.Clock?.Resume();
            sessionLog.Log(LogEvent.Resume, CurrentStage?.targetPartId, "state=" + Flow.State);
            if (messageView != null)
            {
                messageView.Hide();
            }

            if (Flow.State == LessonState.AssemblyCheck)
            {
                EnterAssemblyCheck();
                return;
            }

            Flow.RestartStage();
            EnterNarration();
        }

        /// <summary>中断（A を2回）。abort を記録して授業を止める（その授業は分析から除外。ログは保存）。</summary>
        public void Abort(string reason)
        {
            if (!IsRunning)
            {
                return;
            }

            StopRunning();
            StopNarration();
            _keyPointHandler.Cancel();
            HideHint();
            SetAllGrabbable(false, releaseHeld: true);
            SetPanel(false);
            sessionLog.Log(LogEvent.Abort, CurrentStage?.targetPartId, "reason=" + reason + ";state=" + Flow.State);
            IsAborted = true;
            IsPaused = false;
            if (messageView != null)
            {
                messageView.Show(AbortedMessage);
            }

            Aborted?.Invoke();
        }

        // =====================================================================
        // 自動タイマー（SPEC 5.3：useAutoTimers）
        // =====================================================================

        private void Update()
        {
            if (Flow == null || Flow.State != LessonState.Accepting || IsPaused || IsAborted)
            {
                return;
            }

            switch (_autoTimer.Tick(Time.deltaTime))
            {
                case AutoTimerAction.Hint:
                    ShowHint("auto");
                    break;
                case AutoTimerAction.Complete:
                    Skip("auto");
                    break;
            }
        }

        // =====================================================================
        // 共通
        // =====================================================================

        private bool TryGetTarget(StageData stage, out PartInteraction part)
        {
            part = null;
            return stage != null && _parts.TryGetValue(stage.targetPartId ?? string.Empty, out part);
        }

        private PartSocket FindSocketFor(PartInteraction part)
        {
            foreach (PartSocket s in _sockets)
            {
                if (s.AcceptedPartId == part.PartId)
                {
                    return s;
                }
            }

            return null;
        }

        private void SetAllGrabbable(bool grabbable, bool releaseHeld = false)
        {
            foreach (PartInteraction p in _parts.Values)
            {
                p.SetGrabbable(grabbable, releaseHeld);
            }
        }

        private void SetPanel(bool interactable)
        {
            if (learnerPanel != null)
            {
                learnerPanel.SetInteractable(interactable);
            }
        }

        private void Run(IEnumerator routine)
        {
            StopRunning();
            _running = StartCoroutine(routine);
        }

        private void StopRunning()
        {
            if (_running != null)
            {
                StopCoroutine(_running);
                _running = null;
            }
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

            if (learnerPanel != null)
            {
                learnerPanel.ResetRequested -= OnResetRequested;
                learnerPanel.HintRequested -= OnHintRequested;
            }
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }
    }
}
