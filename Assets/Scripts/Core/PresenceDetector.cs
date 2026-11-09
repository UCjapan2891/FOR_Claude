using System;

namespace VRClass.Core
{
    /// <summary>受講者が授業を受けられない状態。</summary>
    public enum PresenceIssue
    {
        None,

        /// <summary>HMD を外した（装着センサー）</summary>
        HmdRemoved,

        /// <summary>アプリが前面でなくなった（Quest のメニュー、Link の切断など）</summary>
        FocusLost,

        /// <summary>頭のトラッキングを失った</summary>
        TrackingLost,
    }

    /// <summary>
    /// HMD を外した・トラッキングを失ったことの判定（SPEC 5.3：自動で一時停止）。
    /// 状態が猶予時間（graceSec）続いたら、その時に1回だけ問題を返す。元に戻ったら、また検出できるようになる。
    /// 値が取れないもの（null）は判定に使わない（HMD がないエディタなど）。
    /// 優先順：HMD を外した → 前面でない → トラッキングを失った。
    /// </summary>
    public sealed class PresenceDetector
    {
        private readonly double _graceSec;
        private double _badFor;
        private bool _reported;

        public PresenceDetector(double graceSec)
        {
            if (graceSec < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(graceSec), graceSec, "猶予時間は0以上です。");
            }

            _graceSec = graceSec;
        }

        /// <param name="userPresent">装着センサー（取れなければ null）</param>
        /// <param name="tracked">頭のトラッキング（取れなければ null）</param>
        /// <param name="focused">アプリが前面か（判定に使わないときは null）</param>
        /// <returns>猶予時間を越えた瞬間だけ問題を返す。それ以外は None</returns>
        public PresenceIssue Update(double deltaSeconds, bool? userPresent, bool? tracked, bool? focused)
        {
            PresenceIssue current = Classify(userPresent, tracked, focused);
            if (current == PresenceIssue.None)
            {
                _badFor = 0;
                _reported = false;
                return PresenceIssue.None;
            }

            _badFor += Math.Max(0, deltaSeconds);
            if (_reported || _badFor < _graceSec)
            {
                return PresenceIssue.None;
            }

            _reported = true;
            return current;
        }

        public static PresenceIssue Classify(bool? userPresent, bool? tracked, bool? focused)
        {
            if (userPresent == false) return PresenceIssue.HmdRemoved;
            if (focused == false) return PresenceIssue.FocusLost;
            if (tracked == false) return PresenceIssue.TrackingLost;
            return PresenceIssue.None;
        }

        public static string LogReason(PresenceIssue issue)
        {
            switch (issue)
            {
                case PresenceIssue.HmdRemoved: return "hmd_removed";
                case PresenceIssue.FocusLost: return "focus_lost";
                case PresenceIssue.TrackingLost: return "tracking_lost";
                default: return "none";
            }
        }
    }
}
