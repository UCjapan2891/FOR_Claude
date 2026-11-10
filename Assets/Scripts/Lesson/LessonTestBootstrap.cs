using System.Collections;
using UnityEngine;
using VRClass.Core;
using VRClass.Emphasis;
using VRClass.Experiment;
using VRClass.Interaction;
using VRClass.Logging;

namespace VRClass.Lesson
{
    /// <summary>
    /// テスト用：シーンを再生したら、決めた参加者番号で1本の授業を始める。
    /// 本番の流れ（番号入力 → 割り当て → 2本の授業）は段階8の ExperimentManager で作る。
    /// ログは Application.persistentDataPath/&lt;participantId&gt;/ に書く（本番の番号と混ざらないよう 9001 番）。
    /// 条件は番号の割り当て（SPEC 2章）で決まる。HDD なら 9001 → 条件1、9003 → 条件2。
    /// </summary>
    public sealed class LessonTestBootstrap : MonoBehaviour
    {
        [Tooltip("テスト用の参加者番号（本番の番号と混ぜない）")]
        [SerializeField] private int participantId = 9001;

        [SerializeField] private LessonData lesson;
        [SerializeField] private ExperimentConfig config;
        [SerializeField] private LessonController controller;
        [SerializeField] private SessionLog sessionLog;
        [SerializeField] private HeadLogger headLogger;
        [SerializeField] private InputModeController inputMode;

        [Tooltip("強調。割り当てで決まった条件をここに渡す")]
        [SerializeField] private EmphasisController emphasis;

        private EventLogger _events;
        private HeadLogWriter _head;

        private IEnumerator Start()
        {
            // XRI のソケットが最初の部品を掴むまで1フレーム待つ（その socket_ok は記録しない）
            yield return null;

            if (!CheckReferences())
            {
                yield break;
            }

            Assignment assignment = ParticipantAssignment.Assign(participantId);
            LessonSession session = assignment.First.Lesson == lesson.lessonId ? assignment.First : assignment.Second;

            string root = Application.persistentDataPath;
            var clock = new LessonClock(config.timeExcludesPause);
            _events = new EventLogger(LogPaths.EventsFile(root, participantId), () => clock.Elapsed);
            _events.SetParticipant(participantId, assignment.Group);
            _events.SetLesson(session);
            _head = new HeadLogWriter(LogPaths.HeadFile(root, participantId));

            if (emphasis != null)
            {
                emphasis.SetCondition(session.Condition);
            }

            sessionLog.Begin(_events, clock);
            if (headLogger != null)
            {
                headLogger.Begin(_head, () => clock.Elapsed, lesson.lessonId.ToString());
            }

            controller.Finished += OnFinished;
            controller.Aborted += OnFinished;
            string input = inputMode != null ? inputMode.Current.ToString() : "unknown";
            controller.StartLesson(lesson, $"test=true;order={(session.Lesson == assignment.First.Lesson ? 1 : 2)};input={input}");
            Debug.Log($"授業を開始しました：参加者 {participantId}（{session}）。ログ：{LogPaths.ParticipantDirectory(root, participantId)}");
        }

        /// <summary>インスペクターの参照が空なら、どれが空かを Console に出して授業を始めない。</summary>
        private bool CheckReferences()
        {
            string missing = string.Empty;
            if (lesson == null) missing += " Lesson";
            if (config == null) missing += " Config";
            if (controller == null) missing += " Controller";
            if (sessionLog == null) missing += " Session Log";
            if (missing.Length == 0)
            {
                return true;
            }

            Debug.LogError($"LessonTestBootstrap の参照が空です：{missing}。" +
                           "Tools → Experiment → Build Lesson Test Scene でシーンを作り直すか、インスペクターで割り当ててください。", this);
            return false;
        }

        private void OnFinished()
        {
            if (headLogger != null)
            {
                headLogger.End();
            }

            Debug.Log("授業が終わりました。");
        }

        private void OnDestroy()
        {
            if (controller != null)
            {
                controller.Finished -= OnFinished;
                controller.Aborted -= OnFinished;
            }

            if (headLogger != null)
            {
                headLogger.End();
            }

            if (sessionLog != null)
            {
                sessionLog.End();
            }

            _events?.Dispose();
            _head?.Dispose();
        }
    }
}
