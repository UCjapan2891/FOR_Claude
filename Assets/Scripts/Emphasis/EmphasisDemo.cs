using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using VRClass.Experiment;

namespace VRClass.Emphasis
{
    /// <summary>
    /// 発表用のデモ：P を押すと、対象の部品にすぐ空間的強調（引き寄せ → 拡大 → 回転 → 復帰）をかける。
    /// 実験の授業の流れ・ログとは関係しない（Assets/Scenes/EmphasisDemo.unity でだけ使う）。
    /// 操作：P 強調／R 中止して元に戻す／L 引き寄せの有無を切り替え／H 画面の説明を消す・出す／Tab 対象の部品を替える
    /// </summary>
    public sealed class EmphasisDemo : MonoBehaviour
    {
        [SerializeField] private Transform[] targets;
        [SerializeField] private Transform head;
        [SerializeField] private ExperimentConfig config;

        [Tooltip("引き寄せを入れるか（false ならその場で拡大と回転だけ）")]
        [SerializeField] private bool includePull = true;

        [SerializeField] private bool showOverlay = true;

        private int _index;
        private SpatialEmphasis _emphasis;
        private Coroutine _running;
        private GUIStyle _style;

        private Transform Target => targets != null && targets.Length > 0 ? targets[_index % targets.Length] : null;

        private void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null)
            {
                return;
            }

            if (kb.pKey.wasPressedThisFrame && _running == null && Target != null)
            {
                _emphasis = new SpatialEmphasis(Target);
                _running = StartCoroutine(Play());
            }

            if (kb.rKey.wasPressedThisFrame)
            {
                Stop();
            }

            if (kb.lKey.wasPressedThisFrame && _running == null)
            {
                includePull = !includePull;
            }

            if (kb.hKey.wasPressedThisFrame)
            {
                showOverlay = !showOverlay;
            }

            if (kb.tabKey.wasPressedThisFrame && _running == null && targets != null && targets.Length > 0)
            {
                _index = (_index + 1) % targets.Length;
            }
        }

        private IEnumerator Play()
        {
            Transform h = head != null ? head : (Camera.main != null ? Camera.main.transform : null);
            yield return _emphasis.Play(h, config, includePull);
            _running = null;
        }

        private void Stop()
        {
            if (_running != null)
            {
                StopCoroutine(_running);
                _running = null;
            }

            _emphasis?.Restore();
        }

        private void OnGUI()
        {
            if (!showOverlay)
            {
                return;
            }

            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 18, wordWrap = true };
            }

            string phase = _emphasis == null || _running == null ? "待機中" : PhaseLabel(_emphasis.Phase);
            string detail = _emphasis != null && _running != null
                ? $"　{_emphasis.PhaseElapsed:0.0} / {_emphasis.PhaseDuration:0.0} 秒　拡大率 s = {_emphasis.ScaleFactor:0.00}"
                : string.Empty;
            string text = $"空間的強調のデモ　対象：{(Target != null ? Target.name : "-")}\n" +
                          $"状態：{phase}{detail}\n" +
                          $"P：強調　R：中止　L：引き寄せ {(includePull ? "あり" : "なし")}　Tab：対象を替える　H：この表示を消す";
            GUI.Box(new Rect(10, 10, 760, 90), text, _style);
        }

        private static string PhaseLabel(EmphasisPhase p)
        {
            switch (p)
            {
                case EmphasisPhase.Pull: return "① 引き寄せ";
                case EmphasisPhase.Scale: return "② 拡大";
                case EmphasisPhase.Rotate: return "③ 回転";
                case EmphasisPhase.Observe: return "（観察）";
                case EmphasisPhase.Return: return "④ 復帰";
                case EmphasisPhase.Done: return "完了";
                default: return "待機中";
            }
        }
    }
}
