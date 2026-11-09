using UnityEngine;
using VRClass.Core;

namespace VRClass.Experiment
{
    /// <summary>
    /// 実験のパラメータをすべて集めた設定（docs/SPEC.md の [ ] の値と切り替え設定）。
    /// 初期値は SPEC のとおり。予備実験ではこの asset の値をインスペクターで変える。
    /// コードに角度・距離・秒数を直接書かず、必ずここから読む（CLAUDE.md 3章）。
    /// </summary>
    [CreateAssetMenu(fileName = "ExperimentConfig", menuName = "Experiment/Experiment Config")]
    public sealed class ExperimentConfig : ScriptableObject
    {
        [Header("4.1 注視判定")]
        [Tooltip("θth：見ているとみなす角度（度）")]
        [Range(0f, 180f)] public float gazeAngleThresholdDeg = 20f;

        [Tooltip("t_hold：θ ≤ θth がこの秒数続いたら「見ている」")]
        [Min(0f)] public float gazeHoldSec = 0.3f;

        [Tooltip("t_timeout：この秒数たっても見なければ再配置")]
        [Min(0f)] public float gazeTimeoutSec = 3f;

        [Tooltip("N：再配置の上限回数。超えたら強調せずテロップのみ（Ttotal の間）")]
        [Min(0)] public int maxRelocations = 2;

        [Header("4.2 再配置（4.3 の引き寄せ先にも使う）")]
        [Tooltip("D：頭から部品を置く位置までの距離（m）。正面は水平")]
        [Min(0f)] public float frontDistance = 0.6f;

        [Tooltip("他の部品と重なるとき、奥へずらす距離（m）")]
        [Min(0f)] public float relocateShiftStep = 0.1f;

        [Tooltip("奥へずらす回数の上限。超えたら最後の位置に置いてログに記録")]
        [Min(0)] public int relocateMaxShifts = 10;

        [Header("4.3 空間的強調（条件2）")]
        [Tooltip("T1：引き寄せの時間（秒）")]
        [Min(0f)] public float pullDurationSec = 1.0f;

        [Tooltip("dmin：引き寄せ先を頭からこの距離以上離す（m）")]
        [Min(0f)] public float minHeadDistance = 0.4f;

        [Tooltip("T2：拡大の時間（秒）")]
        [Min(0f)] public float scaleDurationSec = 0.5f;

        [Tooltip("φ：拡大後の見かけの大きさ（視角、度）。倍率は1未満にしない")]
        [Range(0f, 179f)] public float targetViewAngleDeg = 30f;

        [Tooltip("ω：回転の速さ（度/秒）。鉛直軸まわりに1回転する")]
        [Min(0.01f)] public float rotationSpeedDegPerSec = 45f;

        [Tooltip("観察フェーズ（拡大したまま止める）を入れるか")]
        public bool useObservePhase = false;

        [Tooltip("T_obs：観察フェーズの時間（秒）")]
        [Min(0f)] public float observeDurationSec = 3f;

        [Tooltip("T4：復帰の時間（秒）")]
        [Min(0f)] public float returnDurationSec = 1.0f;

        [Header("4.4 テロップ")]
        [Tooltip("強調中に部品が動くとき、テロップを部品について動かすか")]
        public bool telopFollowsPart = true;

        [Header("5.1 掴む・付ける")]
        [Tooltip("ソケットに吸着する向きのずれの上限（度）")]
        [Range(0f, 180f)] public float socketAngleToleranceDeg = 15f;

        [Tooltip("ソケットに吸着する距離（m）")]
        [Min(0f)] public float socketSnapDistance = 0.05f;

        [Tooltip("正しい位置に置いた部品を固定して動かせなくするか（下の部品が先に置かれていないときは固定しない）")]
        public bool lockPlacedParts = true;

        [Tooltip("部品がこの高さ（m）より下に落ちたら、床に触れたときと同じく机の上の初期位置へ戻す")]
        public float fallResetY = -1f;

        [Header("5.3 自動タイマー")]
        [Tooltip("一定時間でヒント・自動完了するタイマーを使うか")]
        public bool useAutoTimers = false;

        [Tooltip("ヒントを出すまでの秒数")]
        [Min(0f)] public float autoHintAfterSec = 60f;

        [Tooltip("自動で完了させるまでの秒数")]
        [Min(0f)] public float autoCompleteAfterSec = 120f;

        [Header("5.3 実験者の操作・自動一時停止")]
        [Tooltip("HMD を外した・トラッキングを失った状態がこの秒数続いたら自動で一時停止する")]
        [Min(0f)] public float presenceGraceSec = 0.5f;

        [Tooltip("中断（A）は、この秒数以内に2回押したときだけ行う（誤操作を防ぐ）")]
        [Min(0.1f)] public float abortDoublePressSec = 2f;

        [Header("3章 授業の流れ")]
        [Tooltip("授業の終わりに、全ソケットに正しい部品が入っていることを確認するか")]
        public bool requireAllSocketsFilled = true;

        [Header("2章 参加者")]
        [Tooltip("参加者番号の一覧ファイル名（Application.persistentDataPath に置く）")]
        public string participantsFileName = "participants.csv";

        [Header("6章 ログ")]
        [Tooltip("head.csv の記録頻度（Hz）")]
        [Min(0.1f)] public float headLogHz = 10f;

        [Tooltip("time 列から一時停止中の時間を除くか（false＝含める）")]
        public bool timeExcludesPause = false;

        [Header("入力（本実験では ControllerOnly）")]
        [Tooltip("部品の操作を受け付ける入力。実行中は F1 キーで切り替えられる")]
        public InputMode inputMode = InputMode.Both;

        [Header("開発用（SPEC にない値。本実験の結果には影響しない）")]
        [Tooltip("説明音声（AudioClip）が未設定のとき、音声の代わりに待つ秒数")]
        [Min(0f)] public float missingAudioPlaceholderSec = 2f;

        [Tooltip("HMD がないときのカメラの高さ（m、床から）")]
        [Min(0f)] public float debugEyeHeight = 1.6f;

        [Tooltip("マウスで視点を回す感度（度/ピクセル）")]
        [Min(0f)] public float debugLookSensitivity = 0.15f;

        [Tooltip("キーで部品を回す速さ（度/秒）")]
        [Min(0f)] public float debugRotateSpeedDegPerSec = 90f;

        [Tooltip("矢印キーで移動する速さ（m/秒）")]
        [Min(0f)] public float debugMoveSpeed = 1.0f;
    }
}
