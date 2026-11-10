using System;
using System.Collections.Generic;

namespace VRClass.Core
{
    /// <summary>空間的強調の段階（SPEC 4.3）。</summary>
    public enum EmphasisPhase
    {
        None,
        Pull,     // ① 引き寄せ
        Scale,    // ② 拡大
        Rotate,   // ③ 回転
        Observe,  // （観察）
        Return,   // ④ 復帰
        Done,
    }

    /// <summary>要点で行う演出（SPEC 3章 7、4.1、4.3）。</summary>
    public enum EmphasisMode
    {
        /// <summary>テロップのみを Ttotal の間表示する（条件1、または注視に失敗したとき）。</summary>
        TelopOnly,

        /// <summary>テロップ＋空間的強調（条件2で注視できたとき）。</summary>
        TelopAndSpatial,
    }

    /// <summary>
    /// 強調の各段階の長さ（秒）と合計 Ttotal（SPEC 4.3）。
    /// 条件1の停止時間と条件2の動きは、どちらもこの値から作る（合計時間をそろえるため）。
    /// </summary>
    public readonly struct EmphasisTiming
    {
        public readonly float PullSec;
        public readonly float ScaleSec;
        public readonly float RotateSec;

        /// <summary>観察フェーズの長さ。使わないときは 0。</summary>
        public readonly float ObserveSec;

        public readonly float ReturnSec;
        public readonly bool UseObservePhase;

        public EmphasisTiming(float pullSec, float scaleSec, float rotationSpeedDegPerSec,
            bool useObservePhase, float observeSec, float returnSec)
        {
            PullSec = Math.Max(0f, pullSec);
            ScaleSec = Math.Max(0f, scaleSec);
            RotateSec = EmphasisMath.RotationDurationSec(rotationSpeedDegPerSec);
            UseObservePhase = useObservePhase;
            ObserveSec = useObservePhase ? Math.Max(0f, observeSec) : 0f;
            ReturnSec = Math.Max(0f, returnSec);
        }

        /// <summary>Ttotal = T1 + T2 + 回転時間 + (観察時間) + T4。</summary>
        public float TotalSec => PullSec + ScaleSec + RotateSec + ObserveSec + ReturnSec;
    }

    /// <summary>
    /// 強調の段階の並びと、経過時間から今の段階を求める（SPEC 4.3「4段階を1つずつ順に実行」）。
    /// 段階ごとに時間を数え直さず、強調の開始からの経過時間1本で決めるので、
    /// 条件2の合計時間が条件1の停止時間（Ttotal）とフレームの端数でずれない。
    /// </summary>
    public sealed class EmphasisTimeline
    {
        private readonly List<EmphasisPhase> _phases = new List<EmphasisPhase>();
        private readonly List<float> _durations = new List<float>();
        private readonly List<float> _starts = new List<float>();

        /// <param name="includePull">false なら① 引き寄せを入れない（発表用デモのみ）</param>
        public EmphasisTimeline(EmphasisTiming timing, bool includePull = true)
        {
            if (includePull)
            {
                Add(EmphasisPhase.Pull, timing.PullSec);
            }

            Add(EmphasisPhase.Scale, timing.ScaleSec);
            Add(EmphasisPhase.Rotate, timing.RotateSec);
            if (timing.UseObservePhase)
            {
                Add(EmphasisPhase.Observe, timing.ObserveSec);
            }

            Add(EmphasisPhase.Return, timing.ReturnSec);
        }

        public int Count => _phases.Count;

        /// <summary>全段階の合計（秒）。</summary>
        public float TotalSec { get; private set; }

        public EmphasisPhase PhaseAt(int index) => _phases[index];

        public float DurationAt(int index) => _durations[index];

        public float StartAt(int index) => _starts[index];

        /// <summary>
        /// 経過時間 elapsed の段階番号（0 始まり）と、その段階の進み具合（0〜1）を返す。
        /// 全部終わっていれば Count と 1 を返す。長さ 0 の段階は飛ばす（呼び出し側が最後の状態を当てる）。
        /// </summary>
        public int Locate(float elapsedSec, out float progress)
        {
            if (float.IsNaN(elapsedSec) || elapsedSec < 0f)
            {
                elapsedSec = 0f;
            }

            for (int i = 0; i < _phases.Count; i++)
            {
                float end = _starts[i] + _durations[i];
                if (elapsedSec < end)
                {
                    progress = Easing.Progress(elapsedSec - _starts[i], _durations[i]);
                    return i;
                }
            }

            progress = 1f;
            return _phases.Count;
        }

        private void Add(EmphasisPhase phase, float duration)
        {
            _phases.Add(phase);
            _durations.Add(duration);
            _starts.Add(TotalSec);
            TotalSec += duration;
        }
    }

    /// <summary>
    /// 強調の時計。開始からの経過秒を Time.deltaTime の積算で数え、TotalSec に達したら終わり。
    /// 条件1（テロップのみで待つ）と条件2（空間的強調）が同じ規則で終わるよう、両方でこれを使う。
    /// </summary>
    public sealed class EmphasisClock
    {
        public EmphasisClock(float totalSec)
        {
            TotalSec = float.IsNaN(totalSec) ? 0f : Math.Max(0f, totalSec);
        }

        public float TotalSec { get; }

        public float Elapsed { get; private set; }

        public bool IsFinished => Elapsed >= TotalSec;

        public void Tick(float deltaSec)
        {
            if (deltaSec > 0f && !float.IsNaN(deltaSec) && !float.IsInfinity(deltaSec))
            {
                Elapsed += deltaSec;
            }
        }
    }

    /// <summary>
    /// 要点で何をするかを決める（SPEC 3章 7、4.1）。条件による違いはここと EmphasisController だけで扱う。
    /// </summary>
    public static class EmphasisPlan
    {
        /// <summary>
        /// 条件2で注視できたときだけ空間的強調。条件1、または注視に失敗したとき（gaze_fail）はテロップのみ。
        /// </summary>
        public static EmphasisMode ChooseMode(Condition condition, bool gazeOk)
        {
            return condition == Condition.TelopWithEmphasis && gazeOk
                ? EmphasisMode.TelopAndSpatial
                : EmphasisMode.TelopOnly;
        }

        /// <summary>
        /// 演出の長さ（秒）。どの場合も Ttotal（条件1・2、注視の成否で同じ）。
        /// </summary>
        public static float DurationSec(EmphasisMode mode, EmphasisTiming timing)
        {
            return mode == EmphasisMode.TelopAndSpatial
                ? new EmphasisTimeline(timing).TotalSec
                : timing.TotalSec;
        }

        /// <summary>ログ（emphasis_start の mode）に書く名前。</summary>
        public static string ModeName(EmphasisMode mode)
        {
            return mode == EmphasisMode.TelopAndSpatial ? "spatial" : "telop";
        }

        /// <summary>ログ（emphasis_phase の phase）に書く名前。</summary>
        public static string PhaseName(EmphasisPhase phase)
        {
            switch (phase)
            {
                case EmphasisPhase.Pull: return "pull";
                case EmphasisPhase.Scale: return "scale";
                case EmphasisPhase.Rotate: return "rotate";
                case EmphasisPhase.Observe: return "observe";
                case EmphasisPhase.Return: return "return";
                case EmphasisPhase.Done: return "done";
                default: return "none";
            }
        }
    }
}
