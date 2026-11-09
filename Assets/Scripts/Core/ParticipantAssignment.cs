using System;

namespace VRClass.Core
{
    /// <summary>参加者1人分の割り当て結果。</summary>
    public readonly struct Assignment
    {
        /// <summary>id mod 4（0〜3）。ログの group 列に出す。</summary>
        public readonly int Group;

        /// <summary>1本目の授業。</summary>
        public readonly LessonSession First;

        /// <summary>2本目の授業。</summary>
        public readonly LessonSession Second;

        public Assignment(int group, LessonSession first, LessonSession second)
        {
            Group = group;
            First = first;
            Second = second;
        }

        /// <summary>order = 1 なら1本目、2 なら2本目を返す。</summary>
        public LessonSession GetSession(int order)
        {
            switch (order)
            {
                case 1: return First;
                case 2: return Second;
                default: throw new ArgumentOutOfRangeException(nameof(order), order, "order は 1 か 2 です。");
            }
        }
    }

    /// <summary>
    /// 参加者番号から授業と条件の順番を決める（SPEC 2章）。
    ///
    /// | id mod 4 | 1本目          | 2本目          |
    /// |----------|----------------|----------------|
    /// | 1        | HDD・条件1     | HMD・条件2     |
    /// | 2        | HMD・条件1     | HDD・条件2     |
    /// | 3        | HDD・条件2     | HMD・条件1     |
    /// | 0        | HMD・条件2     | HDD・条件1     |
    /// </summary>
    public static class ParticipantAssignment
    {
        public const int GroupCount = 4;

        /// <summary>id mod 4 を返す。id は正の整数のみ。</summary>
        public static int GetGroup(int id)
        {
            if (id <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(id), id, "参加者番号は正の整数です。");
            }

            return id % GroupCount;
        }

        /// <summary>id から group と2本の授業の割り当てを返す。</summary>
        public static Assignment Assign(int id)
        {
            int group = GetGroup(id);
            switch (group)
            {
                case 1:
                    return new Assignment(group,
                        new LessonSession(LessonId.HDD, Condition.TelopOnly),
                        new LessonSession(LessonId.HMD, Condition.TelopWithEmphasis));
                case 2:
                    return new Assignment(group,
                        new LessonSession(LessonId.HMD, Condition.TelopOnly),
                        new LessonSession(LessonId.HDD, Condition.TelopWithEmphasis));
                case 3:
                    return new Assignment(group,
                        new LessonSession(LessonId.HDD, Condition.TelopWithEmphasis),
                        new LessonSession(LessonId.HMD, Condition.TelopOnly));
                default: // 0
                    return new Assignment(group,
                        new LessonSession(LessonId.HMD, Condition.TelopWithEmphasis),
                        new LessonSession(LessonId.HDD, Condition.TelopOnly));
            }
        }
    }
}
