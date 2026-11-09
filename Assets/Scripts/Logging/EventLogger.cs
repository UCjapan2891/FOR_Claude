using System;
using System.Collections.Generic;
using System.Globalization;
using VRClass.Core;

namespace VRClass.Logging
{
    /// <summary>
    /// events.csv を書く（SPEC 6章）。1人1ファイルで、2本の授業を同じファイルに追記する（lesson 列で区別）。
    /// 出来事ごとに1行書いてフラッシュする。
    ///
    /// 列：time, unix_ms, participant, group, lesson, condition, stage, part, event, detail
    /// </summary>
    public sealed class EventLogger : IDisposable
    {
        public static readonly string[] Header =
        {
            "time", "unix_ms", "participant", "group", "lesson", "condition", "stage", "part", "event", "detail",
        };

        private static readonly HashSet<string> KnownEvents = new HashSet<string>(LogEvent.All);

        private readonly CsvAppendWriter _writer;
        private readonly Func<double> _lessonTime;
        private readonly Func<long> _unixMs;
        private readonly string[] _row = new string[Header.Length];

        private string _participant = string.Empty;
        private string _group = string.Empty;
        private string _lesson = string.Empty;
        private string _condition = string.Empty;

        /// <param name="path">events.csv のパス（<see cref="LogPaths.EventsFile"/>）</param>
        /// <param name="lessonTime">授業開始からの秒を返す関数（通常は LessonClock.Elapsed）</param>
        /// <param name="unixMs">実時刻（ミリ秒）を返す関数。省略すると現在時刻。テストで差し替える</param>
        public EventLogger(string path, Func<double> lessonTime, Func<long> unixMs = null)
        {
            _lessonTime = lessonTime ?? throw new ArgumentNullException(nameof(lessonTime));
            _unixMs = unixMs ?? (() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            _writer = new CsvAppendWriter(path, Header, flushToDisk: true);
        }

        public string Path => _writer.Path;

        /// <summary>参加者番号と group（id mod 4）を設定する。氏名は扱わない。</summary>
        public void SetParticipant(int participantId, int group)
        {
            _participant = participantId.ToString(CultureInfo.InvariantCulture);
            _group = group.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>これから行う授業（lesson, condition 列）を設定する。</summary>
        public void SetLesson(LessonSession session)
        {
            _lesson = session.Lesson.ToString();
            _condition = ((int)session.Condition).ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>授業の外（練習など）では lesson, condition を空欄にする。</summary>
        public void ClearLesson()
        {
            _lesson = string.Empty;
            _condition = string.Empty;
        }

        /// <summary>
        /// 出来事を1行書く。eventName は <see cref="LogEvent"/> の定数を使う（それ以外は例外）。
        /// </summary>
        /// <param name="eventName">LogEvent の定数</param>
        /// <param name="stage">段階番号。なければ null（空欄）</param>
        /// <param name="part">部品名。なければ null</param>
        /// <param name="detail">補足（θ、回数など）。なければ null</param>
        public void Log(string eventName, int? stage = null, string part = null, string detail = null)
        {
            if (!KnownEvents.Contains(eventName))
            {
                throw new ArgumentException($"SPEC 6章にない event です：{eventName}", nameof(eventName));
            }

            _row[0] = CsvFormat.Seconds(_lessonTime());
            _row[1] = CsvFormat.Integer(_unixMs());
            _row[2] = _participant;
            _row[3] = _group;
            _row[4] = _lesson;
            _row[5] = _condition;
            _row[6] = stage.HasValue ? stage.Value.ToString(CultureInfo.InvariantCulture) : string.Empty;
            _row[7] = part ?? string.Empty;
            _row[8] = eventName;
            _row[9] = detail ?? string.Empty;
            _writer.WriteRow(_row);
        }

        public void Dispose() => _writer.Dispose();
    }
}
