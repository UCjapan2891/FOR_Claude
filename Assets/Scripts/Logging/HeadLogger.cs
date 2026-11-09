using System;
using UnityEngine;
using VRClass.Core;
using VRClass.Experiment;

namespace VRClass.Logging
{
    /// <summary>
    /// 頭（HMD のカメラ）の位置・回転を headLogHz（初期値 10 Hz）で head.csv に記録する。
    /// 記録の開始・終了は授業を進めるクラス（LessonController / ExperimentManager）が呼ぶ。
    /// θ は段階5（GazeJudge）で <see cref="ThetaProvider"/> を設定して出す。それまでは空欄。
    /// </summary>
    public sealed class HeadLogger : MonoBehaviour
    {
        [Tooltip("頭の Transform（XR Origin の Main Camera）。空なら Camera.main を使う")]
        [SerializeField] private Transform head;

        [SerializeField] private ExperimentConfig config;

        private HeadLogWriter _writer;
        private Func<double> _lessonTime;
        private string _lesson;
        private IntervalTimer _timer;

        /// <summary>対象部品との角度（度）を返す。判定対象がなければ null（空欄）。</summary>
        public Func<float?> ThetaProvider { get; set; }

        public bool IsRecording => _writer != null;

        /// <summary>記録を始める。writer の後始末（Dispose）は呼び出した側が行う。</summary>
        public void Begin(HeadLogWriter writer, Func<double> lessonTime, string lesson)
        {
            if (config == null)
            {
                throw new InvalidOperationException("HeadLogger に ExperimentConfig が設定されていません。");
            }

            _writer = writer ?? throw new ArgumentNullException(nameof(writer));
            _lessonTime = lessonTime ?? throw new ArgumentNullException(nameof(lessonTime));
            _lesson = lesson;
            _timer = new IntervalTimer(config.headLogHz);
            Sample(); // 開始時点の姿勢を1行目に残す
        }

        /// <summary>記録を止める（ファイルは閉じない）。</summary>
        public void End()
        {
            _writer = null;
            _lessonTime = null;
            _timer = null;
        }

        private void Awake()
        {
            if (head == null && Camera.main != null)
            {
                head = Camera.main.transform;
            }
        }

        private void Update()
        {
            if (!IsRecording)
            {
                return;
            }

            if (_timer.Tick(Time.deltaTime))
            {
                Sample();
            }
        }

        private void Sample()
        {
            if (head == null)
            {
                return;
            }

            Vector3 p = head.position;
            Quaternion q = head.rotation;
            _writer.Write(_lessonTime(), _lesson, p.x, p.y, p.z, q.x, q.y, q.z, q.w, ThetaProvider?.Invoke());
        }
    }
}
