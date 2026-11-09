using System;
using System.IO;
using System.Text;
using NUnit.Framework;
using VRClass.Core;
using VRClass.Logging;

namespace VRClass.Tests
{
    /// <summary>実際に一時フォルダへファイルを書いて、SPEC 6章の列どおりかを確かめる。</summary>
    public class LoggingTests
    {
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "vrclass_logtest_" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, true);
            }
        }

        /// <summary>書き込み中のファイルも読めるように FileShare.ReadWrite で開く。</summary>
        private static string ReadShared(string path)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var sr = new StreamReader(fs, new UTF8Encoding(false)))
            {
                return sr.ReadToEnd();
            }
        }

        private static string[] Lines(string text)
        {
            return text.TrimEnd('\n').Split('\n');
        }

        [Test]
        public void LogPaths_AreUnderParticipantFolder()
        {
            Assert.AreEqual(Path.Combine(_root, "12", "events.csv"), LogPaths.EventsFile(_root, 12));
            Assert.AreEqual(Path.Combine(_root, "12", "head.csv"), LogPaths.HeadFile(_root, 12));
            Assert.Throws<ArgumentOutOfRangeException>(() => LogPaths.ParticipantDirectory(_root, 0));
        }

        [Test]
        public void LogEvent_HasAllSpecEvents()
        {
            string[] spec =
            {
                "lesson_start", "audio_start", "audio_end", "grab", "release", "socket_ok", "reset", "hint",
                "skip", "op_complete", "gaze_ok", "gaze_timeout", "relocate", "gaze_fail", "emphasis_start",
                "emphasis_phase", "emphasis_end", "pause", "resume", "abort", "lesson_end",
            };
            CollectionAssert.AreEquivalent(spec, LogEvent.All);
        }

        [Test]
        public void EventLogger_WritesSpecColumns_AndFlushesEachLine()
        {
            string path = LogPaths.EventsFile(_root, 5);
            double time = 0;
            using (var logger = new EventLogger(path, () => time, () => 1700000000123L))
            {
                Assignment a = ParticipantAssignment.Assign(5);
                logger.SetParticipant(5, a.Group);
                logger.SetLesson(a.First);

                logger.Log(LogEvent.LessonStart);
                time = 12.34567;
                logger.Log(LogEvent.GazeOk, stage: 3, part: "アーム", detail: "theta=12.5,hold=0.3");

                // 閉じる前でも、書いた行はファイルに出ている
                string[] lines = Lines(ReadShared(path));
                Assert.AreEqual(3, lines.Length);
                Assert.AreEqual("time,unix_ms,participant,group,lesson,condition,stage,part,event,detail", lines[0]);
                Assert.AreEqual("0.000,1700000000123,5,1,HDD,1,,,lesson_start,", lines[1]);
                Assert.AreEqual("12.346,1700000000123,5,1,HDD,1,3,アーム,gaze_ok,\"theta=12.5,hold=0.3\"", lines[2]);
            }
        }

        [Test]
        public void EventLogger_AppendsSecondLesson_WithoutSecondHeader()
        {
            string path = LogPaths.EventsFile(_root, 6);
            Assignment a = ParticipantAssignment.Assign(6);

            using (var logger = new EventLogger(path, () => 0, () => 0))
            {
                logger.SetParticipant(6, a.Group);
                logger.SetLesson(a.First);
                logger.Log(LogEvent.LessonEnd);
            }

            using (var logger = new EventLogger(path, () => 0, () => 0))
            {
                logger.SetParticipant(6, a.Group);
                logger.SetLesson(a.Second);
                logger.Log(LogEvent.LessonStart);
            }

            byte[] bytes = File.ReadAllBytes(path);
            Assert.AreEqual(0xEF, bytes[0]);
            Assert.AreEqual(0xBB, bytes[1]);
            Assert.AreEqual(0xBF, bytes[2]);

            string text = new UTF8Encoding(false).GetString(bytes, 3, bytes.Length - 3);
            Assert.AreEqual(-1, text.IndexOf('﻿'), "BOM はファイルの先頭に1回だけ");
            string[] lines = Lines(text);
            Assert.AreEqual(3, lines.Length);
            StringAssert.StartsWith("time,", lines[0]);
            StringAssert.Contains(",HMD,1,", lines[1]); // id 6 → group 2：1本目 HMD・条件1
            StringAssert.Contains(",HDD,2,", lines[2]); //               2本目 HDD・条件2
        }

        [Test]
        public void EventLogger_ClearLesson_LeavesBlank()
        {
            string path = LogPaths.EventsFile(_root, 7);
            using (var logger = new EventLogger(path, () => 0, () => 0))
            {
                logger.SetParticipant(7, 3);
                logger.ClearLesson();
                logger.Log(LogEvent.Pause);
                string[] lines = Lines(ReadShared(path));
                Assert.AreEqual("0.000,0,7,3,,,,,pause,", lines[1]);
            }
        }

        [Test]
        public void EventLogger_UnknownEvent_Throws()
        {
            using (var logger = new EventLogger(LogPaths.EventsFile(_root, 8), () => 0, () => 0))
            {
                Assert.Throws<ArgumentException>(() => logger.Log("lesson_begin"));
            }
        }

        [Test]
        public void EventLogger_AfterDispose_Throws()
        {
            var logger = new EventLogger(LogPaths.EventsFile(_root, 9), () => 0, () => 0);
            logger.Dispose();
            Assert.Throws<ObjectDisposedException>(() => logger.Log(LogEvent.Abort));
        }

        [Test]
        public void HeadLogWriter_WritesColumns_ThetaBlankWhenNull()
        {
            string path = LogPaths.HeadFile(_root, 10);
            using (var w = new HeadLogWriter(path))
            {
                w.Write(0.1, "HMD", 0.1f, 1.6f, -0.25f, 0f, 0f, 0f, 1f, null);
                w.Write(0.2, "HMD", 0.1f, 1.6f, -0.25f, 0f, 0.70711f, 0f, 0.70711f, 12.345f);

                string[] lines = Lines(ReadShared(path));
                Assert.AreEqual(3, lines.Length);
                Assert.AreEqual("time,lesson,pos_x,pos_y,pos_z,rot_x,rot_y,rot_z,rot_w,theta", lines[0]);
                Assert.AreEqual("0.100,HMD,0.1000,1.6000,-0.2500,0.00000,0.00000,0.00000,1.00000,", lines[1]);
                Assert.AreEqual("0.200,HMD,0.1000,1.6000,-0.2500,0.00000,0.70711,0.00000,0.70711,12.35", lines[2]);
            }
        }

        [Test]
        public void CsvAppendWriter_WrongColumnCount_Throws()
        {
            using (var w = new CsvAppendWriter(Path.Combine(_root, "x.csv"), new[] { "a", "b" }, false))
            {
                Assert.Throws<ArgumentException>(() => w.WriteRow(new[] { "1" }));
            }
        }
    }
}
