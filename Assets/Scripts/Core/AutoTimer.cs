using System;

namespace VRClass.Core
{
    /// <summary>自動タイマーが起こす動作。</summary>
    public enum AutoTimerAction
    {
        None,

        /// <summary>ヒントを出す（[60] 秒）</summary>
        Hint,

        /// <summary>自動で完了させる（[120] 秒）</summary>
        Complete,
    }

    /// <summary>
    /// 操作受付の自動タイマー（SPEC 5.3：useAutoTimers が true のときだけ）。
    /// 受付が始まったら <see cref="Restart"/>、毎フレーム Time.deltaTime を <see cref="Tick"/> に渡す。
    /// 一時停止中は Tick を呼ばない（受付の時間だけを数える）。
    /// ヒントと完了はそれぞれ1回だけ返す。1回の Tick で両方を越えたら完了を返す。
    /// </summary>
    public sealed class AutoTimer
    {
        private readonly bool _enabled;
        private readonly double _hintAfter;
        private readonly double _completeAfter;

        public AutoTimer(bool enabled, double hintAfterSec, double completeAfterSec)
        {
            if (hintAfterSec < 0 || completeAfterSec < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(hintAfterSec), "秒数は0以上です。");
            }

            _enabled = enabled;
            _hintAfter = hintAfterSec;
            _completeAfter = completeAfterSec;
        }

        public double Elapsed { get; private set; }

        public bool HintFired { get; private set; }

        public bool CompleteFired { get; private set; }

        public void Restart()
        {
            Elapsed = 0;
            HintFired = false;
            CompleteFired = false;
        }

        public AutoTimerAction Tick(double deltaSeconds)
        {
            if (deltaSeconds < 0 || double.IsNaN(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "経過時間は0以上です。");
            }

            if (!_enabled || CompleteFired)
            {
                return AutoTimerAction.None;
            }

            Elapsed += deltaSeconds;
            if (Elapsed >= _completeAfter)
            {
                CompleteFired = true;
                HintFired = true;
                return AutoTimerAction.Complete;
            }

            if (!HintFired && Elapsed >= _hintAfter)
            {
                HintFired = true;
                return AutoTimerAction.Hint;
            }

            return AutoTimerAction.None;
        }
    }
}
