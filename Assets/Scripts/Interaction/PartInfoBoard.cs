using TMPro;
using UnityEngine;

namespace VRClass.Interaction
{
    /// <summary>
    /// 黒板：触れた部品の名前と役割を表示する（World Space の Canvas ＋ TextMeshPro）。
    /// 何も触れていないときは案内の文を出す。
    /// </summary>
    public sealed class PartInfoBoard : MonoBehaviour
    {
        public const string DefaultTitle = "HDD の内部構造";
        public const string DefaultBody = "部品に触れると、ここに名前と役割が表示されます。";

        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text body;

        private void Awake()
        {
            ShowDefault();
        }

        public void Show(PartDescription description)
        {
            if (description == null)
            {
                ShowDefault();
                return;
            }

            Set(description.displayName, description.role);
        }

        public void ShowDefault()
        {
            Set(DefaultTitle, DefaultBody);
        }

        private void Set(string t, string b)
        {
            if (title != null) title.text = t;
            if (body != null) body.text = b;
        }
    }
}
