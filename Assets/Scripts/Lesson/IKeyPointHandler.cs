using System.Collections;
using VRClass.Interaction;

namespace VRClass.Lesson
{
    /// <summary>要点の処理に渡す情報。</summary>
    public sealed class KeyPointContext
    {
        public KeyPointContext(StageData stage, int stageNumber, PartInteraction target)
        {
            Stage = stage;
            StageNumber = stageNumber;
            Target = target;
        }

        public StageData Stage { get; }

        /// <summary>段階番号（1 始まり）。</summary>
        public int StageNumber { get; }

        /// <summary>対象部品。</summary>
        public PartInteraction Target { get; }
    }

    /// <summary>
    /// 要点の処理（SPEC 3章 6〜7：注視判定 → 再配置 → 強調）。コルーチンが終わったら次の段階へ進む。
    /// 段階5で GazeJudge、段階6で EmphasisController を入れる。条件による違いはこの先（EmphasisController）だけで扱う。
    /// </summary>
    public interface IKeyPointHandler
    {
        IEnumerator Run(KeyPointContext context);

        /// <summary>
        /// 一時停止・中断されたとき、すぐに部品を元の状態（位置・回転・大きさ・ソケット）へ戻す（SPEC 5.3）。
        /// Run のコルーチンは呼び出し側が止める。
        /// </summary>
        void Cancel();
    }

    /// <summary>仮の処理：何もしないで終わる（段階3用）。</summary>
    public sealed class NoOpKeyPointHandler : IKeyPointHandler
    {
        public IEnumerator Run(KeyPointContext context)
        {
            yield break;
        }

        public void Cancel()
        {
        }
    }
}
