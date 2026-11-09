namespace VRClass.Core
{
    /// <summary>授業の種類（SPEC 1章）。</summary>
    public enum LessonId
    {
        HDD,
        HMD,
    }

    /// <summary>
    /// 演出の条件（SPEC 1章）。値はログの condition 列にそのまま出す。
    /// 条件による動作の違いは EmphasisController の中だけで扱う（CLAUDE.md 3章）。
    /// </summary>
    public enum Condition
    {
        /// <summary>条件1：テロップのみ</summary>
        TelopOnly = 1,

        /// <summary>条件2：テロップ＋空間的強調</summary>
        TelopWithEmphasis = 2,
    }

    /// <summary>1本の授業の割り当て（授業と条件の組）。</summary>
    public readonly struct LessonSession
    {
        public readonly LessonId Lesson;
        public readonly Condition Condition;

        public LessonSession(LessonId lesson, Condition condition)
        {
            Lesson = lesson;
            Condition = condition;
        }

        public override string ToString() => $"{Lesson}・条件{(int)Condition}";
    }
}
