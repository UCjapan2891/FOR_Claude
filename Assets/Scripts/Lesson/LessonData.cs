using System;
using System.Collections.Generic;
using UnityEngine;
using VRClass.Core;

namespace VRClass.Lesson
{
    /// <summary>
    /// 1段階の中身（SPEC 3章 StageData）。LessonData の中のリストとして持つ。
    /// </summary>
    [Serializable]
    public sealed class StageData
    {
        [Tooltip("段階名（例：アームを外す）")]
        public string stageName;

        [Tooltip("対象部品の partId（PartInteraction と一致させる）。完了条件が「なし」なら空でもよい")]
        public string targetPartId;

        [Tooltip("説明音声：そのパートで何をするか。操作の前に流す（2D）")]
        public AudioClip narrationClip;

        [Tooltip("解説音声（任意）：要点の仕組みの解説。強調（条件1は停止）の間に流す。長さは Ttotal 以内")]
        public AudioClip commentaryClip;

        [Tooltip("テロップの文言（要点のパートでだけ表示）")]
        [TextArea(2, 4)] public string telopText;

        [Tooltip("完了条件")]
        public CompletionType completion = CompletionType.Detach;

        [Tooltip("要点かどうか")]
        public bool isKeyPoint;
    }

    /// <summary>
    /// 授業の中身（SPEC 3章 LessonData）。HDD と HMD で同じプログラムを使い、この asset だけを差し替える。
    /// </summary>
    [CreateAssetMenu(fileName = "Lesson", menuName = "Experiment/Lesson Data")]
    public sealed class LessonData : ScriptableObject
    {
        [Tooltip("授業の種類（ログの lesson 列）")]
        public LessonId lessonId = LessonId.HDD;

        [Tooltip("段階の一覧（上から順に進む）")]
        public List<StageData> stages = new List<StageData>();

        /// <summary>状態遷移用の情報に変換する。</summary>
        public List<FlowStage> ToFlowStages()
        {
            var list = new List<FlowStage>(stages.Count);
            foreach (StageData s in stages)
            {
                list.Add(new FlowStage(s.completion, s.isKeyPoint));
            }

            return list;
        }
    }
}
