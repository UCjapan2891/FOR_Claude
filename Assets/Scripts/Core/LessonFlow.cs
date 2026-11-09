using System;
using System.Collections.Generic;

namespace VRClass.Core
{
    /// <summary>授業の状態（SPEC 3章）。</summary>
    public enum LessonState
    {
        /// <summary>授業開始前</summary>
        NotStarted,

        /// <summary>2. 説明音声（部品を掴めない）</summary>
        Narration,

        /// <summary>3〜4. 操作受付・完了判定（部品を掴める）</summary>
        Accepting,

        /// <summary>5〜7. 要点（注視判定・強調）</summary>
        KeyPoint,

        /// <summary>9. 組み立て確認</summary>
        AssemblyCheck,

        /// <summary>授業終了</summary>
        Finished,
    }

    /// <summary>状態遷移に必要な段階の情報。</summary>
    public readonly struct FlowStage
    {
        public readonly CompletionType Completion;
        public readonly bool IsKeyPoint;

        public FlowStage(CompletionType completion, bool isKeyPoint)
        {
            Completion = completion;
            IsKeyPoint = isKeyPoint;
        }
    }

    /// <summary>
    /// 授業の状態遷移（SPEC 3章）だけを持つ純粋なクラス。音声・部品・ログは扱わない。
    ///
    /// 1. 授業開始 → 2. 説明音声(i) → 3. 操作受付 → 4. 完了判定
    /// → 5. 要点か？（要点なら 6〜7：注視判定・強調）→ 8. 最後か？（まだなら i+1 で 2 へ）
    /// → 9. 組み立て確認 → 終了
    /// 決められた順序以外の呼び出しは InvalidOperationException。
    /// </summary>
    public sealed class LessonFlow
    {
        private readonly FlowStage[] _stages;

        public LessonFlow(IReadOnlyList<FlowStage> stages)
        {
            if (stages == null || stages.Count == 0)
            {
                throw new ArgumentException("段階が1つもありません。", nameof(stages));
            }

            _stages = new FlowStage[stages.Count];
            for (int i = 0; i < stages.Count; i++)
            {
                _stages[i] = stages[i];
            }

            State = LessonState.NotStarted;
            StageIndex = -1;
        }

        public LessonState State { get; private set; }

        /// <summary>現在の段階（0 始まり）。開始前は -1。終了後も最後の段階のまま。</summary>
        public int StageIndex { get; private set; }

        /// <summary>ログに書く段階番号（1 始まり）。開始前は null。</summary>
        public int? StageNumber => StageIndex >= 0 ? StageIndex + 1 : (int?)null;

        public int StageCount => _stages.Length;

        public FlowStage CurrentStage
        {
            get
            {
                if (StageIndex < 0)
                {
                    throw new InvalidOperationException("授業がまだ始まっていません。");
                }

                return _stages[StageIndex];
            }
        }

        public bool IsLastStage => StageIndex == _stages.Length - 1;

        /// <summary>1. 授業開始 → 段階1の説明音声へ。</summary>
        public void Start()
        {
            Require(LessonState.NotStarted, nameof(Start));
            StageIndex = 0;
            State = LessonState.Narration;
        }

        /// <summary>説明音声が終わった → 操作受付へ。</summary>
        public void NarrationFinished()
        {
            Require(LessonState.Narration, nameof(NarrationFinished));
            State = LessonState.Accepting;
        }

        /// <summary>
        /// 操作が完了した（完了条件を満たした・スキップ・「なし」）。
        /// 要点なら KeyPoint へ、そうでなければ次の段階（最後なら組み立て確認）へ。
        /// </summary>
        public void OperationCompleted()
        {
            Require(LessonState.Accepting, nameof(OperationCompleted));
            if (CurrentStage.IsKeyPoint)
            {
                State = LessonState.KeyPoint;
            }
            else
            {
                Advance();
            }
        }

        /// <summary>要点の処理（注視判定・強調）が終わった → 次の段階（最後なら組み立て確認）へ。</summary>
        public void KeyPointFinished()
        {
            Require(LessonState.KeyPoint, nameof(KeyPointFinished));
            Advance();
        }

        /// <summary>組み立て確認ができた → 授業終了。</summary>
        public void AssemblyConfirmed()
        {
            Require(LessonState.AssemblyCheck, nameof(AssemblyConfirmed));
            State = LessonState.Finished;
        }

        /// <summary>
        /// 今の段階を最初（説明音声）からやり直す（一時停止からの再開、SPEC 5.3）。
        /// 説明音声・操作受付・要点の間だけ呼べる。
        /// </summary>
        public void RestartStage()
        {
            if (State != LessonState.Narration && State != LessonState.Accepting && State != LessonState.KeyPoint)
            {
                throw new InvalidOperationException($"{State} の状態では段階をやり直せません。");
            }

            State = LessonState.Narration;
        }

        private void Advance()
        {
            if (IsLastStage)
            {
                State = LessonState.AssemblyCheck;
                return;
            }

            StageIndex++;
            State = LessonState.Narration;
        }

        private void Require(LessonState expected, string action)
        {
            if (State != expected)
            {
                throw new InvalidOperationException($"{action} は {expected} の状態でのみ呼べます（現在 {State}）。");
            }
        }
    }
}
