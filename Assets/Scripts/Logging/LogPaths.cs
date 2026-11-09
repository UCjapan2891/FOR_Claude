using System;
using System.Globalization;
using System.IO;

namespace VRClass.Logging
{
    /// <summary>
    /// ログの保存先（SPEC 6章：Application.persistentDataPath/&lt;id&gt;/）。
    /// root には実行時に Application.persistentDataPath を渡す（テストでは一時フォルダ）。
    /// </summary>
    public static class LogPaths
    {
        public const string EventsFileName = "events.csv";
        public const string HeadFileName = "head.csv";

        public static string ParticipantDirectory(string root, int participantId)
        {
            if (participantId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(participantId), participantId, "参加者番号は正の整数です。");
            }

            return Path.Combine(root, participantId.ToString(CultureInfo.InvariantCulture));
        }

        public static string EventsFile(string root, int participantId)
            => Path.Combine(ParticipantDirectory(root, participantId), EventsFileName);

        public static string HeadFile(string root, int participantId)
            => Path.Combine(ParticipantDirectory(root, participantId), HeadFileName);
    }
}
