using System;

namespace VRClass.Core
{
    /// <summary>
    /// 授業開始からの経過秒（events.csv / head.csv の time 列）。
    /// 毎フレーム Time.deltaTime を <see cref="Tick"/> に渡して積算する（フレーム数では数えない）。
    ///
    /// excludePause = false（初期値）：一時停止中も時間を進める
    /// excludePause = true          ：一時停止中は時間を止める
    /// （ExperimentConfig.timeExcludesPause、SPEC 6章）
    /// </summary>
    public sealed class LessonClock
    {
        public LessonClock(bool excludePause)
        {
            ExcludePause = excludePause;
        }

        public bool ExcludePause { get; }

        /// <summary>授業開始からの秒。</summary>
        public double Elapsed { get; private set; }

        public bool IsPaused { get; private set; }

        /// <summary>経過時間を 0 に戻し、一時停止を解く（授業開始時）。</summary>
        public void Reset()
        {
            Elapsed = 0;
            IsPaused = false;
        }

        public void Pause() => IsPaused = true;

        public void Resume() => IsPaused = false;

        /// <summary>deltaSeconds だけ進める。</summary>
        public void Tick(double deltaSeconds)
        {
            if (deltaSeconds < 0 || double.IsNaN(deltaSeconds) || double.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "経過時間は0以上の有限の値です。");
            }

            if (IsPaused && ExcludePause)
            {
                return;
            }

            Elapsed += deltaSeconds;
        }
    }
}
