namespace VRClass.Core
{
    /// <summary>イージングと補間（SPEC 4.3）。</summary>
    public static class Easing
    {
        /// <summary>
        /// e(t) = 3t² − 2t³。t は 0〜1 に丸める。
        /// e(0)=0, e(1)=1, e'(0)=e'(1)=0（動き始めと終わりが滑らか）。
        /// </summary>
        public static float SmoothStep(float t)
        {
            t = Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        /// <summary>
        /// 経過時間から t（0〜1）を求める。t = 経過時間 / 所要時間。
        /// 所要時間が 0 以下なら即座に終わったものとして 1。
        /// </summary>
        public static float Progress(float elapsedSec, float durationSec)
        {
            if (durationSec <= 0f)
            {
                return 1f;
            }

            return Clamp01(elapsedSec / durationSec);
        }

        /// <summary>a から b へ e(t) で補間した値（拡大率の変化に使う）。</summary>
        public static float EasedLerp(float a, float b, float t)
        {
            return a + (b - a) * SmoothStep(t);
        }

        public static float Clamp01(float t)
        {
            if (float.IsNaN(t) || t < 0f) return 0f;
            return t > 1f ? 1f : t;
        }
    }
}
