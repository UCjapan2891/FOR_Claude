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

namespace VRClass.Core
{
    /// <summary>段階の完了条件（SPEC 3章 StageData）。</summary>
    public enum CompletionType
    {
        /// <summary>対象部品をソケットから外す</summary>
        Detach,

        /// <summary>対象部品を正しいソケットに付ける</summary>
        Attach,

        /// <summary>対象部品を掴む（コントローラーのボタン、またはデバッグ用のマウス）</summary>
        Touch,

        /// <summary>操作なし（説明音声が終わったら完了）</summary>
        None,
    }

    /// <summary>
    /// 入力の受け付け方。コントローラーのみ／両方／デバッグ用のキーボード・マウスのみ。
    /// 本実験では ControllerOnly にする。
    /// </summary>
    public enum InputMode
    {
        ControllerOnly,
        Both,
        KeyboardMouseOnly,
    }

    /// <summary>回転対称の部品の対称軸（部品のローカル軸）。この軸まわりの回転は向きの判定から外す。</summary>
    public enum SymmetryAxis
    {
        None,
        X,
        Y,
        Z,
    }

    /// <summary>入力の受け付け方の規則。</summary>
    public static class InputModeRules
    {
        public static bool AllowsController(InputMode mode) => mode != InputMode.KeyboardMouseOnly;

        public static bool AllowsKeyboardMouse(InputMode mode) => mode != InputMode.ControllerOnly;

        /// <summary>切り替えキーで次のモードへ（コントローラーのみ → 両方 → キーマウのみ → …）。</summary>
        public static InputMode Next(InputMode mode)
        {
            switch (mode)
            {
                case InputMode.ControllerOnly: return InputMode.Both;
                case InputMode.Both: return InputMode.KeyboardMouseOnly;
                default: return InputMode.ControllerOnly;
            }
        }

        public static string Label(InputMode mode)
        {
            switch (mode)
            {
                case InputMode.ControllerOnly: return "コントローラーのみ";
                case InputMode.Both: return "コントローラー＋キーマウ";
                default: return "キーマウのみ（デバッグ）";
            }
        }
    }
}
