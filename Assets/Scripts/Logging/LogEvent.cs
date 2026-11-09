namespace VRClass.Logging
{
    /// <summary>events.csv の event 列に書く名前（SPEC 6章）。文字列を直接書かず、ここの定数を使う。</summary>
    public static class LogEvent
    {
        public const string LessonStart = "lesson_start";
        public const string AudioStart = "audio_start";
        public const string AudioEnd = "audio_end";
        public const string Grab = "grab";
        public const string Release = "release";
        public const string SocketOk = "socket_ok";
        public const string Reset = "reset";
        public const string Hint = "hint";
        public const string Skip = "skip";
        public const string OpComplete = "op_complete";
        public const string GazeOk = "gaze_ok";
        public const string GazeTimeout = "gaze_timeout";
        public const string Relocate = "relocate";
        public const string GazeFail = "gaze_fail";
        public const string EmphasisStart = "emphasis_start";
        public const string EmphasisPhase = "emphasis_phase";
        public const string EmphasisEnd = "emphasis_end";
        public const string Pause = "pause";
        public const string Resume = "resume";
        public const string Abort = "abort";
        public const string LessonEnd = "lesson_end";

        /// <summary>SPEC 6章の一覧すべて（テストで照合する）。</summary>
        public static readonly string[] All =
        {
            LessonStart, AudioStart, AudioEnd, Grab, Release, SocketOk, Reset, Hint, Skip, OpComplete,
            GazeOk, GazeTimeout, Relocate, GazeFail, EmphasisStart, EmphasisPhase, EmphasisEnd,
            Pause, Resume, Abort, LessonEnd,
        };
    }
}
