using System;
using NUnit.Framework;
using VRClass.Core;

namespace VRClass.Tests
{
    /// <summary>SPEC 2章の割り当て表の4通りを確認する。</summary>
    public class ParticipantAssignmentTests
    {
        // id, group, 1本目の授業, 1本目の条件, 2本目の授業, 2本目の条件
        [TestCase(1, 1, LessonId.HDD, Condition.TelopOnly, LessonId.HMD, Condition.TelopWithEmphasis)]
        [TestCase(2, 2, LessonId.HMD, Condition.TelopOnly, LessonId.HDD, Condition.TelopWithEmphasis)]
        [TestCase(3, 3, LessonId.HDD, Condition.TelopWithEmphasis, LessonId.HMD, Condition.TelopOnly)]
        [TestCase(4, 0, LessonId.HMD, Condition.TelopWithEmphasis, LessonId.HDD, Condition.TelopOnly)]
        // 5以上でも余りで決まる
        [TestCase(5, 1, LessonId.HDD, Condition.TelopOnly, LessonId.HMD, Condition.TelopWithEmphasis)]
        [TestCase(18, 2, LessonId.HMD, Condition.TelopOnly, LessonId.HDD, Condition.TelopWithEmphasis)]
        [TestCase(19, 3, LessonId.HDD, Condition.TelopWithEmphasis, LessonId.HMD, Condition.TelopOnly)]
        [TestCase(20, 0, LessonId.HMD, Condition.TelopWithEmphasis, LessonId.HDD, Condition.TelopOnly)]
        public void Assign_MatchesSpecTable(int id, int group,
            LessonId firstLesson, Condition firstCondition,
            LessonId secondLesson, Condition secondCondition)
        {
            Assignment a = ParticipantAssignment.Assign(id);

            Assert.AreEqual(group, a.Group);
            Assert.AreEqual(firstLesson, a.First.Lesson);
            Assert.AreEqual(firstCondition, a.First.Condition);
            Assert.AreEqual(secondLesson, a.Second.Lesson);
            Assert.AreEqual(secondCondition, a.Second.Condition);
        }

        [Test]
        public void Assign_EveryGroup_HasBothLessonsAndBothConditions()
        {
            for (int id = 1; id <= 4; id++)
            {
                Assignment a = ParticipantAssignment.Assign(id);
                Assert.AreNotEqual(a.First.Lesson, a.Second.Lesson, $"id={id}：同じ授業が2回");
                Assert.AreNotEqual(a.First.Condition, a.Second.Condition, $"id={id}：同じ条件が2回");
            }
        }

        [Test]
        public void ConditionValues_AreOneAndTwo()
        {
            Assert.AreEqual(1, (int)Condition.TelopOnly);
            Assert.AreEqual(2, (int)Condition.TelopWithEmphasis);
        }

        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(-4)]
        public void Assign_NonPositiveId_Throws(int id)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ParticipantAssignment.Assign(id));
            Assert.Throws<ArgumentOutOfRangeException>(() => ParticipantAssignment.GetGroup(id));
        }

        [Test]
        public void GetSession_ReturnsByOrder()
        {
            Assignment a = ParticipantAssignment.Assign(1);
            Assert.AreEqual(a.First.Lesson, a.GetSession(1).Lesson);
            Assert.AreEqual(a.Second.Lesson, a.GetSession(2).Lesson);
            Assert.Throws<ArgumentOutOfRangeException>(() => a.GetSession(3));
        }
    }
}
