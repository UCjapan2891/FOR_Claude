using System;

namespace VRClass.Core
{
    /// <summary>注視判定の結果（その Tick で起きたこと）。</summary>
    public enum GazeEvent
    {
        None,

        /// <summary>θ ≤ θth が t_hold 続いた（gaze_ok）</summary>
        Ok,

        /// <summary>t_timeout たっても見なかった。再配置する（gaze_timeout → relocate）</summary>
        Timeout,

        /// <summary>再配置の上限 N 回を超えた（gaze_timeout → gaze_fail）。強調せずテロップのみ</summary>
        Fail,
    }

    /// <summary>
    /// 注視判定（SPEC 4.1）の状態の移り変わり。毎フレーム θ と経過秒を <see cref="Tick"/> に渡す。
    /// - θ ≤ θth が t_hold 続いたら Ok（途中で θth を超えたら数え直し。θ が NaN なら見ていない扱い）
    /// - t_timeout たっても Ok にならなければ Timeout（呼び出し側が再配置する）。経過時間と保持時間は数え直し
    /// - 再配置を N 回したあとにまた t_timeout になったら Fail
    /// - 同じ Tick で保持と時間切れが重なったら Ok を優先する
    /// Ok か Fail を返したら終わり（以後は None）。
    /// </summary>
    public sealed class GazeJudgeLogic
    {
        private readonly float _thresholdDeg;
        private readonly double _holdSec;
        private readonly double _timeoutSec;
        private readonly int _maxRelocations;

        public GazeJudgeLogic(float thresholdDeg, double holdSec, double timeoutSec, int maxRelocations)
        {
            if (holdSec < 0 || timeoutSec < 0 || maxRelocations < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(holdSec), "秒数・回数は0以上です。");
            }

            _thresholdDeg = thresholdDeg;
            _holdSec = holdSec;
            _timeoutSec = timeoutSec;
            _maxRelocations = maxRelocations;
        }

        /// <summary>θ ≤ θth が続いている秒数。</summary>
        public double HoldElapsed { get; private set; }

        /// <summary>判定開始（または直前の再配置）からの秒数。</summary>
        public double WindowElapsed { get; private set; }

        /// <summary>再配置した回数。</summary>
        public int Relocations { get; private set; }

        /// <summary>判定開始からの合計秒数。</summary>
        public double TotalElapsed { get; private set; }

        public bool IsFinished { get; private set; }

        /// <summary>終わったときの結果（Ok か Fail）。終わる前は None。</summary>
        public GazeEvent Result { get; private set; }

        public GazeEvent Tick(double deltaSeconds, float thetaDeg)
        {
            if (deltaSeconds < 0 || double.IsNaN(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "経過時間は0以上です。");
            }

            if (IsFinished)
            {
                return GazeEvent.None;
            }

            WindowElapsed += deltaSeconds;
            TotalElapsed += deltaSeconds;
            if (GazeMath.IsWithinThreshold(thetaDeg, _thresholdDeg))
            {
                HoldElapsed += deltaSeconds;
            }
            else
            {
                HoldElapsed = 0;
            }

            if (HoldElapsed >= _holdSec)
            {
                return Finish(GazeEvent.Ok);
            }

            if (WindowElapsed < _timeoutSec)
            {
                return GazeEvent.None;
            }

            if (Relocations >= _maxRelocations)
            {
                return Finish(GazeEvent.Fail);
            }

            Relocations++;
            WindowElapsed = 0;
            HoldElapsed = 0;
            return GazeEvent.Timeout;
        }

        private GazeEvent Finish(GazeEvent result)
        {
            IsFinished = true;
            Result = result;
            return result;
        }
    }
}
