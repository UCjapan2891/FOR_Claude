using System;

namespace VRClass.Core
{
    /// <summary>
    /// 一定の頻度で処理するためのタイマー（head.csv の 10 Hz など）。
    /// 毎フレーム Time.deltaTime を渡し、true が返ったフレームで1回処理する。
    /// フレームが大きく遅れても、まとめて何回も true を返さない（同じ姿勢を重複して書かないため）。
    /// </summary>
    public sealed class IntervalTimer
    {
        private double _accumulated;

        public IntervalTimer(double frequencyHz)
        {
            SetFrequency(frequencyHz);
        }

        public double IntervalSeconds { get; private set; }

        public void SetFrequency(double frequencyHz)
        {
            if (frequencyHz <= 0 || double.IsNaN(frequencyHz) || double.IsInfinity(frequencyHz))
            {
                throw new ArgumentOutOfRangeException(nameof(frequencyHz), frequencyHz, "頻度は正の値です。");
            }

            IntervalSeconds = 1.0 / frequencyHz;
        }

        public void Reset() => _accumulated = 0;

        /// <summary>deltaSeconds だけ進め、間隔を超えたら true。</summary>
        public bool Tick(double deltaSeconds)
        {
            if (deltaSeconds < 0 || double.IsNaN(deltaSeconds) || double.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "経過時間は0以上の有限の値です。");
            }

            _accumulated += deltaSeconds;
            if (_accumulated < IntervalSeconds)
            {
                return false;
            }

            _accumulated %= IntervalSeconds;
            return true;
        }
    }
}
