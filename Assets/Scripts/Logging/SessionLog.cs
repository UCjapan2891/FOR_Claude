using System;
using UnityEngine;
using VRClass.Core;

namespace VRClass.Logging
{
    /// <summary>
    /// シーンに1つ置き、各クラスから events.csv に書くための窓口。
    /// 授業の時計（LessonClock）を Time.deltaTime で進め、現在の段階番号を stage 列に付ける。
    /// 授業が始まる前（Begin 前）の出来事は書かない。
    /// </summary>
    public sealed class SessionLog : MonoBehaviour
    {
        private EventLogger _logger;

        /// <summary>授業の時計。Begin 前は null。</summary>
        public LessonClock Clock { get; private set; }

        public bool IsActive => _logger != null;

        /// <summary>stage 列に書く段階番号（1 始まり）。段階の外では null。</summary>
        public int? CurrentStage { get; set; }

        /// <summary>記録を始める。logger の後始末（Dispose）は呼び出した側が行う。</summary>
        public void Begin(EventLogger logger, LessonClock clock)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            Clock = clock ?? throw new ArgumentNullException(nameof(clock));
            CurrentStage = null;
        }

        public void End()
        {
            _logger = null;
            Clock = null;
            CurrentStage = null;
        }

        /// <summary>出来事を1行書く（その場でフラッシュ）。書けなくても授業は止めない。</summary>
        public void Log(string eventName, string part = null, string detail = null)
        {
            if (!IsActive)
            {
                return;
            }

            try
            {
                _logger.Log(eventName, CurrentStage, part, detail);
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }
        }

        private void Update()
        {
            Clock?.Tick(Time.deltaTime);
        }
    }
}
